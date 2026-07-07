namespace PesterDash.Cli.Utilities;

internal static class SourceFileResolver
{
    private static readonly string[] IgnoredDirectoryNames =
    [
        ".artifacts",
        ".git",
        "node_modules",
        "bin",
        "obj",
    ];

    public static string? Find(string projectRoot, string fileName)
    {
        var direct = Path.Combine(projectRoot, fileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        foreach (var path in Directory.EnumerateFiles(projectRoot, fileName, SearchOption.AllDirectories))
        {
            if (IsIgnored(path, projectRoot))
            {
                continue;
            }

            return path;
        }

        return null;
    }

    private static bool IsIgnored(string path, string projectRoot)
    {
        var relative = Path.GetRelativePath(projectRoot, path);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
