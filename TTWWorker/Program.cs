using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;

const string defaultConfigPath = "scenario.json";
var configPath = args.FirstOrDefault() ?? defaultConfigPath;

var app = new YandexControlPanel(configPath);
await app.RunAsync();

internal sealed class YandexControlPanel(string configPath)
{
    private readonly string _configPath = configPath;
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task RunAsync()
    {
        EnsureConfigExists();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== YANDEX TIKTOK CONTROL ===");
            Console.WriteLine("1) Запустить автоматизацию");
            Console.WriteLine("2) Настроить scenario.json");
            Console.WriteLine("3) Показать scenario.json");
            Console.WriteLine("0) Выход");
            Console.Write("Выбор: ");

            var cmd = Console.ReadLine()?.Trim();
            switch (cmd)
            {
                case "1":
                    await RunAutomationAsync();
                    break;
                case "2":
                    await ConfigureAsync();
                    break;
                case "3":
                    Console.WriteLine(File.ReadAllText(_configPath));
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
        if (File.Exists(_configPath))
            return;

        File.WriteAllText(_configPath, JsonSerializer.Serialize(AppConfig.CreateDefault(), _json));
    }

    private AppConfig LoadConfig()
    {
        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, _json) ?? AppConfig.CreateDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка чтения scenario.json: {ex.Message}");
            return AppConfig.CreateDefault();
        }
    }

    private Task SaveConfigAsync(AppConfig config)
        => File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, _json));

    private async Task ConfigureAsync()
    {
        var cfg = LoadConfig();

        Console.Write($"Путь к browser.exe (сейчас: {cfg.Browser.ExecutablePath}): ");
        var exe = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(exe))
            cfg.Browser.ExecutablePath = exe;

        Console.Write($"Папка профилей (сейчас: {cfg.Browser.ProfilesRoot}): ");
        var root = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(root))
            cfg.Browser.ProfilesRoot = root;

        Console.Write($"Использовать системный User Data? (сейчас: {(cfg.Browser.UseSystemUserData ? "да" : "нет")}, y/n): ");
        var useSystem = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (useSystem is "y" or "yes" or "д" or "да")
            cfg.Browser.UseSystemUserData = true;
        else if (useSystem is "n" or "no" or "н" or "нет")
            cfg.Browser.UseSystemUserData = false;

        Console.Write($"Системная папка User Data (сейчас: {cfg.Browser.SystemUserDataDir}): ");
        var systemUserData = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(systemUserData))
            cfg.Browser.SystemUserDataDir = systemUserData;

        Console.Write($"Имя профиля (сейчас: {cfg.Browser.ProfileName}): ");
        var profile = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(profile))
            cfg.Browser.ProfileName = profile;

        Console.Write($"User-Agent (сейчас: {cfg.Browser.UserAgent}): ");
        var userAgent = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(userAgent))
            cfg.Browser.UserAgent = userAgent;

        Console.Write($"Start URL (сейчас: {cfg.StartUrl}): ");
        var url = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(url))
            cfg.StartUrl = url;

        PromptAutomation(cfg.Automation);

        cfg.AutomationConfigured = true;
        await SaveConfigAsync(cfg);
        Console.WriteLine("Сценарий сохранён.");
    }

    private static void PromptAutomation(AutomationConfig automation)
    {
        Console.Write($"Время работы, минут (сейчас: {automation.WorkDurationMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var mins) && mins > 0)
            automation.WorkDurationMinutes = mins;

        Console.Write($"Мин. задержка листания, сек (сейчас: {automation.ScrollDelayMinSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var minDelay) && minDelay > 0)
            automation.ScrollDelayMinSeconds = minDelay;

        Console.Write($"Макс. задержка листания, сек (сейчас: {automation.ScrollDelayMaxSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var maxDelay) && maxDelay >= automation.ScrollDelayMinSeconds)
            automation.ScrollDelayMaxSeconds = maxDelay;

        Console.Write($"Лайков за период (сейчас: {automation.LikesPerPeriod}): ");
        if (int.TryParse(Console.ReadLine(), out var likes) && likes >= 0)
            automation.LikesPerPeriod = likes;

        Console.Write($"Период лайков, минут (сейчас: {automation.LikePeriodMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var period) && period > 0)
            automation.LikePeriodMinutes = period;

        Console.Write($"Клавиша листания (сейчас: {automation.ScrollKey}): ");
        var scrollKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(scrollKey))
            automation.ScrollKey = scrollKey;

        Console.Write($"Клавиша лайка (сейчас: {automation.LikeKey}): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey))
            automation.LikeKey = likeKey;
    }

    private async Task RunAutomationAsync()
    {
        var cfg = LoadConfig();

        if (!File.Exists(cfg.Browser.ExecutablePath))
        {
            Console.WriteLine($"browser.exe не найден: {cfg.Browser.ExecutablePath}");
            return;
        }

        if (!cfg.AutomationConfigured)
        {
            Console.WriteLine("Настройки автоматизации ещё не сохранены. Заполняем...");
            PromptAutomation(cfg.Automation);
            cfg.AutomationConfigured = true;
            await SaveConfigAsync(cfg);
        }

        var profileDir = cfg.Browser.UseSystemUserData
            ? cfg.Browser.SystemUserDataDir
            : Path.Combine(cfg.Browser.ProfilesRoot, cfg.Browser.ProfileName);

        Directory.CreateDirectory(profileDir);

        Console.WriteLine("Запускаю Яндекс.Браузер в отдельном профиле...");
        Console.WriteLine($"Профиль: {profileDir}");
        Console.WriteLine($"Profile directory: {cfg.Browser.ProfileName}");

        var launchArgs = new List<string>
        {
            "--new-window",
            "--no-first-run",
            "--no-default-browser-check",
            $"--profile-directory={cfg.Browser.ProfileName}"
        };

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var context = await playwright.Chromium.LaunchPersistentContextAsync(
                userDataDir: profileDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = cfg.Browser.ExecutablePath,
                    Headless = false,
                    Channel = null,
                    IgnoreDefaultArgs = new[] { "--enable-automation", "--disable-extensions" },
                    Args = launchArgs,
                    UserAgent = cfg.Browser.UserAgent
                });

            // Всегда создаём отдельную вкладку под TikTok, чтобы не оставаться на about:blank.
            var page = await context.NewPageAsync();
            await page.BringToFrontAsync();
            await page.GotoAsync(cfg.StartUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Если стартовая вкладка осталась пустой, закрываем её, чтобы не мешала.
            foreach (var existing in context.Pages.Where(p => p != page && p.Url.Equals("about:blank", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                await existing.CloseAsync();
            }

            Console.WriteLine("TikTok открыт. Проверьте аккаунт/VPN и нажмите Enter для старта.");
            Console.ReadLine();

            var queue = new ConcurrentQueue<string>();
            using var cts = new CancellationTokenSource();
            _ = Task.Run(() => ReadCommands(queue, cts.Token), cts.Token);

            var likes = new LikeScheduleState(DateTime.UtcNow, cfg.Automation);
            var startedAt = DateTime.UtcNow;

            Console.WriteLine("Автоматизация запущена. Команды: like | stop");

            while (!cts.IsCancellationRequested)
            {
                cfg = LoadConfig();
                if (DateTime.UtcNow >= startedAt.AddMinutes(Math.Max(1, cfg.Automation.WorkDurationMinutes)))
                {
                    Console.WriteLine("Время работы вышло.");
                    break;
                }

                await ProcessCommandsAsync(page, cfg.Automation, queue, cts);
                await ProcessLikesAsync(page, cfg.Automation, likes);

                var delaySec = Random.Shared.Next(
                    Math.Max(1, cfg.Automation.ScrollDelayMinSeconds),
                    Math.Max(cfg.Automation.ScrollDelayMinSeconds, cfg.Automation.ScrollDelayMaxSeconds) + 1);

                Console.WriteLine($"Ожидание {delaySec} сек...");
                await page.WaitForTimeoutAsync(delaySec * 1000);

                await ProcessCommandsAsync(page, cfg.Automation, queue, cts);
                await ProcessLikesAsync(page, cfg.Automation, likes);
                if (cts.IsCancellationRequested)
                    break;

                await page.Keyboard.PressAsync(cfg.Automation.ScrollKey);
                Console.WriteLine($"Листание: {cfg.Automation.ScrollKey}");
            }

            cts.Cancel();
            await context.CloseAsync();
        }
        catch (PlaywrightException ex)
        {
            Console.WriteLine("Ошибка Playwright:");
            Console.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка запуска автоматизации:");
            Console.WriteLine(ex.Message);
        }
    }

    private static void ReadCommands(ConcurrentQueue<string> queue, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var cmd = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(cmd))
                queue.Enqueue(cmd.Trim().ToLowerInvariant());
        }
    }

    private static async Task ProcessCommandsAsync(IPage page, AutomationConfig cfg, ConcurrentQueue<string> queue, CancellationTokenSource cts)
    {
        while (queue.TryDequeue(out var cmd))
        {
            switch (cmd)
            {
                case "like":
                    await page.Keyboard.PressAsync(cfg.LikeKey);
                    Console.WriteLine($"Команда like: {cfg.LikeKey}");
                    break;
                case "stop":
                    cts.Cancel();
                    break;
                default:
                    Console.WriteLine($"Неизвестная команда: {cmd}");
                    break;
            }
        }
    }

    private static async Task ProcessLikesAsync(IPage page, AutomationConfig cfg, LikeScheduleState state)
    {
        if (cfg.LikesPerPeriod <= 0)
            return;

        if (DateTime.UtcNow >= state.PeriodEndUtc)
            state.Reset(cfg);

        while (state.TryDequeueDueLike(DateTime.UtcNow, out _))
        {
            await page.Keyboard.PressAsync(cfg.LikeKey);
            Console.WriteLine($"Плановый лайк: {cfg.LikeKey}");
        }
    }
}

internal sealed class AppConfig
{
    public string StartUrl { get; set; } = "https://www.tiktok.com/foryou";
    public BrowserConfig Browser { get; set; } = new();
    public AutomationConfig Automation { get; set; } = new();
    public bool AutomationConfigured { get; set; } = true;

    public static AppConfig CreateDefault() => new();
}

internal sealed class BrowserConfig
{
    public string ExecutablePath { get; set; } = @"C:\Program Files (x86)\Yandex\YandexBrowser\Application\browser.exe";
    public string ProfilesRoot { get; set; } = "managed-profiles";
    public string ProfileName { get; set; } = "Profile 1";
    public bool UseSystemUserData { get; set; }
    public string SystemUserDataDir { get; set; } = @"C:\Users\Администратор\AppData\Local\Yandex\YandexBrowser\User Data";
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 YaBrowser/24.4.0.0 Safari/537.36";
}

internal sealed class AutomationConfig
{
    public int WorkDurationMinutes { get; set; } = 60;
    public int ScrollDelayMinSeconds { get; set; } = 3;
    public int ScrollDelayMaxSeconds { get; set; } = 12;
    public string ScrollKey { get; set; } = "ArrowDown";
    public int LikesPerPeriod { get; set; } = 2;
    public int LikePeriodMinutes { get; set; } = 10;
    public string LikeKey { get; set; } = "KeyL";
}

internal sealed class LikeScheduleState
{
    private readonly Queue<DateTime> _queue = new();
    public DateTime PeriodEndUtc { get; private set; }

    public LikeScheduleState(DateTime startUtc, AutomationConfig cfg)
    {
        Reset(startUtc, cfg);
    }

    public void Reset(AutomationConfig cfg) => Reset(DateTime.UtcNow, cfg);

    private void Reset(DateTime startUtc, AutomationConfig cfg)
    {
        _queue.Clear();
        var mins = Math.Max(1, cfg.LikePeriodMinutes);
        PeriodEndUtc = startUtc.AddMinutes(mins);
        if (cfg.LikesPerPeriod <= 0)
            return;

        var totalSec = (int)TimeSpan.FromMinutes(mins).TotalSeconds;
        var offsets = Enumerable.Range(0, cfg.LikesPerPeriod)
            .Select(_ => Random.Shared.Next(1, Math.Max(2, totalSec)))
            .OrderBy(v => v);

        foreach (var offset in offsets)
            _queue.Enqueue(startUtc.AddSeconds(offset));
    }

    public bool TryDequeueDueLike(DateTime nowUtc, out DateTime dueUtc)
    {
        dueUtc = default;
        if (_queue.Count == 0 || _queue.Peek() > nowUtc)
            return false;

        dueUtc = _queue.Dequeue();
        return true;
    }
}
