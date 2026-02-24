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
        var minutes = Math.Max(5, (int)duration.TotalMinutes);
        var block = Math.Max(2, minutes / 4);

        return
        [
            $"Блок 1 ({block} мин): спокойно смотрите релевантные ролики до конца.",
            $"Блок 2 ({block} мин): вручную лайкните только действительно понравившиеся видео (1-3 шт).",
            $"Блок 3 ({block} мин): вручную оставьте 1 содержательный комментарий по теме.",
            $"Блок 4 ({minutes - (block * 3)} мин): проверьте уведомления и ответьте подписчикам."
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
