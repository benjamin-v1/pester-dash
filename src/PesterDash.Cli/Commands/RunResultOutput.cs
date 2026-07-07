using PesterDash.Core.Models;
using Spectre.Console;

namespace PesterDash.Cli.Commands;

internal static class RunResultOutput
{
    public static void Write(TestRunResult result)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Total", result.Total.ToString());
        table.AddRow("Passed", $"[green]{result.Passed}[/]");
        table.AddRow("Failed", result.Failed > 0 ? $"[red]{result.Failed}[/]" : "0");
        table.AddRow("Skipped", result.Skipped.ToString());
        table.AddRow("Pass rate", $"{result.PassRate:F1}%");
        table.AddRow("Duration", result.Duration.ToString(@"hh\:mm\:ss\.fff"));

        AnsiConsole.Write(table);

        if (result.FailedTests.Count > 0)
        {
            AnsiConsole.WriteLine();
            var failures = new Table()
                .Border(TableBorder.Rounded)
                .Title("[red]Failed tests[/]")
                .AddColumn("Test")
                .AddColumn("Error");

            foreach (var failed in result.FailedTests)
            {
                var error = failed.ErrorMessage ?? "(no message)";
                if (error.Length > 120)
                {
                    error = error[..117] + "...";
                }

                failures.AddRow(Markup.Escape(failed.Name), Markup.Escape(error));
            }

            AnsiConsole.Write(failures);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Results:[/] {Markup.Escape(result.ResultsFile)}");
    }
}
