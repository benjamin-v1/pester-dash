using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using PesterDash.Core.Models;
using PesterDash.Cli.Views;
using Spectre.Console;

namespace PesterDash.Cli.Services;

/// <summary>Resolves and persists interactive run scope selection.</summary>
internal static class RunScopeWorkflow
{
    /// <summary>
    /// Ensures run scope is configured, optionally prompting the user, then applies it to discovery.
    /// </summary>
    public static async Task<(DiscoveryResult? ScopedDiscovery, int ExitCode)> ResolveAsync(
        ProjectContext context,
        DiscoveryResult discovery,
        bool interactive,
        bool forceScopePrompt,
        CancellationToken cancellationToken)
    {
        if (!discovery.HasTests)
        {
            AnsiConsole.MarkupLine(
                "[red]No test candidates found.[/] Look for [grey]*.Tests.ps1[/] or [grey].ps1[/] files under [grey]tests/[/].");
            return (null, 1);
        }

        var needsPrompt = forceScopePrompt
            || (interactive && !context.Options.RunScope.IsConfigured);

        if (needsPrompt)
        {
            if (!interactive)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Run scope is not configured.[/] Run [grey]pesterdash scope <project>[/] or use interactive mode.");
                return (ApplyScope(context, discovery), 0);
            }

            AnsiConsole.MarkupLine("[grey]Select which test and source files to include. Saved to .pester-dash/config.json.[/]");
            var selectedScope = await ScopeSelectorView.SelectAsync(
                discovery,
                context.Options.RunScope,
                cancellationToken);

            if (selectedScope is null)
            {
                AnsiConsole.MarkupLine("[yellow]Scope selection cancelled.[/]");
                return (null, 130);
            }

            context.Options.RunScope = selectedScope;
            ConfigurationLoader.Save(context.ProjectRoot, context.Options);
            AnsiConsole.MarkupLine(
                $"[green]Saved run scope[/] to [grey]{Markup.Escape(ConfigurationLoader.GetProjectConfigPath(context.ProjectRoot))}[/]");
        }

        var scoped = ApplyScope(context, discovery);

        if (!scoped.HasTests)
        {
            AnsiConsole.MarkupLine(
                "[red]No test files in scope.[/] Run [grey]pesterdash scope {path}[/] to update selection."
                    .Replace("{path}", Markup.Escape(context.ProjectRoot)));
            return (null, 1);
        }

        return (scoped, 0);
    }

    public static DiscoveryResult ApplyScope(ProjectContext context, DiscoveryResult discovery) =>
        RunScopeApplicator.Apply(discovery, context.Options);
}
