using PesterDash.Core.Discovery;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;
using PesterDash.Cli.Utilities;
using Spectre.Console;

namespace PesterDash.Cli.Services;

/// <summary>Interactive drill-down dashboard with keyboard navigation.</summary>
public sealed class DashboardService : IDashboard
{
    private const int WatchPollMilliseconds = 250;

    /// <inheritdoc />
    public async Task<int> ShowAsync(
        DashboardData data,
        IDashboardSession session,
        CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected)
        {
            return ExitCode(data);
        }

        var state = Views.DashboardState.Create(data);

        try
        {
            return await AlternateScreen.RunAsync(async () =>
            {
                var needsRedraw = true;

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (needsRedraw)
                    {
                        if (state.Mode is Views.DashboardMode.Navigate)
                        {
                            state.ClampSelection();
                        }

                        Views.DashboardRenderer.Render(state);
                        needsRedraw = false;
                    }

                    if (session.IsWatching && session.TryConsumeWatchTrigger())
                    {
                        var watchData = await session.RunScopeAsync(cancellationToken).ConfigureAwait(false);
                        (state, data) = ApplyRunResult(state, data, watchData, "Watch rerun complete.");
                        needsRedraw = true;
                        continue;
                    }

                    ConsoleKeyInfo key;
                    if (session.IsWatching)
                    {
                        if (!Console.KeyAvailable)
                        {
                            await Task.Delay(WatchPollMilliseconds, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        key = Console.ReadKey(intercept: true);
                    }
                    else
                    {
                        var keyInfo = AnsiConsole.Console.Input.ReadKey(intercept: true);
                        if (keyInfo is null)
                        {
                            return ExitCode(data);
                        }

                        key = keyInfo.Value;
                    }

                    if (state.IsSearching)
                    {
                        if (HandleSearchInput(state, key))
                        {
                            needsRedraw = true;
                            continue;
                        }
                    }

                    if (state.IsGoToLineInput)
                    {
                        if (HandleGoToLineInput(state, key))
                        {
                            needsRedraw = true;
                            continue;
                        }
                    }

                    if (state.Mode == Views.DashboardMode.Help)
                    {
                        state.Mode = state.Data.TestRun.Total == 0 && state.Data.RunOutput is not null
                            ? Views.DashboardMode.RunOutput
                            : Views.DashboardMode.Navigate;
                        needsRedraw = true;
                        continue;
                    }

                    if (state.Mode == Views.DashboardMode.Detail)
                    {
                        if (HandleDetailInput(state, key))
                        {
                            needsRedraw = true;
                            continue;
                        }
                    }

                    if (state.Mode == Views.DashboardMode.CoverageDetail)
                    {
                        if (HandleCoverageDetailInput(state, key))
                        {
                            needsRedraw = true;
                            continue;
                        }
                    }

                    if (state.Mode == Views.DashboardMode.RunOutput)
                    {
                        if (HandleRunOutputInput(state, key))
                        {
                            needsRedraw = true;
                            continue;
                        }
                    }

                    if (HandleGlobalInput(state, key))
                    {
                        needsRedraw = true;
                        continue;
                    }

                    var runAction = await HandleRunActionAsync(state, data, session, key, cancellationToken)
                        .ConfigureAwait(false);
                    if (runAction.Handled)
                    {
                        state = runAction.State;
                        data = runAction.Data;
                        needsRedraw = true;
                        continue;
                    }

                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                            return ExitCode(data);

                        case ConsoleKey.UpArrow:
                            state.MoveSelection(-1);
                            break;

                        case ConsoleKey.DownArrow:
                            state.MoveSelection(1);
                            break;

                        case ConsoleKey.PageUp:
                            state.PageSelection(-1);
                            break;

                        case ConsoleKey.PageDown:
                            state.PageSelection(1);
                            break;

                        case ConsoleKey.Home:
                            state.SelectedIndex = 0;
                            break;

                        case ConsoleKey.End:
                            state.SelectedIndex = Math.Max(0, state.ItemCount - 1);
                            break;

                        case ConsoleKey.Enter:
                            HandleEnter(state);
                            break;

                        case ConsoleKey.Escape:
                            if (state.Navigator.CanGoUp)
                            {
                                state.Navigator.GoUp();
                                state.ResetSelection();
                            }

                            break;

                        case ConsoleKey.Tab:
                            state.TogglePrimaryView();
                            break;

                        case ConsoleKey.Oem2:
                        case ConsoleKey.Divide:
                            state.IsSearching = true;
                            state.Navigator.SearchQuery = string.Empty;
                            break;

                        case ConsoleKey.F when state.PrimaryView == Views.DashboardPrimaryView.Tests:
                            state.Navigator.FailuresOnly = !state.Navigator.FailuresOnly;
                            state.ResetSelection();
                            break;

                        case ConsoleKey.O:
                            ProcessLauncher.OpenPath(state.Data.Context.OutputDirectory);
                            break;

                        default:
                            HandleDefaultKey(state, key);
                            break;
                    }

                    needsRedraw = true;
                }

                return 130;
            });
        }
        finally
        {
            session.Dispose();
        }
    }

    private static async Task<(bool Handled, Views.DashboardState State, DashboardData Data)> HandleRunActionAsync(
        Views.DashboardState state,
        DashboardData data,
        IDashboardSession session,
        ConsoleKeyInfo key,
        CancellationToken cancellationToken)
    {
        if (key.Key == ConsoleKey.F5)
        {
            var runData = await session.RunAllAsync(cancellationToken).ConfigureAwait(false);
            var updated = ApplyRunResult(state, data, runData, "Run complete.");
            return (true, updated.State, updated.Data);
        }

        if (key.KeyChar is 'r' or 'R')
        {
            var runData = await session.RunScopeAsync(cancellationToken).ConfigureAwait(false);
            var updated = ApplyRunResult(state, data, runData, "Rerun complete.");
            return (true, updated.State, updated.Data);
        }

        if (key.KeyChar is 'x' or 'X')
        {
            var filter = RunFilterBuilder.FromSelectedItem(
                state.SelectedTestItem,
                state.Navigator.Path);
            if (!filter.IsActive)
            {
                state.FlashMessage = "Select a file, describe, context, or test to run.";
                return (true, state, data);
            }

            var runData = await session.RunSelectionAsync(filter, cancellationToken).ConfigureAwait(false);
            var updated = ApplyRunResult(state, data, runData, $"Ran {filter.Describe()}.");
            return (true, updated.State, updated.Data);
        }

        if (key.KeyChar is 's' or 'S')
        {
            var scopeData = await session.EditScopeAsync(cancellationToken).ConfigureAwait(false);
            if (scopeData is not null)
            {
                var updated = ApplyRunResult(state, data, scopeData);
                return (true, updated.State, updated.Data);
            }

            return (true, state, data);
        }

        if (key.KeyChar is 'a' or 'A')
        {
            var analyserData = await session.RunAnalyserAsync(cancellationToken).ConfigureAwait(false);
            if (analyserData is not null)
            {
                var updated = ApplyRunResult(state, data, analyserData, "Analyser run complete.");
                return (true, updated.State, updated.Data);
            }

            state.FlashMessage = "Analyser run did not produce results.";
            return (true, state, data);
        }

        if (key.KeyChar is 'w' or 'W')
        {
            var watching = await session.ToggleWatchAsync(cancellationToken).ConfigureAwait(false);
            state.IsWatching = watching;
            state.FlashMessage = watching
                ? "Watch mode on — scoped file changes trigger reruns."
                : "Watch mode off.";
            return (true, state, data);
        }

        return (false, state, data);
    }

    private static (Views.DashboardState State, DashboardData Data) ApplyRunResult(
        Views.DashboardState state,
        DashboardData data,
        DashboardData? newData,
        string? flashMessage = null)
    {
        if (newData is null)
        {
            state.FlashMessage = "Run did not produce results.";
            return (state, data);
        }

        state = Views.DashboardState.Create(newData);
        state.IsWatching = newData.IsWatching;
        data = newData;

        if (!string.IsNullOrWhiteSpace(flashMessage))
        {
            state.FlashMessage = flashMessage;
        }

        return (state, data);
    }

    private static bool HandleGlobalInput(Views.DashboardState state, ConsoleKeyInfo key)
    {
        if (key.KeyChar is 'l' or 'L' && state.Data.RunOutput is not null)
        {
            state.OpenRunLogView();
            return true;
        }

        if (key.KeyChar is 'c' or 'C')
        {
            TryCopy(state);
            return true;
        }

        if (key.KeyChar is 'v' or 'V')
        {
            TryOpenTestFile(state);
            return true;
        }

        if (key.KeyChar is 'e' or 'E')
        {
            TryExport(state);
            return true;
        }

        if (key.KeyChar is 'h' or 'H')
        {
            TryOpenCoverageHtml(state);
            return true;
        }

        if (key.KeyChar is 'g' or 'G' && state.Mode == Views.DashboardMode.CoverageDetail)
        {
            state.IsGoToLineInput = true;
            state.GoToLineBuffer = string.Empty;
            return true;
        }

        return false;
    }

    private static void HandleDefaultKey(Views.DashboardState state, ConsoleKeyInfo key)
    {
        if (key.KeyChar == '?')
        {
            state.Mode = Views.DashboardMode.Help;
        }
        else if (key.KeyChar is 't' or 'T')
        {
            state.TogglePrimaryView();
        }
    }

    private static bool HandleDetailInput(Views.DashboardState state, ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Q)
        {
            return false;
        }

        if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape)
        {
            state.DetailTest = null;
            state.DetailAnalyserFinding = null;
            state.Mode = Views.DashboardMode.Navigate;
            return true;
        }

        return HandleGlobalInput(state, key);
    }

    private static bool HandleRunOutputInput(Views.DashboardState state, ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
                return false;

            case ConsoleKey.Escape:
            case ConsoleKey.Enter when state.Data.TestRun.Total > 0:
                state.Mode = Views.DashboardMode.Navigate;
                return true;

            case ConsoleKey.UpArrow:
                state.MoveRunOutputScroll(-1);
                return true;

            case ConsoleKey.DownArrow:
                state.MoveRunOutputScroll(1);
                return true;

            case ConsoleKey.PageUp:
                state.PageRunOutputScroll(-1);
                return true;

            case ConsoleKey.PageDown:
                state.PageRunOutputScroll(1);
                return true;

            case ConsoleKey.Home:
                state.RunOutputScroll = 0;
                return true;

            case ConsoleKey.End:
                state.RunOutputScroll = Math.Max(0, state.RunOutputLines.Count - state.ListHeight);
                return true;

            case ConsoleKey.O:
                ProcessLauncher.OpenPath(state.Data.Context.OutputDirectory);
                return true;
        }

        return HandleGlobalInput(state, key);
    }

    private static bool HandleCoverageDetailInput(Views.DashboardState state, ConsoleKeyInfo key)
    {
        if (HandleGlobalInput(state, key))
        {
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.Enter when !state.IsGoToLineInput:
                state.CloseCoverageDetail();
                return true;

            case ConsoleKey.UpArrow:
                state.MoveCoverageDetailScroll(-1);
                return true;

            case ConsoleKey.DownArrow:
                state.MoveCoverageDetailScroll(1);
                return true;

            case ConsoleKey.PageUp:
                state.PageCoverageDetailScroll(-1);
                return true;

            case ConsoleKey.PageDown:
                state.PageCoverageDetailScroll(1);
                return true;

            case ConsoleKey.Home:
                state.CoverageDetailScroll = 0;
                return true;

            case ConsoleKey.End:
                state.CoverageDetailScroll = Math.Max(0, state.CoverageDetailLineCount - state.ListHeight);
                return true;

            case ConsoleKey.Q:
                return false;
        }

        return true;
    }

    private static bool HandleGoToLineInput(Views.DashboardState state, ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            state.IsGoToLineInput = false;
            state.GoToLineBuffer = string.Empty;
            return true;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            state.IsGoToLineInput = false;
            if (int.TryParse(state.GoToLineBuffer, out var lineNumber) && lineNumber > 0)
            {
                state.GoToCoverageLine(lineNumber);
            }
            else
            {
                state.FlashMessage = "Enter a valid line number.";
            }

            state.GoToLineBuffer = string.Empty;
            return true;
        }

        if (key.Key == ConsoleKey.Backspace && state.GoToLineBuffer.Length > 0)
        {
            state.GoToLineBuffer = state.GoToLineBuffer[..^1];
            return true;
        }

        if (char.IsDigit(key.KeyChar))
        {
            state.GoToLineBuffer += key.KeyChar;
        }

        return true;
    }

    private static void HandleEnter(Views.DashboardState state)
    {
        if (state.PrimaryView == Views.DashboardPrimaryView.Coverage)
        {
            var file = state.SelectedCoverageItem;
            if (file is not null)
            {
                state.OpenCoverageDetail(file, state.Data.Context.ProjectRoot);
            }

            return;
        }

        if (state.PrimaryView == Views.DashboardPrimaryView.Analyser)
        {
            var finding = state.SelectedAnalyserItem;
            if (finding is not null)
            {
                state.DetailAnalyserFinding = finding;
                state.DetailTest = null;
                state.Mode = Views.DashboardMode.Detail;
            }

            return;
        }

        var item = state.SelectedTestItem;
        if (item is null)
        {
            return;
        }

        if (item.Kind == TestTreeNodeKind.Test && item.Test is not null)
        {
            state.DetailTest = item.Test;
            state.DetailAnalyserFinding = null;
            state.Mode = Views.DashboardMode.Detail;
            return;
        }

        if (item.Children.Count > 0)
        {
            state.Navigator.DrillDown(item);
            state.ResetSelection();
        }
    }

    private static bool HandleSearchInput(Views.DashboardState state, ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            state.IsSearching = false;
            state.Navigator.SearchQuery = string.Empty;
            state.ResetSelection();
            return true;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            state.IsSearching = false;
            state.ResetSelection();
            return true;
        }

        if (key.Key == ConsoleKey.Backspace && state.Navigator.SearchQuery.Length > 0)
        {
            state.Navigator.SearchQuery = state.Navigator.SearchQuery[..^1];
            state.ResetSelection();
            return true;
        }

        if (!char.IsControl(key.KeyChar))
        {
            state.Navigator.SearchQuery += key.KeyChar;
            state.ResetSelection();
        }

        return true;
    }

    private static void TryCopy(Views.DashboardState state)
    {
        var text = state.GetCopyText();
        if (string.IsNullOrWhiteSpace(text))
        {
            state.FlashMessage = "Nothing to copy for the current selection.";
            return;
        }

        state.FlashMessage = ClipboardHelper.TryCopy(text)
            ? "Copied to clipboard."
            : "Could not access the clipboard on this system.";
    }

    private static void TryOpenTestFile(Views.DashboardState state)
    {
        if (state.DetailAnalyserFinding is { } detailFinding)
        {
            EditorLauncher.TryOpen(detailFinding.ScriptPath);
            state.FlashMessage = $"Opened {Path.GetFileName(detailFinding.ScriptPath)}.";
            return;
        }

        if (state.PrimaryView == Views.DashboardPrimaryView.Analyser && state.SelectedAnalyserItem is { } finding)
        {
            EditorLauncher.TryOpen(finding.ScriptPath);
            state.FlashMessage = $"Opened {Path.GetFileName(finding.ScriptPath)}.";
            return;
        }

        if (state.Mode == Views.DashboardMode.CoverageDetail && state.CoverageDetailSourcePath is { } sourcePath)
        {
            EditorLauncher.TryOpen(sourcePath);
            state.FlashMessage = $"Opened {Path.GetFileName(sourcePath)}.";
            return;
        }

        var testFile = state.GetSelectedTestFilePath();
        if (testFile is null)
        {
            state.FlashMessage = "No test file path available for the current selection.";
            return;
        }

        EditorLauncher.TryOpen(testFile);
        state.FlashMessage = $"Opened {Path.GetFileName(testFile)}.";
    }

    private static void TryExport(Views.DashboardState state)
    {
        if (state.Data.TestRun.Total == 0)
        {
            state.FlashMessage = "No test results to export.";
            return;
        }

        var (jsonPath, csvPath) = ResultsExporter.Export(state.Data);
        state.FlashMessage = $"Exported JSON and CSV to {Path.GetFileName(jsonPath)} / {Path.GetFileName(csvPath)}.";
    }

    private static int ExitCode(DashboardData data) =>
        data.TestRun.Failed > 0
            ? Math.Min(data.TestRun.Failed, data.Context.Options.MaxFailures)
            : data.TestRun.Total == 0 && data.RunOutput is not null
                ? 1
                : 0;

    private static void TryOpenCoverageHtml(Views.DashboardState state)
    {
        var htmlPath = state.Data.Coverage?.HtmlReportPath;
        if (string.IsNullOrWhiteSpace(htmlPath) || !File.Exists(htmlPath))
        {
            state.FlashMessage = "Coverage HTML report not available.";
            return;
        }

        ProcessLauncher.OpenPath(htmlPath);
        state.FlashMessage = "Opened coverage HTML report.";
    }
}
