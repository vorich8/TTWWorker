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
var keyboardProfileService = new KeyboardProfileService(storage);
var pointerService = new PointerService();

Console.WriteLine("TTWWorker — автоматизация контент-операций");
Console.WriteLine("1) План публикаций");
Console.WriteLine("2) Черновики описаний/хэштегов");
Console.WriteLine("3) Метрики и отчёты");
Console.WriteLine("4) Кликер-профили и запуск (Windows 11)");
Console.Write("Выберите раздел (1-4): ");
var section = Console.ReadLine();

switch (section)
{
    case "1": RunPlanning(planner); break;
    case "2": RunDrafts(draftGenerator); break;
    case "3": RunMetrics(metricsService, reportService); break;
    case "4": RunClickerProfiles(keyboardProfileService, keyboardWarmupRunner, engagementCoach, pointerService).GetAwaiter().GetResult(); break;
    default: Console.WriteLine("Неизвестный раздел."); break;
}

static async Task RunClickerProfiles(KeyboardProfileService profileService, KeyboardWarmupRunner runner, EngagementSessionCoach coach, PointerService pointerService)
{
    Console.WriteLine("\nПрофили кликера (Windows 11):");
    Console.WriteLine("1) Запустить профиль");
    Console.WriteLine("2) Добавить профиль");
    Console.WriteLine("3) Редактировать профиль");
    Console.WriteLine("4) Список профилей");
    Console.Write("Действие: ");
    var action = Console.ReadLine();

    switch (action)
    {
        case "1": await RunProfile(profileService, runner, coach); break;
        case "2": SaveProfile(profileService, null, pointerService); break;
        case "3":
            var existing = PickProfile(profileService);
            if (existing is not null) SaveProfile(profileService, existing, pointerService);
            break;
        case "4": PrintProfiles(profileService.GetAll()); break;
        default: Console.WriteLine("Неизвестное действие."); break;
    }
}

static async Task RunProfile(KeyboardProfileService profileService, KeyboardWarmupRunner runner, EngagementSessionCoach coach)
{
    var profile = PickProfile(profileService);
    if (profile is null) return;

    Console.Write($"Минуты работы (Enter={profile.DefaultMinutes}): ");
    var raw = Console.ReadLine();
    var minutes = int.TryParse(raw, out var m) && m > 0 ? m : profile.DefaultMinutes;

    using var cts = new CancellationTokenSource();
    var stopListener = Task.Run(async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.S)
            {
                Console.WriteLine($"[{DateTime.Now:T}] [BOT] STOP от пользователя.");
                cts.Cancel();
                break;
            }

            await Task.Delay(100);
        }
    });

    try
    {
        await runner.RunAsync(profile, minutes, cts.Token);
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
    coach.SaveSession(new EngagementSessionLog(DateTime.UtcNow, minutes, 0, 0, 0, notes));
}

static void SaveProfile(KeyboardProfileService service, KeyboardProfile? source, PointerService pointerService)
{
    var model = source ?? service.CreateDefault();

    Console.Write($"Имя профиля (Enter={model.Name}): ");
    var name = ReadOrDefault(model.Name);

    var downMin = ReadIntWithDefault($"Down min сек (Enter={model.DownMinIntervalSeconds}): ", model.DownMinIntervalSeconds);
    var downMax = ReadIntWithDefault($"Down max сек (Enter={model.DownMaxIntervalSeconds}): ", model.DownMaxIntervalSeconds);
    var altTabSec = ReadIntWithDefault($"Alt+Tab интервал сек (0=выкл, Enter={model.AltTabIntervalSeconds}): ", model.AltTabIntervalSeconds);
    var clickDelayMs = ReadIntWithDefault($"Задержка между кликами, мс (Enter={Math.Max(1, model.ClickDelayMilliseconds)}): ", Math.Max(1, model.ClickDelayMilliseconds));
    var clickPeriodSec = ReadIntWithDefault($"Период случайных кликов, сек (Enter={Math.Max(1, model.ClickPeriodSeconds)}): ", Math.Max(1, model.ClickPeriodSeconds));
    var clicksPerPeriod = ReadIntWithDefault($"Сколько раз кликать за период (Enter={Math.Max(1, model.ClicksPerPeriod)}): ", Math.Max(1, model.ClicksPerPeriod));
    var defaultMinutes = ReadIntWithDefault($"Минуты по умолчанию (Enter={model.DefaultMinutes}): ", model.DefaultMinutes);

    var points = EditPoints(model.AltTabClickPoints, pointerService);

    var profile = new KeyboardProfile(
        model.Id,
        name,
        Math.Max(1, downMin),
        Math.Max(downMin, downMax),
        Math.Max(0, altTabSec),
        Math.Max(1, defaultMinutes),
        Math.Max(1, clickDelayMs),
        Math.Max(1, clickPeriodSec),
        Math.Max(1, clicksPerPeriod),
        points);

    service.CreateOrUpdate(profile);
    Console.WriteLine("Профиль сохранён.");
}

