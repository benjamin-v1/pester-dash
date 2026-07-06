using System.CommandLine;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class CleanCommand
{
    public static Command Create(IServiceProvider services)
    {
        var projectRoot = new Argument<string>("project-root")
        {
            Description = "Root directory of the PowerShell project.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var command = new Command("clean", "Delete temporary test artefacts.")
        {
            projectRoot,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var context = ServiceCollectionExtensions.CreateProjectContext(path);

            CommandOutput.WriteBanner();
            CommandOutput.WriteProjectContext(context);

            if (Directory.Exists(context.OutputDirectory))
            {
                Directory.Delete(context.OutputDirectory, recursive: true);
                AnsiConsole.MarkupLine($"[green]Deleted[/] {Markup.Escape(context.OutputDirectory)}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[grey]Nothing to clean — directory does not exist.[/]");
            }

            await Task.CompletedTask;
            return 0;
        });

        return command;
    }
}
