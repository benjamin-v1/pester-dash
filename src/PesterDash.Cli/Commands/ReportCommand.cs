using System.CommandLine;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class ReportCommand
{
    public static Command Create(IServiceProvider services)
    {
        var projectRoot = new Argument<string>("project-root")
        {
            Description = "Root directory of the PowerShell project.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var command = new Command("report", "Generate HTML reports without the interactive dashboard.")
        {
            projectRoot,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var context = ServiceCollectionExtensions.CreateProjectContext(path);

            CommandOutput.WriteBanner();
            CommandOutput.WriteProjectContext(context);
            AnsiConsole.MarkupLine("[grey]Report command scaffold ready — report generation not yet implemented.[/]");

            await Task.CompletedTask;
            return 0;
        });

        return command;
    }
}
