using System.Diagnostics;

namespace TTWWorker.Services;

public class KeyboardWarmupRunner
{
    private readonly Random _random = new();

    public async Task RunAsync(int minutes, CancellationToken cancellationToken)
    {
        minutes = Math.Max(1, minutes);
        OpenTikTok();
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] TikTok открыт. Длительность сессии: {minutes} мин.");

        var lUsedInCurrent4MinBlock = false;

        for (var minute = 1; minute <= minutes; minute++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var minuteInBlock = ((minute - 1) % 4) + 1;
            var mustPressLThisMinute = minuteInBlock == 4 && !lUsedInCurrent4MinBlock;

            var plan = BuildMinutePlan(mustPressLThisMinute);
            lUsedInCurrent4MinBlock = lUsedInCurrent4MinBlock || plan.HasL;

            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Минута {minute}/{minutes}: AltTab=1, Down={plan.DownCount}, L={(plan.HasL ? 1 : 0)}");

            for (var sec = 1; sec <= 60; sec++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (plan.AltTabSecond == sec)
                {
                    PressAltTab();
                }

                if (plan.DownSeconds.Contains(sec))
                {
                    PressDown();
                }

                if (plan.LSecond == sec)
                {
                    PressL();
                }

                await Task.Delay(1000, cancellationToken);
            }

            if (minuteInBlock == 4)
            {
                lUsedInCurrent4MinBlock = false;
            }
        }

        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Сессия завершена.");
    }

    private MinutePlan BuildMinutePlan(bool mustPressL)
    {
        var downCount = _random.Next(0, 4); // 0..3
        var downSeconds = new HashSet<int>();

        while (downSeconds.Count < downCount)
        {
            downSeconds.Add(_random.Next(1, 61));
        }

        if (downSeconds.Count == 0)
        {
            downSeconds.Add(60); // обязательное нажатие если за минуту не было
        }

        // L реже: базово ~15% минут, но принудительно в 4-й минуте блока, если до этого не было.
        var hasL = mustPressL || _random.NextDouble() < 0.15;
        var lSecond = hasL ? _random.Next(1, 61) : -1;

        // Alt+Tab ровно один раз в минуту, обязательно.
        var altTabSecond = _random.Next(1, 61);

        return new MinutePlan(downSeconds, downSeconds.Count, hasL, lSecond, altTabSecond);
    }

    private void OpenTikTok()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.tiktok.com/foryou",
            UseShellExecute = true
        });
    }

    private void PressDown()
    {
        var ok = SendKey("{DOWN}", "Down");
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Key Down {(ok ? "sent" : "failed")}");
    }

    private void PressL()
    {
        var ok = SendKey("l", "l");
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Key L {(ok ? "sent" : "failed")}");
    }

    private void PressAltTab()
    {
        var ok = SendAltTab();
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Key Alt+Tab {(ok ? "sent" : "failed")}");
    }

    private static bool SendKey(string windowsKey, string linuxKey)
    {
        try
        {
            if (OperatingSystem.IsWindows())
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

            if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-lc \"xdotool key {linuxKey}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
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

    private static bool SendAltTab()
    {
        try
        {
            if (OperatingSystem.IsWindows())
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

            if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-lc \"xdotool key alt+Tab\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
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

    private sealed record MinutePlan(HashSet<int> DownSeconds, int DownCount, bool HasL, int LSecond, int AltTabSecond);
}
