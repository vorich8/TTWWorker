using System.Diagnostics;
using TTWWorker.Models;

namespace TTWWorker.Services;

public class BotWarmupRunner
{
    public async Task RunAsync(IReadOnlyList<SessionStep> steps, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Старт сессии. Шагов: {steps.Count}");

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Шаг: {step.Name}");
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Действие: {step.Action}");
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] URL: {step.TargetUrl}");

            OpenUrl(step.TargetUrl);
            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Браузерный переход выполнен.");

            var seconds = Math.Max(step.DurationMinutes * 60, 30);
            for (var elapsed = 0; elapsed < seconds; elapsed++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (elapsed == 0 || elapsed % 15 == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] [BOT] Прогресс {step.Name}: {elapsed}/{seconds} сек");
                }

                await Task.Delay(1000, cancellationToken);
            }

            Console.WriteLine($"[{DateTime.Now:T}] [BOT] Шаг завершен: {step.Name}");
        }

        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Сессия завершена. Длительность: {(DateTime.UtcNow - started).TotalMinutes:F1} мин");
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
