using TTWWorker;
using TTWWorker.Services;
using TTWWorker.Models;

var storage = new JsonStorage();
var planner = new PublicationPlanner(storage);
var draftGenerator = new DraftGenerator();
var metricsService = new MetricsService(storage);
var reportService = new ReportService(metricsService, planner, storage);
var keyboardWarmupRunner = new KeyboardWarmupRunner();
var engagementCoach = new EngagementSessionCoach(storage);

Console.WriteLine("TTWWorker — автоматизация контент-операций");
Console.WriteLine("1) План публикаций");
Console.WriteLine("2) Черновики описаний/хэштегов");
Console.WriteLine("3) Метрики и отчёты");
Console.WriteLine("4) Сессия TikTok по таймеру (Down/L)");
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
        RunTimedKeyboardWarmup(keyboardWarmupRunner, engagementCoach).GetAwaiter().GetResult();
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

static async Task RunTimedKeyboardWarmup(KeyboardWarmupRunner runner, EngagementSessionCoach coach)
{
    Console.WriteLine("\nРежим таймера TikTok: открытие TikTok и нажатия Down/L по минутам.");
    Console.WriteLine("STOP: нажмите клавишу S в любой момент.");
    Console.Write("Сколько минут работать: ");

    var minutes = ReadIntFromConsole();
    if (minutes <= 0)
    {
        minutes = 10;
    }

    using var cts = new CancellationTokenSource();
    var stopListener = Task.Run(async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.S)
                {
                    Console.WriteLine($"[{DateTime.Now:T}] [BOT] STOP от пользователя.");
                    cts.Cancel();
                    break;
                }
            }

            await Task.Delay(100);
        }
    });

    try
    {
        await runner.RunAsync(minutes, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"[{DateTime.Now:T}] [BOT] Сессия остановлена пользователем.");
    }
    finally
    {
        cts.Cancel();
        await stopListener;
    }

    Console.Write("Заметка по сессии: ");
    var notes = Console.ReadLine() ?? string.Empty;

    coach.SaveSession(new EngagementSessionLog(
        StartedAt: DateTime.UtcNow,
        PlannedMinutes: minutes,
        WatchedVideos: 0,
        ManualLikes: 0,
        ManualComments: 0,
        Notes: notes));

    Console.WriteLine("Сессия сохранена.");
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
