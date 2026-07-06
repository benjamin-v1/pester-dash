using System.CommandLine;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class WatchCommand
{
    public static Command Create(IServiceProvider services)
    {
        var projectRoot = new Argument<string>("project-root")
        {
            Description = "Root directory of the PowerShell project.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var command = new Command("watch", "Watch for file changes and rerun tests.")
        {
            projectRoot,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var context = ServiceCollectionExtensions.CreateProjectContext(path);

            CommandOutput.WriteBanner();
            CommandOutput.WriteProjectContext(context);
            AnsiConsole.MarkupLine("[grey]Watch command scaffold ready — file watching not yet implemented.[/]");

            await Task.CompletedTask;
            return 0;
        });

        return command;
    }
}
