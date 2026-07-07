using PesterDash.Core.Formatting;
using PesterDash.Core.Models;
using PesterDash.Cli.Utilities;
using Spectre.Console;

namespace PesterDash.Cli.Views;

internal static class DashboardRenderer
{
    public static void Render(DashboardState state)
    {
        AnsiConsole.Clear();
        RenderAppHeader(state);

        switch (state.Mode)
        {
            case DashboardMode.Help:
                RenderHelp();
                RenderFlash(state);
                return;
            case DashboardMode.Detail when state.DetailAnalyserFinding is { } finding:
                RenderAnalyserDetail(state, finding);
                RenderFooter("Enter/Esc back  c copy  v open file  q quit");
                RenderFlash(state);
                return;
            case DashboardMode.Detail when state.DetailTest is { } test:
                RenderDetail(state, test);
                RenderFooter("Enter/Esc back  c copy  v open file  q quit");
                RenderFlash(state);
                return;
            case DashboardMode.CoverageDetail when state.CoverageDetailFile is { } file:
                RenderCoverageDetail(state, file);
                RenderFlash(state);
                return;
            case DashboardMode.RunOutput:
                RenderRunOutput(state);
                RenderFlash(state);
                return;
        }

        RenderNavigate(state);
        RenderFlash(state);
    }