static List<PixelClickPoint> EditPoints(IReadOnlyList<PixelClickPoint> source, PointerService pointerService)
{
    var points = source.ToList();
    Console.WriteLine("Настройка пикселей для клика ЛКМ.");
    Console.WriteLine("Сколько точек хранить в профиле? (каждый клик использует следующую точку по кругу)");
    Console.Write($"Enter={Math.Max(1, points.Count)}: ");

    var count = int.TryParse(Console.ReadLine(), out var c) && c > 0 ? c : Math.Max(1, points.Count);
    var result = new List<PixelClickPoint>();

    var diagnostic = pointerService.GetCaptureDiagnostic();
    if (!string.Equals(diagnostic, "ok", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  Диагностика захвата курсора: {diagnostic}");
        Console.WriteLine("  Можно ориентироваться по Live-координатам/ввести X,Y вручную.");
    }

    for (var i = 0; i < count; i++)
    {
        var current = i < points.Count ? points[i] : new PixelClickPoint(960, 540, 4);
        Console.WriteLine($"Точка #{i + 1}:");
        Console.WriteLine("  Переместите мышку в нужную точку.");
        Console.WriteLine("  Лайв-режим координат запущен: нажмите Enter для фиксации точки.");

        var liveCaptured = CapturePointWithLivePreview(pointerService, current.X, current.Y);
        var captured = pointerService.CaptureCurrentPosition() ?? liveCaptured;

        var xDefault = captured?.X ?? current.X;
        var yDefault = captured?.Y ?? current.Y;

        Console.WriteLine(captured is null
            ? "  Координаты не удалось захватить автоматически, используем ручной ввод."
            : $"  Захвачено: X={xDefault}, Y={yDefault}");

        var x = ReadIntWithDefault($"  X (Enter={xDefault}): ", xDefault);
        var y = ReadIntWithDefault($"  Y (Enter={yDefault}): ", yDefault);
        var clicks = ReadIntWithDefault($"  ClickCount (Enter={current.ClickCount}, рекомендовано 4): ", current.ClickCount);

        var moved = pointerService.MoveTo(x, y);
        Console.WriteLine(moved
            ? "  Проверка: курсор перемещён в сохранённую точку."
            : "  Проверка: не удалось переместить курсор автоматически.");

        result.Add(new PixelClickPoint(x, y, Math.Max(1, clicks)));
    }

    return result;
}

static KeyboardProfile? PickProfile(KeyboardProfileService service)
{
    var profiles = service.GetAll().ToList();
    PrintProfiles(profiles);
    Console.Write("Выберите индекс профиля: ");
    if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > profiles.Count)
    {
        Console.WriteLine("Неверный индекс.");
        return null;
    }

    return profiles[idx - 1];
}

static void PrintProfiles(IReadOnlyList<KeyboardProfile> profiles)
{
    if (profiles.Count == 0)
    {
        Console.WriteLine("Профилей нет.");
        return;
    }

    for (var i = 0; i < profiles.Count; i++)
    {
        var p = profiles[i];
        var altTabLabel = p.AltTabIntervalSeconds > 0 ? $"{p.AltTabIntervalSeconds}s" : "off";
        Console.WriteLine($"{i + 1}) {p.Name} | Down {p.DownMinIntervalSeconds}-{p.DownMaxIntervalSeconds}s | AltTab {altTabLabel} | clickDelay={Math.Max(1, p.ClickDelayMilliseconds)}ms | clickPeriod={Math.Max(1, p.ClickPeriodSeconds)}s | clicksPerPeriod={Math.Max(1, p.ClicksPerPeriod)} | points={p.AltTabClickPoints.Count}");
    }
}

static (int X, int Y)? CapturePointWithLivePreview(PointerService pointerService, int fallbackX, int fallbackY)
{
    var last = pointerService.CaptureCurrentPosition() ?? (fallbackX, fallbackY);
    var unchangedTicks = 0;
    Console.WriteLine("  Нажмите Enter для фиксации текущей позиции мыши.");

    while (true)
    {
        var current = pointerService.CaptureCurrentPosition();
        if (current.HasValue)
        {
            if (current.Value.X == last.Item1 && current.Value.Y == last.Item2)
            {
                unchangedTicks++;
            }
            else
            {
                unchangedTicks = 0;
                last = current.Value;
            }
        }

        var suffix = unchangedTicks > 40 ? " (координаты не меняются, при необходимости введите вручную)" : string.Empty;
        Console.Write($"\r  Live X={last.Item1}, Y={last.Item2}{suffix}      ");

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return (last.Item1, last.Item2);
            }
        }

        Thread.Sleep(80);
    }
}


static string ReadOrDefault(string fallback)
{
    var raw = Console.ReadLine();
    return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
}

static int ReadIntWithDefault(string label, int fallback)
{
    Console.Write(label);
    var raw = Console.ReadLine();
    return int.TryParse(raw, out var value) ? value : fallback;
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
