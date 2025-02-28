using BigBrother.Contexts;
using BigBrother.Devices;

namespace BigBrother.Commands;

public static class RunAgentCommand
{
    public static async Task Run(IServiceProvider provider)
    {
        var microphone = new MicrophoneDevice();
        var speaker = new SpeakerDevice();

        // var keywordRecognizer = new KeywordRecognizer(AudioConfig.FromDefaultMicrophoneInput());
        // var keywordModel = KeywordRecognitionModel.FromFile(@"C:\Users\jia_v\Desktop\BigBrother-KeyWord.table");
        // var result = await keywordRecognizer.RecognizeOnceAsync(keywordModel);

        var agent = provider.NewAgentSession(microphone, speaker);
        
        agent.AddUserContext(provider);
        agent.AddActionContext(provider);

        microphone.Start();
        
        await agent.StartAsync(CancellationToken.None);

        await agent.ExecuteTask!;
        
        microphone.Stop();
    }
}