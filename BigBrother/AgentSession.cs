using System.Text;
using BigBrother.Utilities;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Transcription;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using OpenAI.RealtimeConversation;

#pragma warning disable OPENAI002

namespace BigBrother;

public interface IAgentAudioInput : IWaveProvider
{
}

public interface IAgentAudioOutput
{
    void Write(ReadOnlyMemory<byte> data);

    void Clear();
}

public class AgentSession(
    IAgentAudioInput audioInput,
    IAgentAudioOutput audioOutput,
    ILogger<AgentSession> logger,
    RealtimeConversationClient client,
    SpeechConfig speechConfig) : BackgroundService
{
    private readonly RecorderWrapperProvider _recorder = new(audioInput);

    private RealtimeConversationSession? _session = null;

    private readonly List<AgentContext> _contexts = [];

    private readonly Dictionary<string, AgentTool> _tools = new();

    private bool _responsing = false;

    internal bool NeedReconfigure { get; set; }

    public void AddContext(AgentContext context)
    {
        context.Session = this;
        _contexts.Add(context);
        if (_session != null)
            context.OnSessionBegin();
        NeedReconfigure = true;
    }

    public void RemoveContext(AgentContext context)
    {
        _contexts.Remove(context);
        if (_session != null)
            context.OnSessionEnd();
        context.Session = null!;
        NeedReconfigure = true;
    }

    public TContext? SearchContext<TContext>() where TContext : AgentContext
    {
        return _contexts.First(context => context is TContext) as TContext;
    }

    public TContext? RequireContext<TContext>() where TContext : AgentContext
    {
        return SearchContext<TContext>() ??
               throw new Exception($"Cannot find the required context of type {typeof(TContext)}.");
    }

    public string BaseInstructions { get; set; } =
        """
        You are a helpful robot agent developed by JSK Robotics Laboratory,
        your name is Big Brother, you are in the office of the JSK Robotics Laboratory in Tokyo.
        Try to respond in short as possible as you can.
        """;

    private async Task ConfigureAgent(
        ConversationSessionOptions? options,
        CancellationToken stopppingToken)
    {
        if (_session == null)
            throw new InvalidOperationException("Session has not been initialized.");

        options ??= new ConversationSessionOptions();

        options.Tools.Add(ConversationTool.CreateFunctionTool(
            "session-end_conversation",
            "When user explicitly says sentences such as 'end this conversation', 'that's all for now' that " +
            "indicates the user want to end this conversation, you should invoke this tool."));

        _tools.Clear();

        var instructions = new StringBuilder();
        instructions.AppendLine(BaseInstructions);
        foreach (var context in _contexts)
        {
            if (stopppingToken.IsCancellationRequested)
                return;

            await context.OnBuild();

            foreach (var tool in context.Tools)
            {
                _tools[tool.Name] = tool;
                options.Tools.Add(tool.Descriptor);
            }

            if (!string.IsNullOrEmpty(context.Instructions))
                instructions.Append(context.Instructions);
        }

        options.Instructions = instructions.ToString();

        await _session.ConfigureSessionAsync(options, stopppingToken);

        NeedReconfigure = false;
    }

    private async Task Respond(ConversationResponseOptions? options, CancellationToken cancellationToken)
    {
        if (_responsing)
        {
            await _session!.CancelResponseAsync(cancellationToken);
        }

        if (options != null)
            await _session!.StartResponseAsync(options, cancellationToken);
        else
            await _session!.StartResponseAsync(cancellationToken);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _session = await client.StartConversationSessionAsync(stoppingToken);
        
        await ConfigureAgent(new ConversationSessionOptions
        {
            TurnDetectionOptions = ConversationTurnDetectionOptions
                .CreateServerVoiceActivityTurnDetectionOptions(
                    detectionThreshold: 0.5f,
                    prefixPaddingDuration: TimeSpan.FromMilliseconds(200),
                    silenceDuration: TimeSpan.FromMilliseconds(500),
                    enableAutomaticResponseCreation: false),
            InputAudioFormat = ConversationAudioFormat.Pcm16,
            OutputAudioFormat = ConversationAudioFormat.Pcm16,
            Voice = ConversationVoice.Ash,
            InputTranscriptionOptions = new ConversationInputTranscriptionOptions()
            {
                Model = ConversationTranscriptionModel.Whisper1
            },
        }, stoppingToken);

        var jobAudioSender = new AudioSenderJob(_session, _recorder, logger);

        // With the session configured, we start processing commands received from the service.
        await foreach (var update in _session.ReceiveUpdatesAsync(stoppingToken))
        {
            var terminating = false;

            if (NeedReconfigure)
            {
                await ConfigureAgent(null, stoppingToken);
                NeedReconfigure = false;
            }

            switch (update)
            {
                // session.created is the very first command on a session and lets us know that connection was successful.
                case ConversationSessionStartedUpdate:
                    logger.LogInformation("Conversation session started.");
                    await jobAudioSender.StartAsync(stoppingToken);
                    logger.LogInformation("Begin pushing audio to the service.");
                    foreach (var context in _contexts)
                        await context.OnSessionBegin();
                    break;
                // input_audio_buffer.speech_started tells us that the beginning of speech was detected in the input audio
                // we're sending from the microphone.
                case ConversationInputSpeechStartedUpdate speechStartedUpdate:
                    logger.LogInformation("User begin speech at {AudioBeginTime}", speechStartedUpdate.AudioStartTime);

                    _recorder.StartRecording(speechStartedUpdate.AudioStartTime);

                    audioOutput.Clear();

                    foreach (var context in _contexts)
                        await context.OnUserSpeechBegin(
                            speechStartedUpdate.AudioStartTime,
                            speechStartedUpdate.ItemId);
                    break;

                case ConversationInputSpeechFinishedUpdate speechFinishedUpdate:
                    logger.LogInformation("User speech ended at {AudioEndTime}", speechFinishedUpdate.AudioEndTime);

                    var speech = _recorder.StopRecording(speechFinishedUpdate.AudioEndTime);

                    if (speech.Duration < TimeSpan.FromMilliseconds(100))
                    {
                        logger.LogInformation("Speech duration {SpeechDuration} is too short, skipped.",
                            speech.Duration);
                        break;
                    }

                    var instructions = new StringBuilder();
                    foreach (var context in _contexts)
                    {
                        var instruction = await context.OnUserSpeechEnd(
                            speechFinishedUpdate.AudioEndTime,
                            speechFinishedUpdate.ItemId,
                            speech.Duration,
                            speech.Wave);
                        if (string.IsNullOrEmpty(instruction))
                            continue;
                        instructions.Append(instruction);
                        instructions.Append(' ');
                    }

                    await Respond(new ConversationResponseOptions()
                    {
                        Instructions = instructions.ToString()
                    }, stoppingToken);
                    break;
                
                case ConversationInputTranscriptionFinishedUpdate transcriptionFinishedUpdate:
                    logger.LogInformation("User Transcription: {Transcript}", transcriptionFinishedUpdate.Transcript);
                    foreach (var context in _contexts)
                        await context.OnUserSpeechTranscript(
                            transcriptionFinishedUpdate.ItemId,
                            transcriptionFinishedUpdate.Transcript);
                    break;

                case ConversationItemStreamingStartedUpdate itemStartedUpdate:
                    logger.LogInformation("Response streaming started.");
                    audioOutput.Clear();
                    foreach (var context in _contexts)
                        await context.OnAgentResponseBegin(
                            itemStartedUpdate.ItemId,
                            itemStartedUpdate.ResponseId);
                    break;

                case ConversationItemStreamingPartDeltaUpdate { AudioBytes: not null } deltaUpdate:
                    audioOutput.Write(deltaUpdate.AudioBytes.ToMemory());
                    break;

                case ConversationItemStreamingFinishedUpdate itemFinishedUpdate:
                    _responsing = false;
                    
                    logger.LogInformation("Response streaming finished.");

                    foreach (var context in _contexts)
                        await context.OnAgentResponseEnd(
                            itemFinishedUpdate.ItemId,
                            itemFinishedUpdate.ResponseId,
                            itemFinishedUpdate.MessageContentParts);
                    if (itemFinishedUpdate.FunctionName != null)
                    {
                        logger.LogInformation("Agent invokes function {ToolName}.", itemFinishedUpdate.FunctionName);
                        if (itemFinishedUpdate.FunctionName == "session-end_conversation")
                        {
                            logger.LogInformation("Agent terminates this conversation.");
                            terminating = true;
                            break;
                        }

                        if (_tools.TryGetValue(itemFinishedUpdate.FunctionName, out var tool))
                        {
                            var functionCallResult = "";
                            try
                            {
                                functionCallResult = await tool.Handle(
                                    itemFinishedUpdate.FunctionCallArguments);
                            }
                            catch (Exception error)
                            {
                                functionCallResult = $"An error occurred: {error.Message}";
                            }

                            await _session.AddItemAsync(ConversationItem.CreateFunctionCallOutput(
                                itemFinishedUpdate.FunctionCallId, functionCallResult
                            ), stoppingToken);
                            await Respond(null, stoppingToken);
                        }
                        else
                        {
                            logger.LogError("Cannot find invoked took {ToolName}.", itemFinishedUpdate.FunctionName);
                        }
                    }

                    break;

                case ConversationItemStreamingAudioTranscriptionFinishedUpdate audioTranscriptionUpdate:
                    logger.LogInformation("Agent Transcription: {Transcript}", audioTranscriptionUpdate.Transcript);
                    foreach (var context in _contexts)
                        await context.OnUserSpeechTranscript(
                            audioTranscriptionUpdate.ItemId,
                            audioTranscriptionUpdate.Transcript);
                    break;

                case ConversationErrorUpdate errorUpdate:
                    logger.LogError("Error: {Message}", errorUpdate.Message);
                    terminating = true;
                    break;
            }

            if (NeedReconfigure)
            {
                await ConfigureAgent(null, stoppingToken);
                NeedReconfigure = false;
            }

            if (terminating)
                break;
        }

        logger.LogInformation("Updates have all been received.");

        await jobAudioSender.StopAsync(stoppingToken);
    }

    private class AudioSenderJob(
        RealtimeConversationSession session,
        RecorderWrapperProvider provider,
        ILogger<AgentSession> logger) :
        BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Audio sender job has been started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await session.SendInputAudioAsync(provider, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception error)
                {
                    logger.LogError(error, "Error occurred while sending audio to the service.");
                }
            }

            logger.LogInformation("Audio sender job has been stopped.");
        }
    }
}

public static class AgentSessionExtensions
{
    public static AgentSession NewAgentSession(this IServiceProvider provider,
        IAgentAudioInput input, IAgentAudioOutput output)
    {
        return new AgentSession(
            input, output,
            provider.GetRequiredService<ILogger<AgentSession>>(),
            provider.GetRequiredService<RealtimeConversationClient>(),
            provider.GetRequiredService<SpeechConfig>());
    }
}