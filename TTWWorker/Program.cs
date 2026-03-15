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

        var profileAutomation = LoadProfileAutomation(cfg, cfg.Browser.ProfileName, cfg.Automation);
        Console.WriteLine($"Настройка действий для профиля: {cfg.Browser.ProfileName}");
        PromptAutomation(profileAutomation);
        SaveProfileAutomation(cfg, cfg.Browser.ProfileName, profileAutomation);
        cfg.Automation = profileAutomation;

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

        Console.Write($"Селектор кнопки листания вниз (сейчас: {automation.ScrollSelector}): ");
        var scrollSelector = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(scrollSelector))
            automation.ScrollSelector = scrollSelector;

        Console.Write($"Селектор кнопки лайка (сейчас: {automation.LikeButtonSelector}): ");
        var likeSelector = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeSelector))
            automation.LikeButtonSelector = likeSelector;

        Console.Write($"Клавиша лайка (сейчас: {automation.LikeKey}): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey))
            automation.LikeKey = likeKey;

        Console.Write($"Разрешить лайк через кнопку? (сейчас: {(automation.AllowLikeButton ? "да" : "нет")}, y/n): ");
        var likeButton = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (likeButton is "y" or "yes" or "д" or "да") automation.AllowLikeButton = true;
        if (likeButton is "n" or "no" or "н" or "нет") automation.AllowLikeButton = false;

        Console.Write($"Разрешить лайк через клавишу L? (сейчас: {(automation.AllowLikeKey ? "да" : "нет")}, y/n): ");
        var likeKeyMode = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (likeKeyMode is "y" or "yes" or "д" or "да") automation.AllowLikeKey = true;
        if (likeKeyMode is "n" or "no" or "н" or "нет") automation.AllowLikeKey = false;

        if (!automation.AllowLikeButton && !automation.AllowLikeKey)
            automation.AllowLikeKey = true;
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
            var firstProfile = LoadProfileAutomation(cfg, cfg.Browser.ProfileName, cfg.Automation);
            PromptAutomation(firstProfile);
            SaveProfileAutomation(cfg, cfg.Browser.ProfileName, firstProfile);
            cfg.Automation = firstProfile;
            cfg.AutomationConfigured = true;
            await SaveConfigAsync(cfg);
        }

        cfg.Automation = LoadProfileAutomation(cfg, cfg.Browser.ProfileName, cfg.Automation);

        var launchUserDataDir = GetOrCreateProfileUserDataDir(cfg, cfg.Browser.ProfileName);

        if (!HasProfileAutomationSettings(cfg, cfg.Browser.ProfileName))
        {
            Console.WriteLine($"Для профиля '{cfg.Browser.ProfileName}' ещё нет настроек действий. Заполняем...");
            var firstProfile = LoadProfileAutomation(cfg, cfg.Browser.ProfileName, cfg.Automation);
            PromptAutomation(firstProfile);
            SaveProfileAutomation(cfg, cfg.Browser.ProfileName, firstProfile);
            cfg.Automation = firstProfile;
            cfg.AutomationConfigured = true;
            await SaveConfigAsync(cfg);
        }

        Console.WriteLine("Запускаю Яндекс.Браузер с профилем (сессия сохраняется)...");
        Console.WriteLine($"User Data: {launchUserDataDir}");

        var launchArgs = new List<string>
        {
            "--new-window",
            "--no-first-run",
            "--no-default-browser-check"
        };

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var launch = await LaunchContextWithFallbackAsync(playwright, cfg, launchUserDataDir, launchArgs);
            var context = launch.Context;

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
                cfg.Automation = LoadProfileAutomation(cfg, cfg.Browser.ProfileName, cfg.Automation);
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

                await page.ClickAsync(cfg.Automation.ScrollSelector);
                Console.WriteLine($"Листание кликом: {cfg.Automation.ScrollSelector}");
            }

            cts.Cancel();
            await context.CloseAsync();

            if (!string.IsNullOrWhiteSpace(launch.TempClonePath))
                TryDeleteDirectory(launch.TempClonePath);
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


    private static string GetOrCreateProfileUserDataDir(AppConfig cfg, string profileName)
    {
        var safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var root = Path.Combine(cfg.Browser.ProfilesRoot, "runtime-profiles", safeName);
        Directory.CreateDirectory(root);
        return root;
    }

    private static bool HasProfileAutomationSettings(AppConfig cfg, string profileName)
        => File.Exists(GetProfileSettingsPath(cfg, profileName));

    private async Task<LaunchResult> LaunchContextWithFallbackAsync(IPlaywright playwright, AppConfig cfg, string launchUserDataDir, IReadOnlyList<string> launchArgs)
    {
        var context = await LaunchContextAsync(playwright, cfg, launchUserDataDir, launchArgs);
        return new LaunchResult(context, null);
    }

    private async Task<IBrowserContext> LaunchContextAsync(IPlaywright playwright, AppConfig cfg, string userDataDir, IReadOnlyList<string> launchArgs)
    {
        return await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir: userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                ExecutablePath = cfg.Browser.ExecutablePath,
                Headless = false,
                Channel = null,
                IgnoreDefaultArgs = new[] { "--enable-automation", "--disable-extensions" },
                Args = launchArgs,
                UserAgent = cfg.Browser.UserAgent
            });
    }


    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore cleanup issues
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
                    await PerformLikeAsync(page, cfg, "Команда like");
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
            await PerformLikeAsync(page, cfg, "Плановый лайк");
        }
    }

    private static async Task PerformLikeAsync(IPage page, AutomationConfig cfg, string source)
    {
        var modes = new List<string>();
        if (cfg.AllowLikeButton && !string.IsNullOrWhiteSpace(cfg.LikeButtonSelector)) modes.Add("button");
        if (cfg.AllowLikeKey && !string.IsNullOrWhiteSpace(cfg.LikeKey)) modes.Add("key");
        if (modes.Count == 0)
        {
            Console.WriteLine($"{source}: нет доступного способа лайка");
            return;
        }

        var selected = modes[Random.Shared.Next(modes.Count)];
        if (selected == "button")
        {
            await page.ClickAsync(cfg.LikeButtonSelector);
            Console.WriteLine($"{source}: кнопка ({cfg.LikeButtonSelector})");
        }
        else
        {
            await page.Keyboard.PressAsync(cfg.LikeKey);
            Console.WriteLine($"{source}: клавиша ({cfg.LikeKey})");
        }
    }

    private AutomationConfig LoadProfileAutomation(AppConfig cfg, string profileName, AutomationConfig fallback)
    {
        var path = GetProfileSettingsPath(cfg, profileName);
        try
        {
            if (!File.Exists(path))
                return CloneAutomation(fallback);

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AutomationConfig>(json, _json) ?? CloneAutomation(fallback);
        }
        catch
        {
            return CloneAutomation(fallback);
        }
    }

    private void SaveProfileAutomation(AppConfig cfg, string profileName, AutomationConfig automation)
    {
        var path = GetProfileSettingsPath(cfg, profileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(automation, _json));
    }

    private static string GetProfileSettingsPath(AppConfig cfg, string profileName)
    {
        var safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var dir = Path.Combine(cfg.Browser.ProfilesRoot, "profile-settings");
        return Path.Combine(dir, $"{safeName}.json");
    }

    private static AutomationConfig CloneAutomation(AutomationConfig source)
    {
        return new AutomationConfig
        {
            WorkDurationMinutes = source.WorkDurationMinutes,
            ScrollDelayMinSeconds = source.ScrollDelayMinSeconds,
            ScrollDelayMaxSeconds = source.ScrollDelayMaxSeconds,
            ScrollSelector = source.ScrollSelector,
            LikesPerPeriod = source.LikesPerPeriod,
            LikePeriodMinutes = source.LikePeriodMinutes,
            LikeKey = source.LikeKey,
            LikeButtonSelector = source.LikeButtonSelector,
            AllowLikeButton = source.AllowLikeButton,
            AllowLikeKey = source.AllowLikeKey
        };
    }
}

internal sealed record LaunchResult(IBrowserContext Context, string? TempClonePath);

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
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 YaBrowser/24.4.0.0 Safari/537.36";
}

internal sealed class AutomationConfig
{
    public int WorkDurationMinutes { get; set; } = 60;
    public int ScrollDelayMinSeconds { get; set; } = 3;
    public int ScrollDelayMaxSeconds { get; set; } = 12;
    public string ScrollSelector { get; set; } = "button[data-e2e='arrow-right']";
    public int LikesPerPeriod { get; set; } = 2;
    public int LikePeriodMinutes { get; set; } = 10;
    public string LikeKey { get; set; } = "KeyL";
    public string LikeButtonSelector { get; set; } = "button[data-e2e='like-icon']";
    public bool AllowLikeButton { get; set; } = true;
    public bool AllowLikeKey { get; set; } = true;
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
