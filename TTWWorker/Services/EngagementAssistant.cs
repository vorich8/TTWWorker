namespace TTWWorker.Services;

public class EngagementAssistant
{
    public IReadOnlyList<string> BuildChecklist(TimeSpan sessionDuration)
    {
        var totalMinutes = Math.Max((int)sessionDuration.TotalMinutes, 5);

        return
        [
            $"Откройте TikTok и проведите в ленте рекомендаций {Math.Min(totalMinutes, 10)}-10 минут без резких серий действий.",
            "Сосредоточьтесь на просмотре релевантных роликов и переходах по рекомендациям.",
            "Не выполняйте лайки и комментарии в автоматическом режиме.",
            "Откройте уведомления и оцените входящую активность без ответов от бота.",
            "Зафиксируйте, какие темы роликов дали лучший отклик, и добавьте их в контент-план."
        ];
    }
}
