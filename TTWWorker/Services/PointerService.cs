using System.Diagnostics;

namespace TTWWorker.Services;

public class PointerService
{
    public (int X, int Y)? CaptureCurrentPosition()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-lc \"xdotool getmouselocation --shell\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                process?.WaitForExit(3000);
                if (process is null || process.ExitCode != 0) return null;

                var x = ParseShellValue(output, "X");
                var y = ParseShellValue(output, "Y");
                return x.HasValue && y.HasValue ? (x.Value, y.Value) : null;
            }

            if (OperatingSystem.IsWindows())
            {
                var script = "$sig='[DllImport(\"user32.dll\")]public static extern bool GetCursorPos(out POINT lpPoint); public struct POINT{ public int X; public int Y; }'; Add-Type -MemberDefinition $sig -Name NativeWin -Namespace Native; $p=New-Object Native.NativeWin+POINT; [Native.NativeWin]::GetCursorPos([ref]$p) | Out-Null; Write-Output \"$($p.X),$($p.Y)\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var output = process?.StandardOutput.ReadToEnd().Trim() ?? string.Empty;
                process?.WaitForExit(3000);
                if (process is null || process.ExitCode != 0) return null;

                var parts = output.Split(',');
                return parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y)
                    ? (x, y)
                    : null;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public bool MoveTo(int x, int y)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-lc \"xdotool mousemove {x} {y}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(3000);
                return process is { ExitCode: 0 };
            }

            if (OperatingSystem.IsWindows())
            {
                var script = "$sig='[DllImport(\"user32.dll\")]public static extern bool SetCursorPos(int X,int Y);'; Add-Type -MemberDefinition $sig -Name NativeWin -Namespace Native; [Native.NativeWin]::SetCursorPos(" + x + "," + y + ") | Out-Null";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(3000);
                return process is { ExitCode: 0 };
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static int? ParseShellValue(string output, string key)
    {
        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(x => x.StartsWith($"{key}="));
        if (line is null) return null;

        var value = line[(key.Length + 1)..].Trim();
        return int.TryParse(value, out var result) ? result : null;
    }
}
