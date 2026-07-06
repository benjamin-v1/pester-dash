using PesterDash.Core.Configuration;
using PesterDash.Core.Interfaces;
using PesterDash.Core.Models;

namespace PesterDash.Core.Discovery;

/// <summary>
/// Discovers test and source files in a PowerShell project.
/// </summary>
public sealed class ProjectDiscoveryService : IProjectDiscovery
{
    private static readonly HashSet<string> DefaultIgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".vscode",
        "coverage",
        "artifacts",
        ".artifacts",
        "node_modules",
    };

    private static readonly HashSet<string> TestDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests",
        "test",
    };

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ps1",
        ".psm1",
        ".psd1",
    };

    /// <inheritdoc />
    public DiscoveryResult Discover(string projectRoot, PesterDashOptions options)
    {
        var root = Path.GetFullPath(projectRoot);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Project root not found: {root}");
        }

        var ignoredDirectories = BuildIgnoredSet(options);
        var testFiles = new List<string>();
        var sourceFiles = new List<string>();
        var totalScanned = 0;

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var file in Directory.EnumerateFiles(root, "*", enumerationOptions))
        {
            totalScanned++;

            if (IsUnderIgnoredDirectory(file, root, ignoredDirectories))
            {
                continue;
            }

            var extension = Path.GetExtension(file);

            if (!SourceExtensions.Contains(extension))
            {
                continue;
            }

            if (IsTestFile(file, root))
            {
                testFiles.Add(file);
            }
            else
            {
                sourceFiles.Add(file);
            }
        }

        testFiles.Sort(StringComparer.OrdinalIgnoreCase);
        sourceFiles.Sort(StringComparer.OrdinalIgnoreCase);

        return new DiscoveryResult
        {
            ProjectRoot = root,
            TestFiles = testFiles,
            SourceFiles = sourceFiles,
            TotalFilesScanned = totalScanned,
        };
    }

    private static HashSet<string> BuildIgnoredSet(PesterDashOptions options)
    {
        var ignored = new HashSet<string>(DefaultIgnoredDirectories, StringComparer.OrdinalIgnoreCase);

        foreach (var directory in options.IgnoredDirectories)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                ignored.Add(directory.Trim());
            }
        }

        return ignored;
    }

    private static bool IsUnderIgnoredDirectory(string filePath, string projectRoot, HashSet<string> ignoredDirectories)
    {
        var relative = Path.GetRelativePath(projectRoot, filePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var part in parts[..^1])
        {
            if (ignoredDirectories.Contains(part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTestFile(string filePath, string projectRoot)
    {
        var fileName = Path.GetFileName(filePath);

        if (fileName.EndsWith(".Tests.ps1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var relative = Path.GetRelativePath(projectRoot, filePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts[..^1].Any(TestDirectoryNames.Contains);
    }
}
