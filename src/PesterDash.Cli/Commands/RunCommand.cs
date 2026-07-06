using System.CommandLine;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class RunCommand
{
    public static Command Create()
    {
        var projectRoot = new Argument<string>("project-root")
        {
            Description = "Root directory of the PowerShell project.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var noCoverage = new Option<bool>("--no-coverage")
        {
            Description = "Disable code coverage collection.",
        };

        var outputDir = new Option<string?>("--output", "-o")
        {
            Description = "Output directory for artefacts (overrides config).",
        };

        var ci = new Option<bool>("--ci")
        {
            Description = "CI mode: no interactive dashboard, exit code reflects failures.",
        };

        var command = new Command("run", "Discover and run Pester tests once.")
        {
            projectRoot,
            noCoverage,
            outputDir,
            ci,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var context = ServiceCollectionExtensions.CreateProjectContext(
                path,
                new()
                {
                    Coverage = !parseResult.GetValue(noCoverage),
                    OutputDirectory = parseResult.GetValue(outputDir) ?? ".artifacts",
                });

            CommandOutput.WriteBanner();
            CommandOutput.WriteProjectContext(context);

            var ciMode = parseResult.GetValue(ci);
            AnsiConsole.MarkupLine(ciMode
                ? "[yellow]CI mode[/] — interactive dashboard will be skipped."
                : "[grey]Run command scaffold ready — test execution not yet implemented.[/]");

            await Task.CompletedTask;
            return 0;
        });

        return command;
    }
}
