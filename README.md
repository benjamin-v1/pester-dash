# PesterDash

Cross-platform CLI for discovering, running, and visualizing Pester tests.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or compatible SDK)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell) (`pwsh`) — required for test execution (coming soon)
- [Pester 5+](https://pester.dev/) — required for test execution (coming soon)

## Quick start

```bash
dotnet build
dotnet run --project src/PesterDash.Cli -- --help
dotnet run --project src/PesterDash.Cli -- run .
```

## Commands

| Command | Description |
|---------|-------------|
| `run <project-root>` | Run tests once |
| `watch <project-root>` | Watch for changes and rerun |
| `report <project-root>` | Generate HTML reports (no dashboard) |
| `clean <project-root>` | Delete artefact directory |

## Configuration

Optional `pesterdash.json` in the project root (or any parent directory):

```json
{
  "coverage": true,
  "watch": false,
  "parallel": true,
  "maxFailures": 100,
  "outputDirectory": ".artifacts",
  "ignoredDirectories": ["docs", "scripts"]
}
```

## Status

**Phase 1 complete:** solution scaffold, DI host, config loading, stub commands.

Next up: project discovery and Pester execution.
