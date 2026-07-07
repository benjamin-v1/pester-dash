using System.Diagnostics;

namespace PesterDash.Cli.Utilities;

internal static class ProcessLauncher
{
    public static void OpenPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"Path not found: {path}", path);
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", path);
            return;
        }

        Process.Start("xdg-open", path);
    }
}
