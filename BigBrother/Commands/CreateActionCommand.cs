using BigBrother.Services;
using Spectre.Console;

namespace BigBrother.Commands;

public static class CreateActionCommand
{
    public static async Task Run(ActionManagementService service)
    {
        var document = new ActionDocument();
        
        document.Description = AnsiConsole.Ask<string>("Description of this action:");
        document.Content = AnsiConsole.Ask<string>("Content of this action:");
        document.AllowedRoles = AnsiConsole.Ask<string>("Roles allowed to perform this action (Separated by ','):")
            .Split(',')
            .Select(role => role.Trim())
            .ToHashSet();
        document.DisallowedRoles = AnsiConsole.Ask<string>("Roles disallowed to perform this action (Separated by ','):")
            .Split(',')
            .Select(role => role.Trim())
            .ToHashSet();
        while (true)
        {
            var condition = AnsiConsole.Ask<string>("Condition for this action (empty to finish):");
            document.Conditions.Add(condition);
            if (! await AnsiConsole.ConfirmAsync("Add another condition?"))
            {
                break;
            }
        }

        await service.CreateAction(document);
    }
}