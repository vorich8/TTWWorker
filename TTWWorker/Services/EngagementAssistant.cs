namespace TTWWorker.Services;

public class EngagementAssistant
{
    public IReadOnlyList<string> BuildChecklist(TimeSpan sessionDuration)
    {
        var totalMinutes = Math.Max((int)sessionDuration.TotalMinutes, 5);

        return
        [
            $"Откройте TikTok и проведите в ленте рекомендаций {Math.Min(totalMinutes, 10)}-10 минут без резких серий действий.",
            "Выберите 3-5 релевантных видео и взаимодействуйте вручную: досмотрите, при желании поставьте лайк.",
            "Оставьте 1-2 осмысленных комментария вручную только там, где действительно есть что добавить.",
            "Зайдите в уведомления и ответьте на комментарии аудитории по существу.",
            "Зафиксируйте, какие темы роликов дали лучший отклик, и добавьте их в контент-план."
        ];
    }
}
