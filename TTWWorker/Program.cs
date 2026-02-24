using TTWWorker;
using TTWWorker.Services;
using TTWWorker.Models;

var storage = new JsonStorage();
var planner = new PublicationPlanner(storage);
var draftGenerator = new DraftGenerator();
var metricsService = new MetricsService(storage);
var reportService = new ReportService(metricsService, planner, storage);
var engagementAssistant = new EngagementAssistant();
var engagementCoach = new EngagementSessionCoach(storage);

Console.WriteLine("TTWWorker — безопасная автоматизация контент-операций");
Console.WriteLine("1) План публикаций");
Console.WriteLine("2) Черновики описаний/хэштегов");
Console.WriteLine("3) Метрики и отчёты");
Console.WriteLine("4) Человеческий прогрев (с открытием браузера и ручной работой)");
Console.Write("Выберите раздел (1-4): ");
var section = Console.ReadLine();

switch (section)
{
    case "1":
        RunPlanning(planner);
        break;
    case "2":
        RunDrafts(draftGenerator);
        break;
    case "3":
        RunMetrics(metricsService, reportService);
        break;
    case "4":
        RunEngagement(engagementAssistant, engagementCoach);
        break;
    default:
        Console.WriteLine("Неизвестный раздел.");
        break;
}

static void RunPlanning(PublicationPlanner planner)
{
    Console.WriteLine("\nПланирование публикаций:");
    Console.WriteLine("1) Добавить публикацию");
    Console.WriteLine("2) Показать план");
    Console.Write("Действие: ");

    var action = Console.ReadLine();
    if (action == "1")
    {
        Console.Write("Дата и время (yyyy-MM-dd HH:mm): ");
        var dateRaw = Console.ReadLine();
        Console.Write("Тема: ");
        var topic = Console.ReadLine() ?? "Без темы";
        Console.Write("Формат (short/review/tutorial): ");
        var format = Console.ReadLine() ?? "short";

        if (!DateTime.TryParse(dateRaw, out var at))
        {
            Console.WriteLine("Некорректная дата.");
            return;
        }

        var item = planner.AddItem(at, topic, format);
        Console.WriteLine($"Добавлено: {item.ScheduledAt:g} | {item.Topic} | {item.Format}");
    }
    else
    {
        var items = planner.GetUpcoming();
        foreach (var item in items)
        {
            Console.WriteLine($"- {item.ScheduledAt:g} | {item.Topic} | {item.Format} | Статус: {item.Status}");
        }
    }
}

static void RunDrafts(DraftGenerator generator)
{
    Console.Write("\nВведите тему ролика: ");
    var topic = Console.ReadLine() ?? "день из жизни";
    Console.Write("Тон (экспертный/дружелюбный/развлекательный): ");
    var tone = Console.ReadLine() ?? "дружелюбный";

    var drafts = generator.Generate(topic, tone, 3);
    Console.WriteLine("\nЧерновики:");
    foreach (var draft in drafts)
    {
        Console.WriteLine($"\nОписание: {draft.Description}");
        Console.WriteLine($"Хэштеги: {string.Join(' ', draft.Hashtags)}");
    }
}

static void RunMetrics(MetricsService metricsService, ReportService reportService)
{
    Console.WriteLine("\nМетрики:");
    Console.WriteLine("1) Добавить срез");
    Console.WriteLine("2) Сформировать отчёт");
    Console.Write("Действие: ");

    var action = Console.ReadLine();
    if (action == "1")
    {
        var snapshot = new MetricsSnapshot(
            CapturedAt: DateTime.UtcNow,
            Views: ReadInt("Просмотры: "),
            Likes: ReadInt("Лайки: "),
            Comments: ReadInt("Комментарии: "),
            Shares: ReadInt("Репосты: "),
            AvgWatchSeconds: ReadDouble("Средний просмотр (сек): "),
            VideoLengthSeconds: ReadDouble("Длительность видео (сек): "));

        metricsService.Add(snapshot);
        Console.WriteLine("Срез добавлен.");
    }
    else
    {
        var reportPath = reportService.BuildWeeklyReport();
        Console.WriteLine($"Отчёт сохранён: {reportPath}");
    }
}

static void RunEngagement(EngagementAssistant engagementAssistant, EngagementSessionCoach engagementCoach)
{
    Console.WriteLine("\nРежим прогрева (человек в контуре):");
    Console.WriteLine("Приложение может открыть TikTok в браузере, но все действия выполняются только вручную.");
    Console.Write("Минут сессии: ");
    var minutes = ReadIntFromConsole();
    var duration = TimeSpan.FromMinutes(minutes <= 0 ? 15 : minutes);

    Console.Write("Открыть TikTok в браузере сейчас? (y/n): ");
    var shouldOpen = (Console.ReadLine() ?? "n").Equals("y", StringComparison.OrdinalIgnoreCase);
    if (shouldOpen)
    {
        var opened = engagementCoach.OpenTikTokInBrowser();
        Console.WriteLine(opened ? "Браузер открыт." : "Не удалось открыть браузер автоматически.");
    }

    Console.WriteLine("\nВаш план сессии:");
    var checklist = engagementAssistant.BuildChecklist(duration)
        .Concat(engagementCoach.BuildLowEffortPlan(duration));
    foreach (var step in checklist)
    {
        Console.WriteLine($"- {step}");
    }

    Console.WriteLine("\nПосле ручной сессии заполните итог:");
    var watched = ReadInt("Сколько роликов просмотрено: ");
    var likes = ReadInt("Сколько лайков поставлено вручную: ");
    var comments = ReadInt("Сколько комментариев оставлено вручную: ");
    Console.Write("Короткая заметка (что зашло аудитории): ");
    var notes = Console.ReadLine() ?? string.Empty;

    engagementCoach.SaveSession(new EngagementSessionLog(
        DateTime.UtcNow,
        (int)duration.TotalMinutes,
        watched,
        likes,
        comments,
        notes));

    Console.WriteLine("Сессия сохранена. Последние сессии:");
    foreach (var session in engagementCoach.GetRecentSessions(3))
    {
        Console.WriteLine($"- {session.StartedAt:g} | {session.PlannedMinutes} мин | видео {session.WatchedVideos} | лайки {session.ManualLikes} | комм {session.ManualComments}");
    }
}

static int ReadInt(string label)
{
    Console.Write(label);
    return int.TryParse(Console.ReadLine(), out var value) ? value : 0;
}

static double ReadDouble(string label)
{
    Console.Write(label);
    return double.TryParse(Console.ReadLine(), out var value) ? value : 0;
}

static int ReadIntFromConsole()
{
    return int.TryParse(Console.ReadLine(), out var value) ? value : 0;
}
