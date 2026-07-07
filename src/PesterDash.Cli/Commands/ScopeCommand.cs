using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PesterDash.Core.Interfaces;
using PesterDash.Cli.Services;
using PesterDash.Cli.Views;
using PesterDash.Core.Configuration;
using PesterDash.Core.Discovery;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class ScopeCommand
{
    public static Command Create(IServiceProvider services)
    {
        var projectRoot = new Argument<string>("project-root")
        {
            Description = "Root directory of the PowerShell project.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var command = new Command("scope", "Select test and source files for runs (saved to pesterdash.json).")
        {
            projectRoot,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(projectRoot)!;
            var context = ServiceCollectionExtensions.CreateProjectContext(path);

            var discovery = services.GetRequiredService<IProjectDiscovery>();
            var discoveryResult = discovery.Discover(context.ProjectRoot, context.Options);

            if (!discoveryResult.HasTests)
            {
                AnsiConsole.MarkupLine(
                    "[red]No test candidates found.[/] Look for [grey]*.Tests.ps1[/] or [grey].ps1[/] files under [grey]tests/[/].");
                return 1;
            }

            if (Console.IsInputRedirected)
            {
                AnsiConsole.MarkupLine("[red]Scope selection requires an interactive terminal.[/]");
                return 1;
            }

            var selectedScope = await ScopeSelectorView.SelectAsync(
                discoveryResult,
                context.Options.RunScope,
                cancellationToken);

            if (selectedScope is null)
            {
                AnsiConsole.MarkupLine("[yellow]Scope selection cancelled.[/]");
                return 130;
            }

            context.Options.RunScope = selectedScope;
            ConfigurationLoader.Save(context.ProjectRoot, context.Options);

            var scoped = RunScopeApplicator.Apply(discoveryResult, context.Options);
            AnsiConsole.MarkupLine(
                $"[green]Saved run scope[/] to [grey]{Markup.Escape(ConfigurationLoader.GetProjectConfigPath(context.ProjectRoot))}[/]");
            AnsiConsole.MarkupLine(
                $"[grey]Tests:[/] {scoped.TestFiles.Count}  [grey]Sources:[/] {scoped.SourceFiles.Count}");

            return 0;
        });

        return command;
    }
}
