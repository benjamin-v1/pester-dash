using PesterDash.Core.Models;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class CommandOutput
{
    public static void WriteBanner()
    {
        AnsiConsole.Write(new FigletText("PesterDash").Color(Color.Cyan1));
    }

    public static void WriteProjectContext(ProjectContext context)
    {
        AnsiConsole.MarkupLine($"[grey]Project:[/] {Markup.Escape(context.ProjectRoot)}");
        AnsiConsole.MarkupLine($"[grey]Output:[/]  {Markup.Escape(context.OutputDirectory)}");
        AnsiConsole.MarkupLine($"[grey]Coverage:[/] {(context.Options.Coverage ? "[green]on[/]" : "[yellow]off[/]")}");
    }
}
