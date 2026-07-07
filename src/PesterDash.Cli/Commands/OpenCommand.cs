using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Interfaces;
using PesterDash.Cli.Services;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class OpenCommand
{
    public static Command Create(IServiceProvider services)
    {
        var projectRoot = new Argument<string>("project-root")
        {
            Description = "Root directory of the PowerShell project.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var watch = new Option<bool>("--watch", "-w")
        {
            Description = "Start in watch mode.",
        };

        var command = new Command("open", "Open the dashboard without running tests.")
        {
            projectRoot,
            watch,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var startWatch = parseResult.GetValue(watch);

            try
            {
                var workflow = services.GetRequiredService<AppWorkflow>();
                var (context, discovery) = await workflow.PrepareProjectAsync(path, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (!discovery.HasTests)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]No test candidates found.[/] Configure scope with [grey]pesterdash scope[/] when tests are added.");
                }

                var scopeData = await workflow.ResolveScopeInteractivelyAsync(
                    context,
                    discovery,
                    forcePrompt: false,
                    cancellationToken).ConfigureAwait(false);

                var dashboardData = scopeData
                    ?? await workflow.LoadLastRunAsync(context, cancellationToken).ConfigureAwait(false)
                    ?? AppWorkflow.CreateEmptyDashboard(context);

                var session = workflow.CreateSession(
                    context,
                    discovery,
                    dashboardData.LastFilter,
                    dashboardData.TestRun,
                    dashboardData);

                if (startWatch)
                {
                    await session.ToggleWatchAsync(cancellationToken).ConfigureAwait(false);
                    dashboardData = new Core.Models.DashboardData
                    {
                        Context = dashboardData.Context,
                        TestRun = dashboardData.TestRun,
                        Coverage = dashboardData.Coverage,
                        Analyser = dashboardData.Analyser,
                        RunOutput = dashboardData.RunOutput,
                        RunLogPath = dashboardData.RunLogPath,
                        DebugLogPath = dashboardData.DebugLogPath,
                        ProcessStderr = dashboardData.ProcessStderr,
                        StatusMessage = dashboardData.StatusMessage,
                        LastFilterDescription = dashboardData.LastFilterDescription,
                        LastFilter = dashboardData.LastFilter,
                        IsWatching = true,
                    };
                }

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