    private static void RenderAppHeader(DashboardState state)
    {
        var width = Math.Max(40, Console.WindowWidth - 2);
        var watchBadge = state.IsWatching ? " [green]WATCH[/]" : string.Empty;
        AnsiConsole.MarkupLine(
            $"[bold cyan] PesterDash [/] [grey]│[/] [white]{Markup.Escape(state.ProjectDisplayName)}[/]{watchBadge}");
        AnsiConsole.MarkupLine("[grey]" + new string('─', width) + "[/]");

        if (!string.IsNullOrWhiteSpace(state.Data.StatusMessage))
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(state.Data.StatusMessage)}[/]");
        }
        else if (!string.IsNullOrWhiteSpace(state.Data.LastFilterDescription))
        {
            AnsiConsole.MarkupLine(
                $"[grey]Last run:[/] {Markup.Escape(state.Data.LastFilterDescription)}  [grey]F5 run all  r rerun  x run selection  s scope  w watch[/]");
        }
    }

    private static void RenderNavigate(DashboardState state)
    {
        RenderProjectSummary(state);
        RenderStderrWarning(state);
        RenderTabBar(state);
        RenderViewSummary(state);
        RenderListHeader(state);

        if (state.PrimaryView == DashboardPrimaryView.Tests)
        {
            RenderTestsTable(state);
        }
        else if (state.PrimaryView == DashboardPrimaryView.Coverage)
        {
            RenderCoverageTable(state);
        }
        else
        {
            RenderAnalyserTable(state);
        }

        AnsiConsole.WriteLine();
        RenderFooter(BuildFooter(state));
    }

    private static void RenderProjectSummary(DashboardState state)
    {
        var run = state.Data.TestRun;
        var coverage = CoverageScopeSummary.FromResult(state.Data.Coverage?.Metrics);

        var testsPanel = new Panel(BuildRunTestsContent(run))
            .Header("[bold]Tests[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);

        var coveragePanel = new Panel(BuildRunCoverageContent(coverage, state.Data.Coverage))
            .Header("[bold]Coverage[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);

        var analyserPanel = new Panel(BuildRunAnalyserContent(state.Data.Analyser))
            .Header("[bold]Analyser[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(testsPanel, coveragePanel, analyserPanel);

        var wrapper = new Panel(grid)
            .Header("[bold]Project summary[/]  [grey]· entire test run[/]")
            .Border(BoxBorder.Double)
            .Padding(0, 0);

        AnsiConsole.Write(wrapper);
    }

    private static void RenderStderrWarning(DashboardState state)
    {
        if (!state.Data.HasProcessStderr)
        {
            return;
        }

        AnsiConsole.WriteLine();
        var summary = RunLogFormatter.GetStderrSummary(state.Data.ProcessStderr);
        var lines = new List<string>
        {
            "[yellow]Pester wrote errors to stderr during the run.[/]",
            "[grey]These may explain failures that look unclear in individual tests.[/]",
        };

        if (!string.IsNullOrWhiteSpace(summary))
        {
            lines.Add($"[red]{Markup.Escape(summary)}[/]");
        }

        lines.Add("[grey]Press [bold]l[/] for full stdout/stderr.[/]");

        if (!string.IsNullOrWhiteSpace(state.Data.DebugLogPath))
        {
            lines.Add($"[grey]Debug log:[/] {Markup.Escape(state.Data.DebugLogPath)}");
        }

        var panel = new Panel(string.Join(Environment.NewLine, lines))
            .Header("[bold yellow]Run warnings[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }

    private static string BuildRunTestsContent(TestRunResult run)
    {
        if (run.Total == 0)
        {
            return "[grey]No test results were produced.[/]";
        }

        var passBar = BuildBar(run.PassRate, "green");
        return string.Join(Environment.NewLine,
        [
            $"[green]{run.Passed}[/] passed   [red]{run.Failed}[/] failed   [yellow]{run.Skipped}[/] skipped",
            $"[bold]{run.PassRate:F1}%[/] pass rate   {run.Total} total   {FormatDuration(run.Duration)}",
            passBar,
        ]);
    }

    private static string BuildRunCoverageContent(CoverageScopeSummary coverage, CoverageReportResult? report)
    {
        if (report?.Metrics is null)
        {
            return report?.Warning is { } warning
                ? $"[yellow]{Markup.Escape(warning)}[/]"
                : "[grey]Coverage not collected[/]";
        }

        var lineBar = BuildBar(coverage.LinePercent, CoverageColor(coverage.LinePercent));
        var lines = new List<string>
        {
            $"[bold]{coverage.LinePercent:F1}%[/] line   {coverage.CoveredLines}/{coverage.TotalLines} lines",
            lineBar,
        };

        if (coverage.HasBranchCoverage)
        {
            lines.Add(
                $"[bold]{coverage.BranchPercent:F1}%[/] branch   {coverage.CoveredBranches}/{coverage.TotalBranches}");
        }

        lines.Add($"[grey]{coverage.FileCount} files[/]");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRunAnalyserContent(AnalyserResult? analyser)
    {
        if (analyser is null)
        {
            return "[grey]Analyser not run[/]";
        }

        if (!string.IsNullOrWhiteSpace(analyser.WarningMessage) && analyser.Findings.Count == 0)
        {
            var summary = RunLogFormatter.GetStderrSummary(analyser.WarningMessage) ?? analyser.WarningMessage;
            return $"[yellow]{Markup.Escape(summary)}[/]\n[grey]Press l for full run log if needed.[/]";
        }

        return string.Join(Environment.NewLine,
        [
            $"[red]{analyser.ErrorCount}[/] errors   [yellow]{analyser.WarningCount}[/] warnings   [grey]{analyser.InformationCount}[/] info",
            $"[grey]{analyser.Findings.Count} findings[/]",
        ]);
    }

    private static void RenderTabBar(DashboardState state)
    {
        AnsiConsole.WriteLine();
        var testsTab = state.PrimaryView == DashboardPrimaryView.Tests
            ? "[bold white on blue]  Tests  [/]"
            : "[grey]  Tests  [/]";
        var coverageTab = state.PrimaryView == DashboardPrimaryView.Coverage
            ? "[bold white on blue]  Coverage  [/]"
            : "[grey]  Coverage  [/]";
        var analyserTab = state.PrimaryView == DashboardPrimaryView.Analyser
            ? "[bold white on blue]  Analyser  [/]"
            : "[grey]  Analyser  [/]";

        AnsiConsole.MarkupLine($"[grey]View[/]  {testsTab}  {coverageTab}  {analyserTab}  [grey](Tab)[/]");
        AnsiConsole.MarkupLine("[grey]" + new string('─', Math.Max(20, state.ContentWidth)) + "[/]");
    }

    private static void RenderViewSummary(DashboardState state)
    {
        if (state.PrimaryView == DashboardPrimaryView.Tests)
        {
            var scope = state.CurrentTestScope;
            var nav = state.Navigator;
            var breadcrumb = nav.Path.Count == 0 ? "All test files" : Markup.Escape(nav.Breadcrumb);
            var passBar = BuildBar(scope.PassRate, scope.Failed > 0 ? "yellow" : "green");

            var content = string.Join(Environment.NewLine,
            [
                $"[grey]{breadcrumb}[/]",
                $"[green]{scope.Passed}[/] passed   [red]{scope.Failed}[/] failed   [yellow]{scope.Skipped}[/] skipped",
                $"[bold]{scope.PassRate:F1}%[/] pass rate   {scope.Total} total   {FormatDuration(scope.Duration)}",
                passBar,
            ]);

            var panel = new Panel(content)
                .Header("[bold]Current view · Tests[/]")
                .Border(BoxBorder.Rounded)
                .Padding(1, 0);

            AnsiConsole.Write(panel);
            return;
        }

        if (state.PrimaryView == DashboardPrimaryView.Analyser)
        {
            var analyser = state.Data.Analyser;
            var content = analyser is null
                ? "[grey]PSScriptAnalyzer has not been run yet.[/]\n[grey]Press F5 to run tests and analyser, or a for analyser only.[/]"
                : BuildRunAnalyserContent(analyser);

            var analyserPanel = new Panel(content)
                .Header("[bold]Current view · Analyser[/]")
                .Border(BoxBorder.Rounded)
                .Padding(1, 0);

            AnsiConsole.Write(analyserPanel);
            return;
        }

        var coverageScope = state.CurrentCoverageScope;
        var coverageContent = BuildScopeCoverageContent(coverageScope);
        var coveragePanel = new Panel(coverageContent)
            .Header("[bold]Current view · Coverage[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);

        AnsiConsole.Write(coveragePanel);
    }

    private static string BuildScopeCoverageContent(CoverageScopeSummary scope)
    {
        if (scope.TotalLines == 0)
        {
            return $"[grey]{Markup.Escape(scope.Title)}[/]\n[grey]No coverage data for this view[/]";
        }

        var color = CoverageColor(scope.LinePercent);
        var lineBar = BuildBar(scope.LinePercent, color);
        var lines = new List<string>
        {
            $"[grey]{Markup.Escape(scope.Title)}[/]",
            $"[bold]{scope.LinePercent:F1}%[/] line   {scope.CoveredLines}/{scope.TotalLines} lines",
            lineBar,
        };

        if (scope.HasBranchCoverage)
        {
            lines.Add(
                $"[bold]{scope.BranchPercent:F1}%[/] branch   {scope.CoveredBranches}/{scope.TotalBranches}");
        }

        lines.Add($"[grey]{scope.FileCount} files[/]");
        return string.Join(Environment.NewLine, lines);
    }

    private static void RenderListHeader(DashboardState state)
    {
        AnsiConsole.WriteLine();

        var filterNote = state.PrimaryView == DashboardPrimaryView.Tests && state.Navigator.FailuresOnly
            ? " · [red]failures only[/]"
            : string.Empty;

        var itemCount = state.ItemCount;
        var visibleCount = state.PrimaryView switch
        {
            DashboardPrimaryView.Tests => state.VisibleTestItems.Count,
            DashboardPrimaryView.Coverage => state.VisibleCoverageItems.Count,
            DashboardPrimaryView.Analyser => state.VisibleAnalyserItems.Count,
            _ => 0,
        };
        var range = itemCount == 0
            ? "0 items"
            : $"{state.ViewportStart + 1}–{state.ViewportStart + visibleCount} of {itemCount}";

        var title = state.PrimaryView switch
        {
            DashboardPrimaryView.Tests => state.Navigator.LevelTitle,
            DashboardPrimaryView.Coverage => "Source files",
            DashboardPrimaryView.Analyser => "PSScriptAnalyzer findings",
            _ => "Items",
        };

        var hint = state.PrimaryView switch
        {
            DashboardPrimaryView.Coverage => " · [grey]Enter file detail[/]",
            DashboardPrimaryView.Analyser => " · [grey]Enter detail[/]",
            _ => string.Empty,
        };

        AnsiConsole.MarkupLine($"[bold]{title}[/]  [grey]{range}[/]{filterNote}{hint}");
    }

    private static void RenderTestsTable(DashboardState state)
    {
        var nameWidth = state.NameColumnWidth;
        var table = new Table().Border(TableBorder.Rounded).Expand();

        table.AddColumn("", c => c.Width(1).NoWrap());
        table.AddColumn("Name", c => c.Width(nameWidth));
        table.AddColumn("Pass", c => c.RightAligned().NoWrap());
        table.AddColumn("Fail", c => c.RightAligned().NoWrap());
        table.AddColumn("Skip", c => c.RightAligned().NoWrap());
        table.AddColumn("Total", c => c.RightAligned().NoWrap());
        table.AddColumn("%", c => c.RightAligned().NoWrap());
        table.AddColumn("Duration", c => c.RightAligned().NoWrap());

        var visible = state.VisibleTestItems;
        if (visible.Count == 0)
        {
            table.AddRow(string.Empty, "[grey]No items at this level[/]", "-", "-", "-", "-", "-", "-");
        }
        else
        {
            for (var i = 0; i < visible.Count; i++)
            {
                var absoluteIndex = state.ViewportStart + i;
                AddTestRow(table, visible[i], absoluteIndex == state.SelectedIndex, nameWidth);
            }
        }

        AnsiConsole.Write(table);
    }

    private static void AddTestRow(Table table, TestTreeNode item, bool selected, int nameWidth)
    {
        var style = selected ? "[black on cyan]" : string.Empty;
        var end = selected ? "[/]" : string.Empty;
        var marker = selected ? ">" : string.Empty;

        var total = item.Total;
        var passRate = total == 0 ? 0 : item.Passed * 100.0 / total;
        var passRateColor = item.Failed > 0 ? "yellow" : "green";

        var displayName = item.Kind switch
        {
            TestTreeNodeKind.File => $"[blue]{Markup.Escape(Truncate(item.Name, nameWidth))}[/]",
            TestTreeNodeKind.Describe => Markup.Escape(Truncate(item.Name, nameWidth)),
            TestTreeNodeKind.Context => $"  {Markup.Escape(Truncate(item.Name, nameWidth - 2))}",
            TestTreeNodeKind.Test => $"{OutcomeIcon(item.Test?.Outcome ?? TestOutcome.Inconclusive)} {Markup.Escape(Truncate(item.Name, nameWidth - 2))}",
            _ => Markup.Escape(Truncate(item.Name, nameWidth)),
        };

        var duration = item.Kind == TestTreeNodeKind.Test
            ? $"{item.Duration.TotalMilliseconds:F0}ms"
            : FormatDuration(item.Duration);

        table.AddRow(
            $"{style}{marker}{end}",
            $"{style}{displayName}{end}",
            $"{style}[green]{item.Passed}[/]{end}",
            $"{style}{(item.Failed > 0 ? $"[red]{item.Failed}[/]" : "0")}{end}",
            $"{style}[yellow]{item.Skipped}[/]{end}",
            $"{style}{total}{end}",
            $"{style}[{passRateColor}]{passRate:F0}%[/]{end}",
            $"{style}{duration}{end}");
    }

    private static void RenderCoverageTable(DashboardState state)
    {
        var fileWidth = state.FileColumnWidth;
        var table = new Table().Border(TableBorder.Rounded).Expand();

        table.AddColumn("", c => c.Width(1).NoWrap());
        table.AddColumn("File", c => c.Width(fileWidth));
        table.AddColumn("Line %", c => c.RightAligned().NoWrap());
        table.AddColumn("Lines", c => c.RightAligned().NoWrap());
        table.AddColumn("Branch %", c => c.RightAligned().NoWrap());
        table.AddColumn("Branches", c => c.RightAligned().NoWrap());

        var visible = state.VisibleCoverageItems;
        if (visible.Count == 0)
        {
            table.AddRow(string.Empty, "[grey]No coverage files in this view[/]", "-", "-", "-", "-");
        }
        else
        {
            for (var i = 0; i < visible.Count; i++)
            {
                var absoluteIndex = state.ViewportStart + i;
                AddCoverageRow(table, visible[i], absoluteIndex == state.SelectedIndex, fileWidth);
            }
        }

        AnsiConsole.Write(table);
    }

    private static void AddCoverageRow(Table table, CoverageFileEntry file, bool selected, int fileWidth)
    {
        var style = selected ? "[black on cyan]" : string.Empty;
        var end = selected ? "[/]" : string.Empty;
        var marker = selected ? ">" : string.Empty;
        var lineColor = CoverageColor(file.LinePercent);
        var branchText = file.HasBranchCoverage
            ? $"[{CoverageColor(file.BranchPercent)}]{file.BranchPercent:F0}%[/]"
            : "[grey]-[/]";

        table.AddRow(
            $"{style}{marker}{end}",
            $"{style}{Markup.Escape(Truncate(file.FileName, fileWidth))}{end}",
            $"{style}[{lineColor}]{file.LinePercent:F0}%[/]{end}",
            $"{style}{file.CoveredLines}/{file.TotalLines}{end}",
            $"{style}{branchText}{end}",
            $"{style}{(file.HasBranchCoverage ? $"{file.CoveredBranches}/{file.TotalBranches}" : "-")}{end}");
    }

    private static void RenderAnalyserTable(DashboardState state)
    {
        var (ruleWidth, fileWidth, messageWidth) = state.AnalyserColumnWidths;
        var table = new Table().Border(TableBorder.Rounded).Expand();

        table.AddColumn("", c => c.Width(1).NoWrap());
        table.AddColumn("Severity", c => c.NoWrap());
        table.AddColumn("Rule", c => c.Width(ruleWidth).NoWrap());
        table.AddColumn("File", c => c.Width(fileWidth));
        table.AddColumn("Line", c => c.RightAligned().NoWrap());
        table.AddColumn("Message", c => c.Width(messageWidth));

        var visible = state.VisibleAnalyserItems;
        if (visible.Count == 0)
        {
            var note = state.Data.Analyser?.WarningMessage;
            if (string.IsNullOrWhiteSpace(note))
            {
                table.AddRow(string.Empty, "-", "-", "[grey]No analyser findings in scope[/]", "-", "-");
            }
            else
            {
                var summary = RunLogFormatter.GetStderrSummary(note) ?? note;
                table.AddRow(
                    string.Empty,
                    "[yellow]Error[/]",
                    "Analyser",
                    "[grey]-[/]",
                    "-",
                    $"[yellow]{Markup.Escape(Truncate(summary, messageWidth))}[/]");
            }
        }
        else
        {
            for (var i = 0; i < visible.Count; i++)
            {
                var absoluteIndex = state.ViewportStart + i;
                AddAnalyserRow(table, visible[i], absoluteIndex == state.SelectedIndex, ruleWidth, fileWidth, messageWidth);
            }
        }

        AnsiConsole.Write(table);
    }

    private static void AddAnalyserRow(
        Table table,
        AnalyserFinding finding,
        bool selected,
        int ruleWidth,
        int fileWidth,
        int messageWidth)
    {
        var style = selected ? "[black on cyan]" : string.Empty;
        var end = selected ? "[/]" : string.Empty;
        var marker = selected ? ">" : string.Empty;
        var severityColor = finding.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
            ? "red"
            : finding.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                ? "yellow"
                : "grey";

        table.AddRow(
            $"{style}{marker}{end}",
            $"{style}[{severityColor}]{Markup.Escape(finding.Severity)}[/]{end}",
            $"{style}{Markup.Escape(Truncate(finding.RuleName, ruleWidth))}{end}",
            $"{style}{Markup.Escape(Truncate(Path.GetFileName(finding.ScriptPath), fileWidth))}{end}",
            $"{style}{finding.Line}{end}",
            $"{style}{Markup.Escape(Truncate(finding.Message, messageWidth))}{end}");
    }

    private static void RenderCoverageDetail(DashboardState state, CoverageFileEntry file)
    {
        var path = state.CoverageDetailSourcePath is not null
            ? Path.GetRelativePath(state.Data.Context.ProjectRoot, state.CoverageDetailSourcePath)
            : file.FileName;
        var lineColor = CoverageColor(file.LinePercent);
        var coverable = file.TotalLines;
        var covered = file.CoveredLines;
        var maxPathWidth = Math.Max(20, state.ContentWidth - 12);
        var displayPath = Truncate(path, maxPathWidth);

        AnsiConsole.MarkupLine(
            $"[bold]{Markup.Escape(file.FileName)}[/]  [grey]│[/]  [{lineColor}]{covered}/{coverable} lines ({file.LinePercent:F1}%)[/]");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(displayPath)}[/]");
        AnsiConsole.MarkupLine("[grey]" + new string('─', Math.Max(20, state.ContentWidth)) + "[/]");

        if (state.CoverageDetailSourceLines.Count == 0)
        {
            if (file.HasLineDetail)
            {
                RenderCoverageLineSummary(state, file);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Source file not found and no line detail in coverage report.[/]");
            }
        }
        else
        {
            RenderAnnotatedSource(state);
        }

        AnsiConsole.WriteLine();
        var footer = state.IsGoToLineInput
            ? $"Go to line: {Markup.Escape(state.GoToLineBuffer)}_  (Enter/Esc)"
            : "↑↓ PgUp/PgDn  g goto  c copy  v open  Esc back  q quit";
        RenderFooter(footer);
    }

    private static void RenderAnnotatedSource(DashboardState state)
    {
        var width = Math.Max(40, state.ContentWidth);
        var lineNumberWidth = Math.Max(4, state.CoverageDetailLineCount.ToString().Length);
        var codeWidth = width - lineNumberWidth - 4;

        for (var index = state.CoverageDetailScroll;
             index < state.CoverageDetailScroll + state.ListHeight && index < state.CoverageDetailSourceLines.Count;
             index++)
        {
            var lineNumber = index + 1;
            var status = state.GetLineStatus(lineNumber);
            var text = state.CoverageDetailSourceLines[index].TrimEnd('\r');
            var prefix = $"{lineNumber.ToString().PadLeft(lineNumberWidth)} │ ";
            var styled = FormatSourceLine(text, status, codeWidth);
            AnsiConsole.MarkupLine($"{prefix}{styled}");
        }
    }

    private static void RenderCoverageLineSummary(DashboardState state, CoverageFileEntry file)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Line coverage[/]")
            .Expand();

        table.AddColumn("Line", c => c.RightAligned().NoWrap());
        table.AddColumn("Status", c => c.NoWrap());
        table.AddColumn("Hits", c => c.RightAligned().NoWrap());

        var visible = file.Lines
            .Skip(state.CoverageDetailScroll)
            .Take(state.ListHeight)
            .ToList();

        foreach (var line in visible)
        {
            table.AddRow(
                line.LineNumber.ToString(),
                FormatLineStatus(line.Status),
                line.HitCount > 0 ? line.HitCount.ToString() : "-");
        }

        AnsiConsole.Write(table);
    }

    private static void RenderRunOutput(DashboardState state)
    {
        AnsiConsole.WriteLine();
        if (state.Data.TestRun.Total == 0)
        {
            AnsiConsole.MarkupLine("[bold red]Pester could not complete — raw output[/]");
            AnsiConsole.MarkupLine("[grey]Also saved to .pester-dash/logs/pester-output.log[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[bold]Pester run log[/]");
            if (state.Data.RunLogPath is not null)
            {
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(state.Data.RunLogPath)}[/]");
            }

            if (state.Data.DebugLogPath is not null)
            {
                AnsiConsole.MarkupLine($"[grey]Debug log:[/] {Markup.Escape(state.Data.DebugLogPath)}");
            }
        }

        AnsiConsole.WriteLine();

        foreach (var line in state.VisibleRunOutputLines)
        {
            AnsiConsole.MarkupLine(Markup.Escape(line));
        }

        AnsiConsole.WriteLine();
        RenderFooter("↑↓ PgUp/PgDn scroll  c copy  o artefacts  Esc back  q quit");
    }

    private static string FormatSourceLine(string text, CoverageLineStatus? status, int maxWidth)
    {
        var clipped = Truncate(text, maxWidth);
        var (backgroundOpen, backgroundClose) = status switch
        {
            CoverageLineStatus.Covered => ("[green on grey23]", "[/]"),
            CoverageLineStatus.Uncovered => ("[white on darkred]", "[/]"),
            CoverageLineStatus.Partial => ("[black on yellow]", "[/]"),
            CoverageLineStatus.NotExecutable => ("[grey on default]", "[/]"),
            _ => (string.Empty, string.Empty),
        };

        if (string.IsNullOrEmpty(backgroundOpen))
        {
            return Markup.Escape(clipped);
        }

        return PowerShellSyntaxHighlighter.Highlight(clipped, backgroundOpen, backgroundClose);
    }

    private static string FormatLineStatus(CoverageLineStatus status) => status switch
    {
        CoverageLineStatus.Covered => "[green]covered[/]",
        CoverageLineStatus.Uncovered => "[red]uncovered[/]",
        CoverageLineStatus.Partial => "[yellow]partial[/]",
        CoverageLineStatus.NotExecutable => "[grey]n/a[/]",
        _ => "[grey]unknown[/]",
    };

    private static void RenderAnalyserDetail(DashboardState state, AnalyserFinding finding)
    {
        var relativePath = Path.GetRelativePath(state.Data.Context.ProjectRoot, finding.ScriptPath);
        var width = Math.Max(40, Console.WindowWidth - 4);
        var lines = new List<string>
        {
            $"Rule:     {finding.RuleName}",
            $"Severity: {finding.Severity}",
            $"File:     {relativePath}",
            $"Line:     {(finding.Line > 0 ? finding.Line.ToString() : "-")}",
            $"Column:   {(finding.Column > 0 ? finding.Column.ToString() : "-")}",
            string.Empty,
            "Message:",
            WrapText(finding.Message, width),
        };

        var panel = new Panel(string.Join(Environment.NewLine, lines))
            .Header("[bold]Analyser finding[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
    }

    private static void RenderDetail(DashboardState state, TestCaseResult test)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(state.Navigator.Breadcrumb)}[/]");
        AnsiConsole.WriteLine();

        var lines = new List<string>
        {
            $"Test:     {test.Name}",
            $"Outcome:  {test.Outcome}",
            $"Duration: {test.Duration.TotalMilliseconds:F0}ms",
        };

        if (!string.IsNullOrWhiteSpace(test.ErrorMessage))
        {
            lines.Add(string.Empty);
            lines.Add("Error:");
            lines.Add(WrapText(test.ErrorMessage, Math.Max(40, Console.WindowWidth - 4)));
        }

        if (!string.IsNullOrWhiteSpace(test.StackTrace))
        {
            lines.Add(string.Empty);
            lines.Add("Stack trace:");
            lines.Add(WrapText(test.StackTrace, Math.Max(40, Console.WindowWidth - 4)));
        }

        var panel = new Panel(string.Join(Environment.NewLine, lines))
            .Header("[bold]Test detail[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
    }

    private static void RenderHelp()
    {
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Keyboard shortcuts[/]")
            .AddColumn("Key")
            .AddColumn("Action");

        table.AddRow("↑ / ↓ / PgUp / PgDn", "Move or scroll");
        table.AddRow("Enter", "Drill in, test/finding detail, or coverage file");
        table.AddRow("Esc", "Back / cancel");
        table.AddRow("Tab / t", "Switch Tests / Coverage / Analyser");
        table.AddRow("F5", "Run all tests in scope");
        table.AddRow("a", "Run PSScriptAnalyzer only (Analyser tab)");
        table.AddRow("r", "Rerun last filter / scope");
        table.AddRow("x", "Run current tree selection");
        table.AddRow("s", "Edit run scope");
        table.AddRow("w", "Toggle watch mode");
        table.AddRow("f", "Failures-only filter");
        table.AddRow("/", "Search");
        table.AddRow("g", "Go to line (coverage file view)");
        table.AddRow("l", "View Pester stdout/stderr log");
        table.AddRow("c", "Copy selection or summary to clipboard");
        table.AddRow("v", "Open test file in VS Code (or default editor)");
        table.AddRow("e", "Export JSON + CSV to results folder");
        table.AddRow("h", "Open coverage HTML report");
        table.AddRow("o", "Open artefact folder");
        table.AddRow("q", "Quit");
        table.AddRow("?", "This help");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to return...[/]");
    }

    private static string BuildFooter(DashboardState state)
    {
        if (state.IsSearching)
        {
            return $"Search: {Markup.Escape(state.Navigator.SearchQuery)}_  (Enter/Esc)";
        }

        return state.Data.HasProcessStderr
            ? "↑↓ move  Enter  Tab  F5 run  a analyser  r rerun  x selection  s scope  w watch  [bold yellow]l log[/]  f filter  / search  c copy  v open  e export  h coverage  ? help  q quit"
            : "↑↓ move  Enter  Tab  F5 run  a analyser  r rerun  x selection  s scope  w watch  l log  f filter  / search  c copy  v open  e export  h coverage  ? help  q quit";
    }

    private static void RenderFooter(string shortcuts) =>
        AnsiConsole.MarkupLine($"[grey]{shortcuts}[/]");

    private static void RenderFlash(DashboardState state)
    {
        if (!string.IsNullOrWhiteSpace(state.FlashMessage))
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(state.FlashMessage)}[/]");
        }
    }

    private static string BuildBar(double percent, string color)
    {
        var width = Math.Clamp(Console.WindowWidth / 4, 12, 28);
        var filled = (int)Math.Round(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        return $"[{color}]{new string('█', filled)}[/][grey]{new string('░', width - filled)}[/]";
    }

    private static string CoverageColor(double percent) => percent switch
    {
        >= 80 => "green",
        >= 50 => "yellow",
        _ => "red",
    };

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.TotalMinutes >= 1
                ? duration.ToString(@"m\:ss")
                : $"{duration.TotalSeconds:F1}s";

    private static string OutcomeIcon(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => "[green]✔[/]",
        TestOutcome.Failed => "[red]✘[/]",
        TestOutcome.Skipped => "[yellow]![/]",
        _ => "[grey]?[/]",
    };

    private static string Truncate(string value, int maxWidth)
    {
        if (string.IsNullOrEmpty(value) || maxWidth <= 1)
        {
            return value;
        }

        return value.Length <= maxWidth
            ? value
            : value[..(maxWidth - 1)] + "…";
    }

    private static string WrapText(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length <= width)
            {
                lines.Add(line);
                continue;
            }

            for (var offset = 0; offset < line.Length; offset += width)
            {
                lines.Add(line.Substring(offset, Math.Min(width, line.Length - offset)));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
