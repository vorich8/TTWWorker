using TTWWorker.Models;

namespace TTWWorker.Services;

public class DraftGenerator
{
    public IReadOnlyList<DraftVariant> Generate(string topic, string tone, int count)
    {
        var templates = new[]
        {
            $"{topic}: коротко показываю шаги и что реально сработало.",
            $"Разбираю тему '{topic}' без воды: бери и применяй сегодня.",
            $"Мой мини-эксперимент по теме '{topic}' — результаты в конце видео."
        };

        var toneSuffix = tone.ToLowerInvariant() switch
        {
            "экспертный" => " Сохрани, чтобы не потерять.",
            "развлекательный" => " Если было полезно — ставь 🔥.",
            _ => " Напиши, сделать продолжение?"
        };

        return Enumerable.Range(0, Math.Max(count, 1))
            .Select(i => templates[i % templates.Length] + toneSuffix)
            .Select(description => new DraftVariant(
                description,
                BuildHashtags(topic)))
            .ToList();
    }

    private static IReadOnlyList<string> BuildHashtags(string topic)
    {
        var normalized = topic.Replace(' ', '_').ToLowerInvariant();
        return [$"#{normalized}", "#tiktoktips", "#contentcreator", "#smm", "#продвижение"];
    }
}
