using System.Text;
using BigBrother.Services;
using BigBrother.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using OpenAI.RealtimeConversation;

#pragma warning disable OPENAI002

namespace BigBrother.Contexts;

public class ActionContext(
    ILogger<ActionContext> logger,
    ActionManagementService actionManagementService) : AgentContext
{
    public override IEnumerable<AgentTool> Tools { get; protected set; } =
    [
        new InvokeActionTool(logger)
    ];

    public override async Task<string> OnUserSpeechEnd(TimeSpan endingTime, string itemId, TimeSpan speechDuration,
        MemoryStream speechWave)
    {
        var roles = new HashSet<string>();
        var user = Session.SearchContext<UserContext>()?.CurrentUser;
        if (user != null)
            roles.UnionWith(user.Roles);

        var actions = actionManagementService.FilterActions(roles);

        var instructions = new StringBuilder();

        await foreach (var action in actions)
        {
            if (instructions.Length == 0)
            {
                instructions.AppendLine("Here is the list of actions that this user can perform;" +
                                        "however, some actions have special conditions that need to be met," +
                                        "otherwise the user cannot perform the action.");
            }

            instructions.AppendLine($" - {action.Description}: {action.Content}");
            if (action.Conditions.Count <= 0)
                continue;
            foreach (var condition in action.Conditions)
            {
                instructions.AppendLine($" ↳ Conditions - {condition}");
            }
        }

        return instructions.ToString();
    }
}

public static class ActionContextExtensions
{
    public static AgentSession AddActionContext(this AgentSession session, IServiceProvider provider)
    {
        session.AddContext(new ActionContext(
            provider.GetRequiredService<ILogger<ActionContext>>(),
            provider.GetRequiredService<ActionManagementService>()
        ));
        return session;
    }
}