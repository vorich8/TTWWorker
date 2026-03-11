using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;

const string defaultConfigPath = "scenario.json";
var configPath = args.FirstOrDefault() ?? defaultConfigPath;

var app = new ControlPanel(configPath);
await app.RunAsync();

internal sealed class ControlPanel(string configPath)
{
    private readonly string _configPath = configPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly BrowserProfileStore _profileStore = new(Path.Combine(AppContext.BaseDirectory, "managed-profiles"));

    public async Task RunAsync()
    {
        EnsureConfigExists();
        _profileStore.EnsureStoreExists();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== ПУНКТ УПРАВЛЕНИЯ TTWWorker ===");
            Console.WriteLine("1) Запустить автоматизацию");
            Console.WriteLine("2) Настроить автоматизацию");
            Console.WriteLine("3) Показать текущий JSON");
            Console.WriteLine("4) Управление профилями");
            Console.WriteLine("0) Выход");
            Console.Write("Выбор: ");

            var input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1":
                    await RunAutomationAsync();
                    break;
                case "2":
                    await ConfigureAutomationAsync();
                    break;
                case "3":
                    ShowCurrentJson();
                    break;
                case "4":
                    ManageProfiles();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Неизвестная команда.");
                    break;
            }
        }
    }

    private void EnsureConfigExists()
    {
        if (File.Exists(_configPath)) return;
        File.WriteAllText(_configPath, JsonSerializer.Serialize(AppConfig.CreateDefault(), _jsonOptions));
    }

    private async Task ConfigureAutomationAsync()
    {
        var config = LoadConfig();
        var a = config.Automation;

        Console.Write($"URL (сейчас: {config.StartUrl}): ");
        var url = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(url)) config.StartUrl = url;

        Console.Write($"Время работы в минутах (сейчас: {a.WorkDurationMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var workMin) && workMin > 0) a.WorkDurationMinutes = workMin;

        Console.Write($"Мин. задержка листания в сек (сейчас: {a.ScrollDelayMinSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var scrollMin) && scrollMin > 0) a.ScrollDelayMinSeconds = scrollMin;

        Console.Write($"Макс. задержка листания в сек (сейчас: {a.ScrollDelayMaxSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var scrollMax) && scrollMax >= a.ScrollDelayMinSeconds) a.ScrollDelayMaxSeconds = scrollMax;

        Console.Write($"Действие листания keyPress/click (сейчас: {a.ScrollActionType}): ");
        var actionType = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (actionType is "keypress" or "click") a.ScrollActionType = actionType;

        if (a.ScrollActionType == "keypress")
        {
            Console.Write($"Клавиша листания (сейчас: {a.ScrollKey}): ");
            var key = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(key)) a.ScrollKey = key;
        }
        else
        {
            Console.Write($"Селектор кнопки листания (сейчас: {a.ScrollSelector}): ");
            var selector = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(selector)) a.ScrollSelector = selector;
        }

        Console.Write($"Лайков за период (сейчас: {a.LikesPerPeriod}): ");
        if (int.TryParse(Console.ReadLine(), out var likesCount) && likesCount >= 0) a.LikesPerPeriod = likesCount;

        Console.Write($"Длина периода лайков в минутах (сейчас: {a.LikePeriodMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var likePeriodMin) && likePeriodMin > 0) a.LikePeriodMinutes = likePeriodMin;

        Console.Write($"Клавиша лайка (сейчас: {a.LikeKey}): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey)) a.LikeKey = likeKey;

        await SaveConfigAsync(config);
        Console.WriteLine("Настройки автоматизации сохранены.");
    }

    private void ManageProfiles()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== УПРАВЛЕНИЕ ПРОФИЛЯМИ ===");
            var profiles = _profileStore.GetProfiles();
            if (profiles.Count == 0)
            {
                Console.WriteLine("Профилей нет. Создайте новый (команда n).");
            }
            else
            {
                foreach (var p in profiles) Console.WriteLine($"- {p.Name}");
            }

            Console.WriteLine("n) Создать новый профиль");
            Console.WriteLine("d) Удалить профиль");
            Console.WriteLine("q) Назад");
            Console.Write("Выбор: ");
            var cmd = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (cmd == "q") return;

            if (cmd == "n")
            {
                var created = _profileStore.CreateNextProfile();
                Console.WriteLine($"Создан профиль: {created.Name}");
            }
            else if (cmd == "d")
            {
                Console.Write("Имя профиля для удаления: ");
                var name = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && _profileStore.DeleteProfile(name))
                {
                    Console.WriteLine($"Профиль удалён: {name}");
                }
                else
                {
                    Console.WriteLine("Не удалось удалить профиль.");
                }
            }
        }
    }

    private void ShowCurrentJson()
    {
        Console.WriteLine();
        Console.WriteLine(File.ReadAllText(_configPath));
    }

    private BrowserProfile SelectProfileForRun()
    {
        var profiles = _profileStore.GetProfiles();
        if (profiles.Count == 0)
        {
            var created = _profileStore.CreateNextProfile();
            Console.WriteLine($"Автосоздан профиль: {created.Name}");
            return created;
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Выберите профиль для запуска:");
            for (var i = 0; i < profiles.Count; i++)
            {
                Console.WriteLine($"{i + 1}) {profiles[i].Name}");
            }
            Console.WriteLine("n) Создать новый профиль");
            Console.Write("Выбор: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input == "n")
            {
                var created = _profileStore.CreateNextProfile();
                Console.WriteLine($"Создан профиль: {created.Name}");
                profiles = _profileStore.GetProfiles();
                continue;
            }

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= profiles.Count)
            {
                return profiles[idx - 1];
            }

            Console.WriteLine("Некорректный выбор.");
        }
    }

    private async Task RunAutomationAsync()
    {
        var initialConfig = LoadConfig();
        if (!File.Exists(initialConfig.BrowserExecutablePath))
        {
            Console.WriteLine($"Браузер не найден: {initialConfig.BrowserExecutablePath}");
            return;
        }

        var selectedProfile = SelectProfileForRun();
        Console.WriteLine($"Запуск с профилем: {selectedProfile.Name}");

        using var playwright = await Playwright.CreateAsync();

        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
            selectedProfile.UserDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                ExecutablePath = initialConfig.BrowserExecutablePath,
                Headless = false,
                Args = ["--new-window", "--no-first-run", "--no-default-browser-check"]
            });

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        await page.GotoAsync(initialConfig.StartUrl);

        Console.WriteLine("Автоматизация запущена.");
        Console.WriteLine("Команды в любой момент: like | stop");

        var commandQueue = new ConcurrentQueue<string>();
        using var cts = new CancellationTokenSource();
        var inputTask = Task.Run(() => ReadCommands(commandQueue, cts.Token), cts.Token);

        var runStartedAt = DateTime.UtcNow;
        var likesPlan = LikeScheduler.CreatePlan(DateTime.UtcNow, LoadConfig().Automation);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var cfg = LoadConfig(); // постоянный рескан json
                var automation = cfg.Automation;

                var workDeadline = runStartedAt.AddMinutes(Math.Max(1, automation.WorkDurationMinutes));
                if (DateTime.UtcNow >= workDeadline)
                {
                    Console.WriteLine("Время работы вышло. Автоматизация завершена.");
                    break;
                }

                await ExecuteCommandsAsync(page, automation, commandQueue, cts);
                await ExecuteScheduledLikesAsync(page, automation, likesPlan);

                var delaySec = Random.Shared.Next(
                    Math.Max(1, automation.ScrollDelayMinSeconds),
                    Math.Max(automation.ScrollDelayMinSeconds, automation.ScrollDelayMaxSeconds) + 1);

                Console.WriteLine($"Ожидание {delaySec} сек...");
                await page.WaitForTimeoutAsync(delaySec * 1000);

                await ExecuteCommandsAsync(page, automation, commandQueue, cts);
                await ExecuteScheduledLikesAsync(page, automation, likesPlan);
                if (cts.Token.IsCancellationRequested) break;

                if (automation.ScrollActionType == "click" && !string.IsNullOrWhiteSpace(automation.ScrollSelector))
                {
                    await page.ClickAsync(automation.ScrollSelector);
                    Console.WriteLine($"Листание: click по {automation.ScrollSelector}");
                }
                else
                {
                    var key = string.IsNullOrWhiteSpace(automation.ScrollKey) ? "ArrowDown" : automation.ScrollKey;
                    await page.Keyboard.PressAsync(key);
                    Console.WriteLine($"Листание: keyPress {key}");
                }
            }
        }
        finally
        {
            cts.Cancel();
        }
    }

    private static void ReadCommands(ConcurrentQueue<string> queue, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var command = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(command))
            {
                queue.Enqueue(command.Trim().ToLowerInvariant());
            }
        }
    }

    private static async Task ExecuteScheduledLikesAsync(IPage page, AutomationConfig cfg, LikeScheduleState state)
    {
        if (cfg.LikesPerPeriod <= 0)
        {
            return;
        }

        if (DateTime.UtcNow >= state.PeriodEndUtc)
        {
            state.Reset(cfg);
        }

        while (state.TryDequeueDueLike(DateTime.UtcNow, out _))
        {
            await page.Keyboard.PressAsync(cfg.LikeKey);
            Console.WriteLine($"Плановый лайк: {cfg.LikeKey}");
        }
    }

    private static async Task ExecuteCommandsAsync(IPage page, AutomationConfig cfg, ConcurrentQueue<string> queue, CancellationTokenSource cts)
    {
        while (queue.TryDequeue(out var cmd))
        {
            switch (cmd)
            {
                case "like":
                    await page.Keyboard.PressAsync(cfg.LikeKey);
                    Console.WriteLine($"Команда like выполнена: {cfg.LikeKey}");
                    break;
                case "stop":
                    cts.Cancel();
                    break;
                default:
                    Console.WriteLine($"Неизвестная команда: {cmd}. Доступно: like | stop");
                    break;
            }
        }
    }

    private AppConfig LoadConfig()
    {
        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? AppConfig.CreateDefault();
    }

    private Task SaveConfigAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        return File.WriteAllTextAsync(_configPath, json);
    }
}

