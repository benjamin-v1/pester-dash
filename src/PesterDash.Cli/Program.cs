using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PesterDash.Cli;
using PesterDash.Cli.Commands;
using Spectre.Console;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPesterDash();

using var host = builder.Build();

var rootCommand = new RootCommand("Discover, run, and visualize Pester tests.")
{
    OpenCommand.Create(host.Services),
    RunCommand.Create(host.Services),
    ScopeCommand.Create(host.Services),
    WatchCommand.Create(host.Services),
    ReportCommand.Create(host.Services),
    CleanCommand.Create(host.Services),
};

try
{
    var parseResult = rootCommand.Parse(args);
    return await parseResult.InvokeAsync();
}
catch (DirectoryNotFoundException ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
    return 1;
}
