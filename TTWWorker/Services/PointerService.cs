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
                // Формат обычно: x:123 y:456 screen:0 window:...
                var output = Run("bash", "-lc \"xdotool getmouselocation\"", out var code);
                if (code != 0) return null;

                var x = ParseColonValue(output, "x");
                var y = ParseColonValue(output, "y");
                return x.HasValue && y.HasValue ? (x.Value, y.Value) : null;
            }

            if (OperatingSystem.IsWindows())
            {
                var script = "$sig='[DllImport(\"user32.dll\")]public static extern bool GetCursorPos(out POINT lpPoint); public struct POINT{ public int X; public int Y; }'; Add-Type -MemberDefinition $sig -Name NativeWin -Namespace Native; $p=New-Object Native.NativeWin+POINT; [Native.NativeWin]::GetCursorPos([ref]$p) | Out-Null; Write-Output \"$($p.X),$($p.Y)\"";
                var output = Run("powershell", $"-NoProfile -Command \"{script}\"", out var code);
                if (code != 0) return null;

                var parts = output.Trim().Split(',');
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

    public string GetCaptureDiagnostic()
    {
        if (OperatingSystem.IsLinux())
        {
            _ = Run("bash", "-lc \"command -v xdotool >/dev/null 2>&1\"", out var cmdCode);
            if (cmdCode != 0) return "xdotool не найден в PATH";

            var output = Run("bash", "-lc \"xdotool getmouselocation\"", out var code, out var err);
            if (code != 0) return $"xdotool вернул код {code}: {err.Trim()}";
            if (!ParseColonValue(output, "x").HasValue || !ParseColonValue(output, "y").HasValue)
            {
                return "Не удалось распарсить x/y из xdotool";
            }

            return "ok";
        }

        return "ok";
    }

    public bool MoveTo(int x, int y)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                _ = Run("bash", $"-lc \"xdotool mousemove {x} {y}\"", out var code);
                return code == 0;
            }

            if (OperatingSystem.IsWindows())
            {
                var script = "$sig='[DllImport(\"user32.dll\")]public static extern bool SetCursorPos(int X,int Y);'; Add-Type -MemberDefinition $sig -Name NativeWin -Namespace Native; [Native.NativeWin]::SetCursorPos(" + x + "," + y + ") | Out-Null";
                _ = Run("powershell", $"-NoProfile -Command \"{script}\"", out var code);
                return code == 0;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static string Run(string fileName, string args, out int exitCode)
        => Run(fileName, args, out exitCode, out _);

    private static string Run(string fileName, string args, out int exitCode, out string stderr)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
        stderr = process?.StandardError.ReadToEnd() ?? string.Empty;
        process?.WaitForExit(3000);
        exitCode = process?.ExitCode ?? -1;
        return output;
    }

    private static int? ParseColonValue(string output, string key)
    {
        var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var token = parts.FirstOrDefault(x => x.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase));
        if (token is null) return null;

        var value = token[(key.Length + 1)..];
        return int.TryParse(value, out var result) ? result : null;
    }
}
