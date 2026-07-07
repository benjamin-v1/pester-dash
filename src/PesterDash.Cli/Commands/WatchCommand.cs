using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Interfaces;
using PesterDash.Cli.Services;
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

        var command = new Command("watch", "Open the dashboard and rerun tests when scoped files change.")
        {
            projectRoot,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;

            try
            {
                var workflow = services.GetRequiredService<AppWorkflow>();
                var (context, discovery) = await workflow.PrepareProjectAsync(path, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

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
                    StatusMessage = dashboardData.StatusMessage ?? "Watch mode on. Press F5 to run tests.",
                    LastFilterDescription = dashboardData.LastFilterDescription,
                    LastFilter = dashboardData.LastFilter,
                    IsWatching = true,
                };

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
