using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class DiscoveryOutput
{
    public static void Write(DiscoveryResult result)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Count");

        table.AddRow("Files scanned", result.TotalFilesScanned.ToString());
        table.AddRow("Test files", $"[green]{result.TestFiles.Count}[/]");
        table.AddRow("Source files", result.SourceFiles.Count.ToString());

        AnsiConsole.Write(table);

        if (result.TestFiles.Count > 0)
        {
            AnsiConsole.WriteLine();
            var tree = new Tree("[bold]Test files[/]");

            foreach (var testFile in result.TestFiles)
            {
                var relative = Path.GetRelativePath(result.ProjectRoot, testFile);
                tree.AddNode(Markup.Escape(relative));
            }

            AnsiConsole.Write(tree);
        }
    }
}
