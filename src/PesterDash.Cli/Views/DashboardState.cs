using PesterDash.Core.Models;
using PesterDash.Cli.Utilities;

namespace PesterDash.Cli.Views;

internal enum DashboardMode
{
    Navigate,
    Detail,
    CoverageDetail,
    RunOutput,
    Help,
}

internal sealed class DashboardState
{
    public const int AppHeaderLines = 2;
    public const int HeaderLines = 11;
    public const int FooterLines = 2;
    public const int CoverageDetailInfoLines = 7;
    public const int RunOutputHeaderLines = 3;

    public required DashboardData Data { get; set; }

    public required TestNavigator Navigator { get; set; }

    public DashboardMode Mode { get; set; } = DashboardMode.Navigate;

    public DashboardPrimaryView PrimaryView { get; set; } = DashboardPrimaryView.Tests;

    public int SelectedIndex { get; set; }

    public bool IsSearching { get; set; }

    public bool IsGoToLineInput { get; set; }

    public string GoToLineBuffer { get; set; } = string.Empty;

    public string? FlashMessage { get; set; }

    public bool IsWatching { get; set; }

    public CoverageFileEntry? CoverageDetailFile { get; set; }

    public int CoverageDetailScroll { get; set; }

    public int RunOutputScroll { get; set; }

    public string? CoverageDetailSourcePath { get; set; }

    public IReadOnlyList<string> CoverageDetailSourceLines { get; private set; } = [];

    public IReadOnlyList<string> RunOutputLines { get; private set; } = [];

    public TestCaseResult? DetailTest { get; set; }

    public AnalyserFinding? DetailAnalyserFinding { get; set; }

    public (int RuleWidth, int FileWidth, int MessageWidth) AnalyserColumnWidths =>
        TableLayoutHelper.ComputeAnalyserColumnWidths(ContentWidth);

    public static DashboardState Create(DashboardData data)
    {
        var tree = data.TestRun.Tree ?? new TestTreeNode
        {
            Name = "Tests",
            Kind = TestTreeNodeKind.Root,
            Children = BuildFlatTree(data.TestRun.Tests),
        };

        var state = new DashboardState
        {
            Data = data,
            Navigator = new TestNavigator(tree)
            {
                FailuresOnly = data.TestRun.Failed > 0,
            },
            RunOutputLines = SplitLines(data.RunOutput),
            IsWatching = data.IsWatching,
        };

        if (!string.IsNullOrWhiteSpace(data.RunOutput) && data.TestRun.Total == 0)
        {
            state.Mode = DashboardMode.RunOutput;
        }
        else if (data.HasProcessStderr)
        {
            state.FlashMessage = "Pester wrote to stderr — press l for the full run log.";
        }

        return state;
    }

    public string ProjectDisplayName
    {
        get
        {
            var root = Data.Context.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(root);
        }
    }

    public IReadOnlyList<TestTreeNode> TestItems => Navigator.VisibleItems;

