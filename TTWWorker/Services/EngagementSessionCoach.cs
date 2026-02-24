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
            $"Блок 2 ({block2} мин): автоскролл ленты рекомендаций без взаимодействий.",
            $"Блок 3 ({block3} мин): продолжайте просмотр релевантной ленты.",
            $"Блок 4 ({block4} мин): откройте уведомления и анализируйте отклик без действий."
        ];
    }



    public IReadOnlyList<SessionStep> BuildAutoPlan(TimeSpan duration)
    {
        var total = Math.Max(8, (int)duration.TotalMinutes);
        var feedPassOne = Math.Max(3, total / 3);
        var feedPassTwo = Math.Max(2, total / 3);
        var discover = Math.Max(2, total / 5);
        var notifications = Math.Max(1, total - feedPassOne - feedPassTwo - discover);

        return
        [
            new SessionStep("Лента рекомендаций — проход 1", feedPassOne, "Автоскролл ленты рекомендаций", "https://www.tiktok.com/foryou"),
            new SessionStep("Лента рекомендаций — проход 2", feedPassTwo, "Автоскролл без лайков и комментариев", "https://www.tiktok.com/foryou"),
            new SessionStep("Раздел интересов", discover, "Переход по рекомендациям и просмотр", "https://www.tiktok.com/explore"),
            new SessionStep("Уведомления", notifications, "Просмотр входящих уведомлений без отправки действий", "https://www.tiktok.com/notifications")
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