internal sealed class BrowserProfileStore(string rootPath)
{
    private readonly string _rootPath = rootPath;

    public void EnsureStoreExists() => Directory.CreateDirectory(_rootPath);

    public List<BrowserProfile> GetProfiles()
    {
        EnsureStoreExists();
        return Directory.GetDirectories(_rootPath)
            .Select(path => new BrowserProfile(Path.GetFileName(path), path))
            .OrderBy(x => x.Name)
            .ToList();
    }

    public BrowserProfile CreateNextProfile()
    {
        EnsureStoreExists();
        var nextNumber = 1;
        var existing = GetProfiles().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (existing.Contains($"Profile {nextNumber}")) nextNumber++;

        var name = $"Profile {nextNumber}";
        var dir = Path.Combine(_rootPath, name);
        Directory.CreateDirectory(dir);
        return new BrowserProfile(name, dir);
    }

    public bool DeleteProfile(string profileName)
    {
        var path = Path.Combine(_rootPath, profileName);
        if (!Directory.Exists(path)) return false;

        Directory.Delete(path, true);
        return true;
    }
}

internal sealed record BrowserProfile(string Name, string UserDataDir);

internal static class LikeScheduler
{
    public static LikeScheduleState CreatePlan(DateTime nowUtc, AutomationConfig cfg)
    {
        return new LikeScheduleState(nowUtc, cfg);
    }
}