    public IReadOnlyList<CoverageFileEntry> CoverageItems
    {
        get
        {
            var files = Data.Coverage?.Metrics?.Files ?? [];
            var scoped = ScopeCoverageFiles(files);
            var query = Navigator.SearchQuery;

            if (!string.IsNullOrWhiteSpace(query))
            {
                scoped = scoped
                    .Where(file => file.FileName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return scoped;
        }
    }

    public int ItemCount => Mode switch
    {
        DashboardMode.CoverageDetail => CoverageDetailLineCount,
        DashboardMode.RunOutput => RunOutputLines.Count,
        _ => PrimaryView switch
        {
            DashboardPrimaryView.Tests => TestItems.Count,
            DashboardPrimaryView.Coverage => CoverageItems.Count,
            DashboardPrimaryView.Analyser => AnalyserItems.Count,
            _ => 0,
        },
    };

    public int CoverageDetailLineCount => CoverageDetailSourceLines.Count > 0
        ? CoverageDetailSourceLines.Count
        : CoverageDetailFile?.Lines.Count ?? 0;

    public TestTreeNode? SelectedTestItem
    {
        get
        {
            if (PrimaryView != DashboardPrimaryView.Tests || Mode != DashboardMode.Navigate)
            {
                return null;
            }

            var items = TestItems;
            return SelectedIndex >= 0 && SelectedIndex < items.Count
                ? items[SelectedIndex]
                : null;
        }
    }

    public CoverageFileEntry? SelectedCoverageItem
    {
        get
        {
            if (PrimaryView != DashboardPrimaryView.Coverage || Mode != DashboardMode.Navigate)
            {
                return null;
            }

            var items = CoverageItems;
            return SelectedIndex >= 0 && SelectedIndex < items.Count
                ? items[SelectedIndex]
                : null;
        }
    }

    public AnalyserFinding? SelectedAnalyserItem
    {
        get
        {
            if (PrimaryView != DashboardPrimaryView.Analyser || Mode != DashboardMode.Navigate)
            {
                return null;
            }

            var items = AnalyserItems;
            return SelectedIndex >= 0 && SelectedIndex < items.Count
                ? items[SelectedIndex]
                : null;
        }
    }

    public IReadOnlyList<AnalyserFinding> AnalyserItems
    {
        get
        {
            var findings = Data.Analyser?.Findings ?? [];
            var query = Navigator.SearchQuery;

            if (string.IsNullOrWhiteSpace(query))
            {
                return findings;
            }

            return findings
                .Where(finding =>
                    finding.RuleName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || finding.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || finding.ScriptPath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public ScopeSummary CurrentTestScope => Navigator.CurrentNode is { } node
        ? ScopeSummary.FromNode(node)
        : ScopeSummary.FromRun(Data.TestRun);

    public CoverageScopeSummary CurrentCoverageScope =>
        CoverageScopeSummary.FromFiles(CoverageItems, GetCoverageScopeTitle());

    public int ListHeight => Mode switch
    {
        DashboardMode.CoverageDetail => Math.Max(
            4,
            Console.WindowHeight - AppHeaderLines - CoverageDetailInfoLines - FooterLines - 1),
        DashboardMode.RunOutput => Math.Max(
            4,
            Console.WindowHeight - AppHeaderLines - RunOutputHeaderLines - FooterLines),
        _ => Math.Max(4, Console.WindowHeight - AppHeaderLines - HeaderLines - FooterLines),
    };

    public int NameColumnWidth => TableLayoutHelper.ComputeNameColumnWidth(ContentWidth, Mode);

    public int FileColumnWidth => TableLayoutHelper.ComputeFileColumnWidth(ContentWidth);

    public void OpenCoverageDetail(CoverageFileEntry file, string projectRoot)
    {
        CoverageDetailFile = file;
        CoverageDetailScroll = 0;
        IsGoToLineInput = false;
        GoToLineBuffer = string.Empty;
        CoverageDetailSourcePath = SourceFileResolver.Find(projectRoot, file.FileName);
        CoverageDetailSourceLines = CoverageDetailSourcePath is not null && File.Exists(CoverageDetailSourcePath)
            ? File.ReadAllLines(CoverageDetailSourcePath)
            : [];
        Mode = DashboardMode.CoverageDetail;
    }

    public void CloseCoverageDetail()
    {
        CoverageDetailFile = null;
        CoverageDetailSourcePath = null;
        CoverageDetailSourceLines = [];
        CoverageDetailScroll = 0;
        IsGoToLineInput = false;
        GoToLineBuffer = string.Empty;
        Mode = DashboardMode.Navigate;
    }

    public void MoveCoverageDetailScroll(int delta)
    {
        var maxScroll = Math.Max(0, CoverageDetailLineCount - ListHeight);
        CoverageDetailScroll = Math.Clamp(CoverageDetailScroll + delta, 0, maxScroll);
    }

    public void PageCoverageDetailScroll(int direction) =>
        MoveCoverageDetailScroll(direction * Math.Max(1, ListHeight - 1));

    public void MoveRunOutputScroll(int delta)
    {
        var maxScroll = Math.Max(0, RunOutputLines.Count - ListHeight);
        RunOutputScroll = Math.Clamp(RunOutputScroll + delta, 0, maxScroll);
    }

    public void PageRunOutputScroll(int direction) =>
        MoveRunOutputScroll(direction * Math.Max(1, ListHeight - 1));

    public void GoToCoverageLine(int lineNumber)
    {
        if (CoverageDetailLineCount == 0)
        {
            return;
        }

        var index = CoverageDetailSourceLines.Count > 0
            ? Math.Clamp(lineNumber - 1, 0, CoverageDetailSourceLines.Count - 1)
            : FindCoverageLineIndex(lineNumber);

        if (index < 0)
        {
            FlashMessage = $"Line {lineNumber} not found in coverage data.";
            return;
        }

        CoverageDetailScroll = Math.Clamp(index - ListHeight / 2, 0, Math.Max(0, CoverageDetailLineCount - ListHeight));
        FlashMessage = $"Jumped to line {lineNumber}.";
    }

    private int FindCoverageLineIndex(int lineNumber)
    {
        if (CoverageDetailFile is null)
        {
            return -1;
        }

        for (var i = 0; i < CoverageDetailFile.Lines.Count; i++)
        {
            if (CoverageDetailFile.Lines[i].LineNumber == lineNumber)
            {
                return i;
            }
        }

        return -1;
    }

    public CoverageLineStatus? GetLineStatus(int lineNumber) =>
        CoverageDetailFile?.Lines.FirstOrDefault(line => line.LineNumber == lineNumber)?.Status;

    public string? GetSelectedTestFilePath()
    {
        var fileNode = Navigator.Path.LastOrDefault(node => node.Kind == TestTreeNodeKind.File)
            ?? (SelectedTestItem?.Kind == TestTreeNodeKind.File ? SelectedTestItem : null);

        if (fileNode?.SourcePath is { } path && File.Exists(path))
        {
            return path;
        }

        if (fileNode is not null)
        {
            return SourceFileResolver.Find(Data.Context.ProjectRoot, fileNode.Name);
        }

        return null;
    }

    public string? GetCopyText()
    {
        if (DetailAnalyserFinding is { } detailFinding)
        {
            return FormatAnalyserCopy(detailFinding);
        }

        if (DetailTest is { } detailTest)
        {
            return FormatTestCopy(detailTest);
        }

        if (PrimaryView == DashboardPrimaryView.Analyser && SelectedAnalyserItem is { } selectedFinding)
        {
            return FormatAnalyserCopy(selectedFinding);
        }

        if (SelectedTestItem?.Test is { } selectedTest)
        {
            return FormatTestCopy(selectedTest);
        }

        if (Mode == DashboardMode.RunOutput)
        {
            return Data.RunOutput;
        }

        var run = Data.TestRun;
        return
            $"PesterDash results — {ProjectDisplayName}\n" +
            $"Passed: {run.Passed}  Failed: {run.Failed}  Skipped: {run.Skipped}  ({run.PassRate:F1}%)\n" +
            $"Duration: {run.Duration}";
    }

    public int ViewportStart
    {
        get
        {
            if (Mode == DashboardMode.RunOutput)
            {
                return RunOutputScroll;
            }

            if (Mode == DashboardMode.CoverageDetail && CoverageDetailSourceLines.Count == 0)
            {
                return CoverageDetailScroll;
            }

            var count = ItemCount;
            if (count == 0)
            {
                return 0;
            }

            var height = ListHeight;
            if (count <= height)
            {
                return 0;
            }

            var start = SelectedIndex - height / 2;
            return Math.Clamp(start, 0, count - height);
        }
    }

    public IReadOnlyList<TestTreeNode> VisibleTestItems =>
        TestItems.Skip(ViewportStart).Take(ListHeight).ToList();

    public IReadOnlyList<CoverageFileEntry> VisibleCoverageItems =>
        CoverageItems.Skip(ViewportStart).Take(ListHeight).ToList();

    public IReadOnlyList<AnalyserFinding> VisibleAnalyserItems =>
        AnalyserItems.Skip(ViewportStart).Take(ListHeight).ToList();

    public IReadOnlyList<string> VisibleRunOutputLines =>
        RunOutputLines.Skip(RunOutputScroll).Take(ListHeight).ToList();

    public int ContentWidth => Math.Max(40, Console.WindowWidth - 4);

    public void OpenRunLogView()
    {
        if (string.IsNullOrWhiteSpace(Data.RunOutput))
        {
            return;
        }

        RunOutputLines = SplitLines(Data.RunOutput);
        RunOutputScroll = 0;
        Mode = DashboardMode.RunOutput;
    }

    public void TogglePrimaryView()
    {
        PrimaryView = PrimaryView switch
        {
            DashboardPrimaryView.Tests => DashboardPrimaryView.Coverage,
            DashboardPrimaryView.Coverage => DashboardPrimaryView.Analyser,
            _ => DashboardPrimaryView.Tests,
        };
        ResetSelection();
    }

    public void ClampSelection()
    {
        var count = ItemCount;
        SelectedIndex = count == 0 ? 0 : Math.Clamp(SelectedIndex, 0, count - 1);
    }

    public void MoveSelection(int delta)
    {
        SelectedIndex += delta;
        ClampSelection();
    }

    public void PageSelection(int direction)
    {
        MoveSelection(direction * Math.Max(1, ListHeight - 1));
    }

    public void ResetSelection()
    {
        SelectedIndex = 0;
    }

    private static string FormatAnalyserCopy(AnalyserFinding finding) =>
        $"{finding.RuleName} ({finding.Severity})\n" +
        $"{finding.ScriptPath}:{finding.Line}\n" +
        finding.Message;

    private static string FormatTestCopy(TestCaseResult test)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(test.Name);
        builder.AppendLine($"Outcome: {test.Outcome}");
        builder.AppendLine($"Duration: {test.Duration.TotalMilliseconds:F0}ms");

        if (!string.IsNullOrWhiteSpace(test.ErrorMessage))
        {
            builder.AppendLine();
            builder.AppendLine(test.ErrorMessage);
        }

        if (!string.IsNullOrWhiteSpace(test.StackTrace))
        {
            builder.AppendLine();
            builder.AppendLine(test.StackTrace);
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> SplitLines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private string GetCoverageScopeTitle()
    {
        var fileNode = Navigator.Path.FirstOrDefault(node => node.Kind == TestTreeNodeKind.File);
        if (fileNode is not null)
        {
            return $"Coverage for {fileNode.Name}";
        }

        return Navigator.Path.Count > 0
            ? $"Coverage · {Navigator.Breadcrumb}"
            : "All source files";
    }

    private IReadOnlyList<CoverageFileEntry> ScopeCoverageFiles(IReadOnlyList<CoverageFileEntry> files)
    {
        var fileNode = Navigator.Path.FirstOrDefault(node => node.Kind == TestTreeNodeKind.File);
        if (fileNode is null)
        {
            return files;
        }

        var token = fileNode.Name
            .Replace(".Tests.ps1", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".ps1", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(token))
        {
            return files;
        }

        var scoped = files
            .Where(file => file.FileName.Contains(token, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return scoped.Count > 0 ? scoped : files;
    }

    private static List<TestTreeNode> BuildFlatTree(IReadOnlyList<TestCaseResult> tests)
    {
        return tests
            .GroupBy(test => GetFileName(test.Name))
            .Select(group => new TestTreeNode
            {
                Name = group.Key,
                Kind = TestTreeNodeKind.File,
                Children = group.Select(test => new TestTreeNode
                {
                    Name = test.Name,
                    Kind = TestTreeNodeKind.Test,
                    Test = test,
                }).ToList(),
            })
            .ToList();
    }

    private static string GetFileName(string name)
    {
        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0] : "Tests";
    }
}

internal static class TableLayoutHelper
{
    public static int ComputeNameColumnWidth(int contentWidth, DashboardMode mode)
    {
        if (mode == DashboardMode.CoverageDetail)
        {
            return Math.Max(30, contentWidth - 12);
        }

        const int fixedColumns = 42;
        var available = contentWidth - fixedColumns;
        var wide = (int)(contentWidth * 0.55);
        return Math.Clamp(Math.Max(available, wide), 24, contentWidth - 20);
    }

    public static int ComputeFileColumnWidth(int contentWidth)
    {
        const int fixedColumns = 28;
        var available = contentWidth - fixedColumns;
        var wide = (int)(contentWidth * 0.5);
        return Math.Clamp(Math.Max(available, wide), 28, contentWidth - 16);
    }

    public static (int RuleWidth, int FileWidth, int MessageWidth) ComputeAnalyserColumnWidths(int contentWidth)
    {
        const int markerWidth = 1;
        const int severityWidth = 9;
        const int lineWidth = 5;
        const int padding = 10;
        var ruleWidth = 22;
        var fileWidth = Math.Clamp(28, 24, 34);
        var messageWidth = Math.Max(
            36,
            contentWidth - markerWidth - severityWidth - ruleWidth - fileWidth - lineWidth - padding);
        return (ruleWidth, fileWidth, messageWidth);
    }
}

internal sealed class CoverageScopeSummary
{
    public required string Title { get; init; }

    public int CoveredLines { get; init; }

    public int TotalLines { get; init; }

    public int CoveredBranches { get; init; }

    public int TotalBranches { get; init; }

    public int FileCount { get; init; }

    public double LinePercent => TotalLines == 0 ? 0 : CoveredLines * 100.0 / TotalLines;

    public double BranchPercent => TotalBranches == 0 ? 0 : CoveredBranches * 100.0 / TotalBranches;

    public bool HasBranchCoverage => TotalBranches > 0;

    public static CoverageScopeSummary FromFiles(IReadOnlyList<CoverageFileEntry> files, string title) =>
        new()
        {
            Title = title,
            FileCount = files.Count,
            CoveredLines = files.Sum(file => file.CoveredLines),
            TotalLines = files.Sum(file => file.TotalLines),
            CoveredBranches = files.Sum(file => file.CoveredBranches),
            TotalBranches = files.Sum(file => file.TotalBranches),
        };

    public static CoverageScopeSummary FromResult(CoverageResult? result) =>
        result is null
            ? new CoverageScopeSummary
            {
                Title = "No coverage",
                FileCount = 0,
            }
            : new CoverageScopeSummary
            {
                Title = "Overall",
                FileCount = result.Files.Count,
                CoveredLines = result.CoveredLines,
                TotalLines = result.TotalLines,
                CoveredBranches = result.CoveredBranches,
                TotalBranches = result.TotalBranches,
            };
}
