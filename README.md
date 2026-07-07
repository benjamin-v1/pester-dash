# PesterDash

Cross-platform CLI for discovering, running, and visualizing Pester tests.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or compatible SDK)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell) (`pwsh`)
- [Pester 5+](https://pester.dev/)
- [PSScriptAnalyzer](https://github.com/PowerShell/PSScriptAnalyzer) (optional, for the Analyser tab)

## Quick start

```bash
dotnet build
dotnet run --project src/PesterDash.Cli -- open .
dotnet run --project src/PesterDash.Cli -- run .
```

`open` loads the last run instantly — no test execution. `run` executes tests and opens the dashboard.

## Commands

| Command | Description |
|---------|-------------|
| `open <project-root>` | Open dashboard without running tests |
| `run <project-root>` | Run tests once, then open dashboard |
| `watch <project-root>` | Open dashboard with watch mode on |
| `scope <project-root>` | Configure which test/source files to include |
| `report <project-root>` | Generate HTML reports (no dashboard) |
| `clean <project-root>` | Delete artefact directory |

## Project store

PesterDash keeps project state under `.pester-dash/`:

```text
.pester-dash/
  config.json       # settings + run scope
  last-run.json     # snapshot for instant open
  results/          # TestResults.xml, coverage, exports
  logs/             # pester-output.log, debug log
  analyser/         # PSScriptAnalyzer JSON
```

Legacy `pesterdash.json` at the project root is still read and migrated on save.

## Configuration

Optional `.pester-dash/config.json` (or legacy `pesterdash.json`):

```json
{
  "coverage": true,
  "analyser": true,
  "watch": false,
  "parallel": true,
  "maxFailures": 100,
  "outputDirectory": ".pester-dash/results",
  "runScope": {
    "testFiles": ["tests/MyModule.Tests.ps1"],
    "sourceFiles": ["src/MyModule.ps1"]
  }
}
```

## Dashboard

Full-screen TUI with Tests, Coverage, and Analyser tabs.

### Run actions

| Key | Action |
|-----|--------|
| `F5` | Run all tests in scope |
| `a` | Run PSScriptAnalyzer only (keeps existing test results) |
| `r` | Rerun last filter / scope |
| `x` | Run current tree selection (file, describe, context, or It) |
| `s` | Edit run scope |
| `w` | Toggle watch mode |

### Navigation

| Key | Action |
|-----|--------|
| `Tab` / `t` | Switch Tests / Coverage / Analyser |
| `↑↓` / PgUp/PgDn | Move or scroll |
| `Enter` | Drill in, test detail, coverage file, or open analyser finding |
| `Esc` | Back |
| `f` | Failures-only filter (Tests tab) |
| `/` | Search |
| `l` | View Pester stdout/stderr log |
| `c` | Copy to clipboard |
| `v` | Open file in editor |
| `e` | Export JSON + CSV |
| `h` | Open coverage HTML report |
| `o` | Open artefact folder |
| `?` | Help |
| `q` | Quit |

Use `--debug` on `run` when Pester errors disappear in the UI — full output goes to `.pester-dash/logs/pester-debug.log`.

## CI mode

```bash
dotnet run --project src/PesterDash.Cli -- run . --ci
dotnet run --project src/PesterDash.Cli -- run . --no-coverage --no-analyser --ci
```

## Sample project

```bash
dotnet run --project src/PesterDash.Cli -- run samples/SampleModule
dotnet run --project src/PesterDash.Cli -- open samples/SampleModule
```