internal sealed class LikeScheduleState
{
    private readonly Queue<DateTime> _plannedLikesUtc = new();
    public DateTime PeriodEndUtc { get; private set; }

    public LikeScheduleState(DateTime periodStartUtc, AutomationConfig cfg)
    {
        Reset(periodStartUtc, cfg);
    }

    public void Reset(AutomationConfig cfg)
    {
        Reset(DateTime.UtcNow, cfg);
    }

    private void Reset(DateTime periodStartUtc, AutomationConfig cfg)
    {
        _plannedLikesUtc.Clear();
        var periodMin = Math.Max(1, cfg.LikePeriodMinutes);
        PeriodEndUtc = periodStartUtc.AddMinutes(periodMin);

        if (cfg.LikesPerPeriod <= 0) return;

        var secondsInPeriod = (int)TimeSpan.FromMinutes(periodMin).TotalSeconds;
        var offsets = Enumerable.Range(0, cfg.LikesPerPeriod)
            .Select(_ => Random.Shared.Next(1, Math.Max(2, secondsInPeriod)))
            .OrderBy(x => x)
            .ToList();

        foreach (var offset in offsets)
        {
            _plannedLikesUtc.Enqueue(periodStartUtc.AddSeconds(offset));
        }
    }

    public bool TryDequeueDueLike(DateTime nowUtc, out DateTime dueUtc)
    {
        dueUtc = default;
        if (_plannedLikesUtc.Count == 0) return false;
        if (_plannedLikesUtc.Peek() > nowUtc) return false;

        dueUtc = _plannedLikesUtc.Dequeue();
        return true;
    }
}

internal sealed class AppConfig
{
    public string StartUrl { get; set; } = "https://www.tiktok.com/foryou";
    public string BrowserExecutablePath { get; set; } = @"C:\Program Files (x86)\Yandex\YandexBrowser\Application\browser.exe";
    public AutomationConfig Automation { get; set; } = new();

    public static AppConfig CreateDefault() => new();
}

internal sealed class AutomationConfig
{
    public int WorkDurationMinutes { get; set; } = 60;
    public int ScrollDelayMinSeconds { get; set; } = 3;
    public int ScrollDelayMaxSeconds { get; set; } = 12;
    public string ScrollActionType { get; set; } = "keyPress";
    public string ScrollKey { get; set; } = "ArrowDown";
    public string ScrollSelector { get; set; } = "";

    public int LikesPerPeriod { get; set; } = 2;
    public int LikePeriodMinutes { get; set; } = 10;
    public string LikeKey { get; set; } = "KeyL";
}
