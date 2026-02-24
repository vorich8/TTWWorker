using System.Text;

namespace TTWWorker.Services;

public class ReportService(MetricsService metricsService, PublicationPlanner publicationPlanner, JsonStorage storage)
{
    public string BuildWeeklyReport()
    {
        var snapshots = metricsService.GetLastDays(7);
        var plans = publicationPlanner.GetUpcoming();

        var avgEr = snapshots.Count == 0 ? 0 : snapshots.Average(MetricsService.EngagementRate);
        var avgRetention = snapshots.Count == 0 ? 0 : snapshots.Average(MetricsService.RetentionRate);
        var totalViews = snapshots.Sum(x => x.Views);

        var sb = new StringBuilder();
        sb.AppendLine("# Недельный отчёт TikTok");
        sb.AppendLine();
        sb.AppendLine($"- Срезов метрик: {snapshots.Count}");
        sb.AppendLine($"- Суммарные просмотры: {totalViews}");
        sb.AppendLine($"- Средний ER: {avgEr:F2}%");
        sb.AppendLine($"- Среднее удержание: {avgRetention:F2}%");
        sb.AppendLine();
        sb.AppendLine("## Ближайшие публикации");

        foreach (var p in plans.Take(5))
        {
            sb.AppendLine($"- {p.ScheduledAt:g} | {p.Topic} | {p.Format}");
        }

        if (!plans.Any())
        {
            sb.AppendLine("- Пусто");
        }

        return storage.SaveReport(sb.ToString());
    }
}
