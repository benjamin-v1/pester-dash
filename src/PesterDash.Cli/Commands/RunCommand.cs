using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Cli.Services;
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

        var noAnalyser = new Option<bool>("--no-analyser")
        {
            Description = "Disable PSScriptAnalyzer on scoped source files.",
        };

        var outputDir = new Option<string?>("--output", "-o")
        {
            Description = "Output directory for artefacts (overrides config).",
        };

        var ci = new Option<bool>("--ci")
        {
            Description = "CI mode: no interactive dashboard, exit code reflects failures.",
        };

        var debug = new Option<bool>("--debug")
        {
            Description = "Write full Pester stdout/stderr and run metadata to .pester-dash/logs/pester-debug.log.",
        };

        var scope = new Option<bool>("--scope")
        {
            Description = "Open the run scope picker before running tests.",
        };

        var command = new Command("run", "Discover and run Pester tests once.")
        {
            projectRoot,
            noCoverage,
            noAnalyser,
            outputDir,
            ci,
            debug,
            scope,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var overrides = new Core.Configuration.PesterDashOptions
            {
                Coverage = !parseResult.GetValue(noCoverage),
                Analyser = !parseResult.GetValue(noAnalyser),
                Debug = parseResult.GetValue(debug),
            };

            if (!string.IsNullOrWhiteSpace(parseResult.GetValue(outputDir)))
            {
                overrides.OutputDirectory = parseResult.GetValue(outputDir)!;
            }

            var ciMode = parseResult.GetValue(ci);
            var forceScope = parseResult.GetValue(scope);

            try
            {
                var workflow = services.GetRequiredService<AppWorkflow>();
                var (context, discovery) = await workflow.PrepareProjectAsync(path, overrides, cancellationToken)
                    .ConfigureAwait(false);

                if (ciMode)
                {
                    CommandOutput.WriteBanner();
                    CommandOutput.WriteProjectContext(context);
                }

                var (scopedDiscovery, scopeExitCode) = await RunScopeWorkflow.ResolveAsync(
                    context,
                    discovery,
                    interactive: !ciMode,
                    forceScopePrompt: forceScope,
                    cancellationToken).ConfigureAwait(false);

                if (scopedDiscovery is null)
                {
                    return scopeExitCode;
                }

                if (ciMode)
                {
                    DiscoveryOutput.Write(scopedDiscovery);
                }

                if (!scopedDiscovery.HasTests)
                {
                    AnsiConsole.MarkupLine("[red]No test files in scope.[/] Run [grey]pesterdash scope[/] to update selection.");
                    return 1;
                }

                DashboardData? dashboardData = null;
                var exitCode = 0;

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Running Pester tests...", async ctx =>
                    {
                        var progress = new Progress<string>(line =>
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                ctx.Status(Markup.Escape(line));
                            }
                        });

                        (exitCode, dashboardData) = await workflow.RunTestsAsync(
                            context,
                            scopedDiscovery,
                            filter: null,
                            progress,
                            priorResults: null,
                            cancellationToken).ConfigureAwait(false);
                    });

                if (dashboardData is null)
                {
                    if (exitCode == 130)
                    {
                        AnsiConsole.MarkupLine("[yellow]Test run cancelled.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]Pester did not produce test results.[/]");
                    }

                    return exitCode;
                }

                if (ciMode)
                {
                    if (dashboardData.TestRun.Total == 0 && dashboardData.RunOutput is not null)
                    {
                        AnsiConsole.MarkupLine("[red]Pester could not complete.[/]");
                        AnsiConsole.WriteLine(dashboardData.RunOutput);
                    }
                    else
                    {
                        RunResultOutput.Write(dashboardData.TestRun);

                        if (dashboardData.Coverage is not null)
                        {
                            CoverageOutput.Write(dashboardData.Coverage);
                        }
                    }

                    if (context.Options.Debug && dashboardData.DebugLogPath is not null)
                    {
                        AnsiConsole.MarkupLine($"[grey]Debug log:[/] {Markup.Escape(dashboardData.DebugLogPath)}");
                    }

                    return exitCode;
                }

                if (context.Options.Debug && dashboardData.DebugLogPath is not null)
                {
                    AnsiConsole.MarkupLine($"[grey]Debug log:[/] {Markup.Escape(dashboardData.DebugLogPath)}");
                }

                var session = workflow.CreateSession(
                    context,
                    discovery,
                    dashboardData.LastFilter,
                    dashboardData.TestRun,
                    dashboardData);
                var dashboard = services.GetRequiredService<IDashboard>();
                return await dashboard.ShowAsync(dashboardData, session, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
        });

        return command;
    }
}
