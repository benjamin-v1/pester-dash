using System.Diagnostics;

namespace PesterDash.Cli.Utilities;

internal static class ClipboardHelper
{
    public static bool TryCopy(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                return CopyWindows(text);
            }

            if (OperatingSystem.IsMacOS())
            {
                return RunCopyProcess("pbcopy", text);
            }

            if (RunCopyProcess("wl-copy", text) || RunCopyProcess("xclip", text, "-selection", "clipboard"))
            {
                return true;
            }

            return RunCopyProcess("xsel", text, "--clipboard", "--input");
        }
        catch
        {
            return false;
        }
    }

    private static bool CopyWindows(string text)
    {
        var startInfo = new ProcessStartInfo("cmd.exe", "/c clip")
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.StandardInput.Write(text);
        process.StandardInput.Close();
        process.WaitForExit(3000);
        return process.ExitCode == 0;
    }

    private static bool RunCopyProcess(string fileName, string text, params string[] args)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.StandardInput.Write(text);
        process.StandardInput.Close();
        process.WaitForExit(3000);
        return process.ExitCode == 0;
    }
}
