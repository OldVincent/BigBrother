using OpenAI.Chat;

namespace BigBrother.Contexts;

public class TriggerContext(ChatClient chatClient) : AgentContext
{
    public override async Task OnUserSpeechBegin(TimeSpan startingTime, string itemId)
    {
        
    }
}