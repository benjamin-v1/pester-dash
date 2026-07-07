using System.Diagnostics;

namespace PesterDash.Cli.Utilities;

internal static class EditorLauncher
{
    public static bool TryOpen(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (TryStart("code", "-r", path) || TryStart("code", path))
        {
            return true;
        }

        ProcessLauncher.OpenPath(path);
        return true;
    }

    private static bool TryStart(string fileName, params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }
}
