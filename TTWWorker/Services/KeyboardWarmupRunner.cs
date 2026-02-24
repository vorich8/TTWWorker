using System.Diagnostics;

namespace TTWWorker.Services;

public class KeyboardWarmupRunner
{
    private readonly Random _random = new();

    public async Task RunAsync(int minutes, CancellationToken cancellationToken)
    {
        minutes = Math.Max(1, minutes);
        var totalSeconds = minutes * 60;

        OpenTikTok();
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Окно открыто. Длительность сессии: {minutes} мин ({totalSeconds} сек).");

        // Down: каждые 7-20 секунд.
        var nextDownAt = NextDownSecond(0);

        // Alt+Tab: каждые 30 секунд.
        var nextAltTabAt = 30;

        // L: реже, но минимум 1 раз в каждом блоке 4 минут.
        var lUsedInCurrent4MinBlock = false;
        var nextLAt = BuildLSecondForCurrentBlock(0, totalSeconds, forceByBlockEnd: false);

        var downPressedAfterLastAltTab = false;

        for (var sec = 1; sec <= totalSeconds; sec++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentBlockStart = ((sec - 1) / 240) * 240;
            var currentBlockEnd = Math.Min(currentBlockStart + 240, totalSeconds);

            // Если подошли к концу 4-минутного блока без L — форсим L в оставшееся время.
            if (!lUsedInCurrent4MinBlock && sec >= currentBlockEnd - 30 && (nextLAt < sec || nextLAt > currentBlockEnd))
            {
                nextLAt = _random.Next(sec, currentBlockEnd + 1);
            }

            if (sec == nextAltTabAt)
            {
                PressAltTab();
                downPressedAfterLastAltTab = false;
                nextAltTabAt += 30;
            }

            if (sec == nextDownAt)
            {
                PressDown();
                downPressedAfterLastAltTab = true;
                nextDownAt = NextDownSecond(sec);
            }

            if (sec == nextLAt)
            {
                PressL();
                lUsedInCurrent4MinBlock = true;
                nextLAt = BuildLSecondForCurrentBlock(sec, totalSeconds, forceByBlockEnd: false);
            }

            // Гарантия: после Alt+Tab до следующего Alt+Tab должен быть минимум один Down.
            if (!downPressedAfterLastAltTab && sec == nextAltTabAt - 1)
            {
                PressDown();
                downPressedAfterLastAltTab = true;
                nextDownAt = NextDownSecond(sec);
            }

            if (sec % 240 == 0)
            {
                lUsedInCurrent4MinBlock = false;
            }

            if (sec % 30 == 0)
            {
                Console.WriteLine($"[{DateTime.Now:T}] [BOT] Прогресс: {sec}/{totalSeconds} сек");
            }

            await Task.Delay(1000, cancellationToken);
        }

        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Сессия завершена.");
    }

    private int NextDownSecond(int currentSecond)
    {
        var offset = _random.Next(7, 21); // 7-20
        return currentSecond + offset;
    }

    private int BuildLSecondForCurrentBlock(int currentSecond, int totalSeconds, bool forceByBlockEnd)
    {
        var blockStart = (currentSecond / 240) * 240;
        var blockEnd = Math.Min(blockStart + 240, totalSeconds);

        if (blockEnd <= currentSecond)
        {
            return int.MaxValue;
        }

        if (forceByBlockEnd)
        {
            return _random.Next(currentSecond + 1, blockEnd + 1);
        }

        // Реже: ~10% вероятность в блоке на старте, иначе ждем возможного форса ближе к концу.
        var shouldPress = _random.NextDouble() < 0.10;
        if (!shouldPress)
        {
            return int.MaxValue;
        }

        return _random.Next(currentSecond + 1, blockEnd + 1);
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
}
