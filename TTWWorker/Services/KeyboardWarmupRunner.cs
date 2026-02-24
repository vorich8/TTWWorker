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

        for (var minute = 1; minute <= minutes; minute++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var plan = BuildMinutePlan();
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Минута {minute}/{minutes}: Down={plan.DownCount}, L={(plan.HasL ? 1 : 0)}");

            for (var sec = 1; sec <= 60; sec++)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
        }

        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Сессия завершена.");
    }

    private MinutePlan BuildMinutePlan()
    {
        var downCount = _random.Next(0, 4); // 0..3
        var downSeconds = new HashSet<int>();

        while (downSeconds.Count < downCount)
        {
            downSeconds.Add(_random.Next(1, 60));
        }

        if (downSeconds.Count == 0)
        {
            downSeconds.Add(60); // обязательное нажатие если за минуту не было
        }

        var hasL = _random.Next(0, 2) == 1;
        var lSecond = hasL ? _random.Next(1, 60) : -1;

        if (hasL && downSeconds.Contains(lSecond))
        {
            lSecond = 59;
        }

        return new MinutePlan(downSeconds, downSeconds.Count, hasL, lSecond);
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

    private sealed record MinutePlan(HashSet<int> DownSeconds, int DownCount, bool HasL, int LSecond);
}
