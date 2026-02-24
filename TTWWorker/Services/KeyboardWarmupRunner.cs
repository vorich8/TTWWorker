using System.Diagnostics;
using TTWWorker.Models;

namespace TTWWorker.Services;

public class KeyboardWarmupRunner
{
    private readonly Random _random = new();
    private readonly PointerService _pointerService = new();

    public async Task RunAsync(KeyboardProfile profile, int minutes, CancellationToken cancellationToken)
    {
        minutes = Math.Max(1, minutes);
        var totalSeconds = minutes * 60;

        OpenWindow();
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Профиль: {profile.Name}");
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Down: {profile.DownMinIntervalSeconds}-{profile.DownMaxIntervalSeconds} сек, Alt+Tab: {profile.AltTabIntervalSeconds} сек");

        var nextDownAt = NextDownSecond(0, profile);
        var nextAltTabAt = Math.Max(1, profile.AltTabIntervalSeconds);
        var downPressedAfterLastAltTab = false;
        var altTabCounter = 0;

        for (var sec = 1; sec <= totalSeconds; sec++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sec == nextAltTabAt)
            {
                PressAltTab();
                altTabCounter++;
                ClickForAltTab(profile, altTabCounter);
                downPressedAfterLastAltTab = false;
                nextAltTabAt += Math.Max(1, profile.AltTabIntervalSeconds);
            }

            if (sec == nextDownAt)
            {
                PressDown();
                downPressedAfterLastAltTab = true;
                nextDownAt = NextDownSecond(sec, profile);
            }

            if (!downPressedAfterLastAltTab && sec == nextAltTabAt - 1)
            {
                PressDown();
                downPressedAfterLastAltTab = true;
                nextDownAt = NextDownSecond(sec, profile);
            }

            if (sec % 30 == 0)
            {
                Console.WriteLine($"[{DateTime.Now:T}] [BOT] Прогресс: {sec}/{totalSeconds} сек");
            }

            await Task.Delay(1000, cancellationToken);
        }

        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Сессия завершена.");
    }

    private int NextDownSecond(int currentSecond, KeyboardProfile profile)
    {
        var min = Math.Max(1, profile.DownMinIntervalSeconds);
        var max = Math.Max(min, profile.DownMaxIntervalSeconds);
        return currentSecond + _random.Next(min, max + 1);
    }

    private void ClickForAltTab(KeyboardProfile profile, int altTabCounter)
    {
        if (profile.AltTabClickPoints.Count == 0)
        {
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Пиксели не настроены: клик пропущен.");
            return;
        }

        var point = profile.AltTabClickPoints[(altTabCounter - 1) % profile.AltTabClickPoints.Count];
        var ok = _pointerService.LeftClick(point.X, point.Y, Math.Max(1, point.ClickCount));
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Mouse click x={point.X} y={point.Y} count={point.ClickCount} {(ok ? "sent" : "failed")}");
    }

    private void OpenWindow()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.tiktok.com/foryou",
            UseShellExecute = true
        });
    }

    private void PressDown()
    {
        var ok = SendKey("{DOWN}");
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Key Down {(ok ? "sent" : "failed")}");
    }

    private void PressAltTab()
    {
        var ok = SendAltTab();
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Key Alt+Tab {(ok ? "sent" : "failed")}");
    }

    private static bool SendKey(string windowsKey)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"$wshell = New-Object -ComObject wscript.shell; Start-Sleep -Milliseconds 50; $wshell.SendKeys('{windowsKey}')\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private static bool SendAltTab()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"$wshell = New-Object -ComObject wscript.shell; Start-Sleep -Milliseconds 50; $wshell.SendKeys('%{TAB}')\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }
}
