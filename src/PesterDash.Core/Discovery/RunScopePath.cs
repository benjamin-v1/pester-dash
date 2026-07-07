namespace PesterDash.Core.Discovery;

/// <summary>Normalizes project-relative paths for run scope persistence.</summary>
public static class RunScopePath
{
    public static string ToRelative(string projectRoot, string fullPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(projectRoot), Path.GetFullPath(fullPath));
        return relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    public static string ToAbsolute(string projectRoot, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Path.GetFullPath(projectRoot), relativePath));
    }

    public static bool Equals(string left, string right) =>
        string.Equals(
            Normalize(left),
            Normalize(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public static string Normalize(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}
