using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Interfaces;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class RunCommand
{
    public static Command Create(IServiceProvider services)
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

            var discovery = services.GetRequiredService<IProjectDiscovery>();
            var result = discovery.Discover(context.ProjectRoot, context.Options);

            DiscoveryOutput.Write(result);

            if (!result.HasTests)
            {
                AnsiConsole.MarkupLine("[red]No test files found.[/] Look for [grey]*.Tests.ps1[/] or files under [grey]tests/[/].");
                return 1;
            }

            var ciMode = parseResult.GetValue(ci);
            AnsiConsole.MarkupLine(ciMode
                ? "[yellow]CI mode[/] — test execution not yet implemented."
                : "[grey]Discovery complete — test execution not yet implemented.[/]");

            await Task.CompletedTask;
            return 0;
        });

        return command;
    }
}
