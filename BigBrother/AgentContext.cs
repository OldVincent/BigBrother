using OpenAI.RealtimeConversation;

#pragma warning disable OPENAI002

namespace BigBrother;

public abstract class AgentContext
{
    public virtual IEnumerable<AgentTool> Tools { get; protected set; } = [];

    public virtual string Instructions { get; } = "";

    public AgentSession Session { get; internal set; } = null!;

    protected void ConfigureAgent()
    {
        Session.NeedReconfigure = true;
    }

    public virtual Task OnBuild()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnSessionBegin()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnSessionEnd()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnUserSpeechBegin(
        TimeSpan startingTime, string itemId)
    {
        return Task.CompletedTask;
    }

    public virtual Task<string> OnUserSpeechEnd(
        TimeSpan endingTime, string itemId,
        TimeSpan speechDuration, MemoryStream speechWave)
    {
        return Task.FromResult("");
    }

    public virtual Task OnUserSpeechTranscript(string itemId, string transcript)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnAgentResponseBegin(string itemId, string responseId)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnAgentResponseEnd(
        string itemId, string responseId, IReadOnlyList<ConversationContentPart>? parts)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnAgentSpeechTranscript(string itemId, string transcript)
    {
        return Task.CompletedTask;
    }
}