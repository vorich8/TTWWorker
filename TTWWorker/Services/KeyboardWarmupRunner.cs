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

        var altTabEnabled = profile.AltTabIntervalSeconds > 0;
        var clickDelayMs = Math.Max(1, profile.ClickDelayMilliseconds);
        var configuredTiming = profile.ClickTiming;
        var effectiveTiming = configuredTiming == ClickTimingMode.AfterAltTab && !altTabEnabled
            ? ClickTimingMode.AfterDown
            : configuredTiming;

        var altTabMode = altTabEnabled
            ? $"каждые {profile.AltTabIntervalSeconds} сек"
            : "выключен";
        var timingLabel = effectiveTiming == ClickTimingMode.AfterAltTab ? "после Alt+Tab" : "после Down";
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Down: {profile.DownMinIntervalSeconds}-{profile.DownMaxIntervalSeconds} сек, Alt+Tab: {altTabMode}, клики: {timingLabel}, задержка {clickDelayMs}мс");

        if (configuredTiming == ClickTimingMode.AfterAltTab && !altTabEnabled)
        {
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Click timing 'после Alt+Tab' невозможен при Alt+Tab=0, применяю 'после Down'.");
        }

        var nextDownAt = NextDownSecond(0, profile);
        var nextAltTabAt = altTabEnabled ? profile.AltTabIntervalSeconds : int.MaxValue;
        var downPressedAfterLastAltTab = false;
        var altTabCounter = 0;
        var downCounter = 0;

        for (var sec = 1; sec <= totalSeconds; sec++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sec == nextAltTabAt)
            {
                PressAltTab();
                altTabCounter++;
                if (effectiveTiming == ClickTimingMode.AfterAltTab)
                {
                    ExecuteClick(profile, altTabCounter, clickDelayMs);
                }
                downPressedAfterLastAltTab = false;
                nextAltTabAt += profile.AltTabIntervalSeconds;
            }

            if (sec == nextDownAt)
            {
                PressDown();
                downCounter++;
                if (effectiveTiming == ClickTimingMode.AfterDown)
                {
                    ExecuteClick(profile, downCounter, clickDelayMs);
                }
                downPressedAfterLastAltTab = true;
                nextDownAt = NextDownSecond(sec, profile);
            }

            if (altTabEnabled && !downPressedAfterLastAltTab && sec == nextAltTabAt - 1)
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

    private void ExecuteClick(KeyboardProfile profile, int clickCounter, int clickDelayMs)
    {
        if (profile.AltTabClickPoints.Count == 0)
        {
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Пиксели не настроены: клик пропущен.");
            return;
        }

        var point = profile.AltTabClickPoints[(clickCounter - 1) % profile.AltTabClickPoints.Count];
        var ok = _pointerService.LeftClick(point.X, point.Y, Math.Max(1, point.ClickCount), clickDelayMs);
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Mouse click x={point.X} y={point.Y} count={point.ClickCount} delay={clickDelayMs}ms {(ok ? "sent" : "failed")}");
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
