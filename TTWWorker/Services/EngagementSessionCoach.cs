using System.Diagnostics;
using TTWWorker.Models;

namespace TTWWorker.Services;

public class EngagementSessionCoach(JsonStorage storage)
{
    public bool OpenTikTokInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tiktok.com/foryou",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> BuildLowEffortPlan(TimeSpan duration)
    {
        var minutes = Math.Max(8, (int)duration.TotalMinutes);
        var block1 = Math.Max(2, minutes / 4);
        var block2 = Math.Max(2, minutes / 4);
        var block3 = Math.Max(2, minutes / 4);
        var block4 = Math.Max(1, minutes - block1 - block2 - block3);

        return
        [
            $"Блок 1 ({block1} мин): спокойно смотрите релевантные ролики до конца.",
            $"Блок 2 ({block2} мин): точечно лайкните релевантные видео (1-3 шт).",
            $"Блок 3 ({block3} мин): оставьте 1 содержательный комментарий по теме.",
            $"Блок 4 ({block4} мин): проверьте уведомления и ответьте подписчикам."
        ];
    }



    public IReadOnlyList<SessionStep> BuildAutoPlan(TimeSpan duration)
    {
        var total = Math.Max(8, (int)duration.TotalMinutes);
        var watch = Math.Max(3, total / 3);
        var like = Math.Max(2, total / 4);
        var comment = Math.Max(2, total / 4);
        var notifications = Math.Max(1, total - watch - like - comment);

        return
        [
            new SessionStep("Лента рекомендаций", watch, "Скролл и просмотр релевантных видео", "https://www.tiktok.com/foryou"),
            new SessionStep("Взаимодействие", like, "Точечные лайки в рамках лимита", "https://www.tiktok.com/foryou"),
            new SessionStep("Комментарии", comment, "Короткие комментарии по шаблонам", "https://www.tiktok.com/foryou"),
            new SessionStep("Уведомления", notifications, "Ответы аудитории и проверка отклика", "https://www.tiktok.com/notifications")
        ];
    }
    public void SaveSession(EngagementSessionLog log)
    {
        var sessions = storage.LoadEngagementSessions();
        sessions.Add(log);
        storage.SaveEngagementSessions(sessions.OrderByDescending(x => x.StartedAt).ToList());
    }

    public IReadOnlyList<EngagementSessionLog> GetRecentSessions(int count = 10)
        => storage.LoadEngagementSessions().Take(Math.Max(1, count)).ToList();
}
