using TTWWorker.Models;

namespace TTWWorker.Services;

public class MetricsService(JsonStorage storage)
{
    public void Add(MetricsSnapshot snapshot)
    {
        var items = storage.LoadMetrics();
        items.Add(snapshot);
        storage.SaveMetrics(items.OrderBy(x => x.CapturedAt).ToList());
    }

    public IReadOnlyList<MetricsSnapshot> GetLastDays(int days)
    {
        var from = DateTime.UtcNow.AddDays(-Math.Abs(days));
        return storage.LoadMetrics().Where(x => x.CapturedAt >= from).OrderBy(x => x.CapturedAt).ToList();
    }

    public static double EngagementRate(MetricsSnapshot snapshot)
    {
        if (snapshot.Views <= 0) return 0;
        return ((double)(snapshot.Likes + snapshot.Comments + snapshot.Shares) / snapshot.Views) * 100d;
    }

    public static double RetentionRate(MetricsSnapshot snapshot)
    {
        if (snapshot.VideoLengthSeconds <= 0) return 0;
        return (snapshot.AvgWatchSeconds / snapshot.VideoLengthSeconds) * 100d;
    }
}
