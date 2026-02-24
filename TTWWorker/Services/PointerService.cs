using System.Diagnostics;

namespace TTWWorker.Services;

public class PointerService
{
    public (int X, int Y)? CaptureCurrentPosition()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var script = "$sig='[DllImport(\"user32.dll\")]public static extern bool GetCursorPos(out POINT lpPoint); public struct POINT{ public int X; public int Y; }'; Add-Type -MemberDefinition $sig -Name NativeWin -Namespace Native; $p=New-Object Native.NativeWin+POINT; [Native.NativeWin]::GetCursorPos([ref]$p) | Out-Null; Write-Output \"$($p.X),$($p.Y)\"";
            var output = Run("powershell", $"-NoProfile -Command \"{script}\"", out var code);
            if (code != 0) return null;

            var parts = output.Trim().Split(',');
            return parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y)
                ? (x, y)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public string GetCaptureDiagnostic()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "Поддерживается только Windows 11";
        }

        var output = Run("powershell", "-NoProfile -Command \"Write-Output ok\"", out var code, out var err);
        if (code != 0)
        {
            return $"PowerShell недоступен: {err.Trim()}";
        }

        return output.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase)
            ? "ok"
            : "PowerShell не вернул ожидаемый ответ";
    }

    public bool MoveTo(int x, int y)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var script = "$sig='[DllImport(\"user32.dll\")]public static extern bool SetCursorPos(int X,int Y);'; Add-Type -MemberDefinition $sig -Name NativeWin -Namespace Native; [Native.NativeWin]::SetCursorPos(" + x + "," + y + ") | Out-Null";
            _ = Run("powershell", $"-NoProfile -Command \"{script}\"", out var code);
            return code == 0;
        }
        catch
        {
            return false;
        }
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
}
