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

    public async Task RunAsync()
    {
        EnsureConfigExists();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== ПУНКТ УПРАВЛЕНИЯ TTWWorker ===");
            Console.WriteLine("1) Запустить автоматизацию");
            Console.WriteLine("2) Настроить простую автоматизацию");
            Console.WriteLine("3) Показать текущий JSON");
            Console.WriteLine("0) Выход");
            Console.Write("Выбор: ");

            var input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1":
                    await RunAutomationAsync();
                    break;
                case "2":
                    await ConfigureSimpleAutomationAsync();
                    break;
                case "3":
                    ShowCurrentJson();
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
        {
            return;
        }

        var defaultConfig = AppConfig.CreateDefault();
        File.WriteAllText(_configPath, JsonSerializer.Serialize(defaultConfig, _jsonOptions));
    }

    private async Task ConfigureSimpleAutomationAsync()
    {
        var config = LoadConfig();

        Console.Write("URL (пусто = оставить текущий): ");
        var url = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(url))
        {
            config.StartUrl = url;
        }

        Console.Write("Минимальная задержка (сек): ");
        if (int.TryParse(Console.ReadLine(), out var minSeconds) && minSeconds > 0)
        {
            config.Automation.MinDelaySeconds = minSeconds;
        }

        Console.Write("Максимальная задержка (сек): ");
        if (int.TryParse(Console.ReadLine(), out var maxSeconds) && maxSeconds >= config.Automation.MinDelaySeconds)
        {
            config.Automation.MaxDelaySeconds = maxSeconds;
        }

        Console.Write("JSON-кнопка (keyPress/click): ");
        var actionType = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (actionType is "keypress" or "click")
        {
            config.Automation.ActionType = actionType;
        }

        if (config.Automation.ActionType == "keypress")
        {
            Console.Write("Клавиша (пусто = ArrowDown): ");
            var key = Console.ReadLine()?.Trim();
            config.Automation.Key = string.IsNullOrWhiteSpace(key) ? "ArrowDown" : key;
        }
        else
        {
            Console.Write("CSS-селектор кнопки справа ниже центра: ");
            var selector = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(selector))
            {
                config.Automation.Selector = selector;
            }
        }

        Console.Write("Клавиша лайка (пусто = KeyL): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey))
        {
            config.Automation.LikeKey = likeKey;
        }

        await SaveConfigAsync(config);
        Console.WriteLine("Настройки сохранены.");
    }

    private void ShowCurrentJson()
    {
        Console.WriteLine();
        Console.WriteLine(File.ReadAllText(_configPath));
    }

    private async Task RunAutomationAsync()
    {
        var initialConfig = LoadConfig();
        if (!File.Exists(initialConfig.BrowserExecutablePath))
        {
            Console.WriteLine($"Браузер не найден: {initialConfig.BrowserExecutablePath}");
            return;
        }

        using var playwright = await Playwright.CreateAsync();
        var runtimeUserDataDir = RuntimeProfileStore.CreateTempProfileDir();

        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
            runtimeUserDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                ExecutablePath = initialConfig.BrowserExecutablePath,
                Headless = false,
                Args = ["--new-window", "--no-first-run", "--no-default-browser-check"]
            });

        try
        {
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
            await page.GotoAsync(initialConfig.StartUrl);

            Console.WriteLine("Автоматизация запущена.");
            Console.WriteLine("Команды в любой момент: like | stop");

            var commandQueue = new ConcurrentQueue<string>();
            using var cts = new CancellationTokenSource();
            var inputTask = Task.Run(() => ReadCommands(commandQueue, cts.Token), cts.Token);

            while (!cts.Token.IsCancellationRequested)
            {
                var cfg = LoadConfig(); // ПОСТОЯННЫЙ РЕСКАН JSON
                await ExecuteCommandsAsync(page, cfg, commandQueue, cts);

                var delaySec = Random.Shared.Next(cfg.Automation.MinDelaySeconds, cfg.Automation.MaxDelaySeconds + 1);
                Console.WriteLine($"Ожидание {delaySec} сек... (рескан JSON выполнен)");
                await page.WaitForTimeoutAsync(delaySec * 1000);

                await ExecuteCommandsAsync(page, cfg, commandQueue, cts);
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                if (cfg.Automation.ActionType == "click" && !string.IsNullOrWhiteSpace(cfg.Automation.Selector))
                {
                    await page.ClickAsync(cfg.Automation.Selector);
                    Console.WriteLine($"Нажата JSON-кнопка по селектору: {cfg.Automation.Selector}");
                }
                else
                {
                    var key = string.IsNullOrWhiteSpace(cfg.Automation.Key) ? "ArrowDown" : cfg.Automation.Key;
                    await page.Keyboard.PressAsync(key);
                    Console.WriteLine($"Нажата клавиша из JSON: {key}");
                }
            }

            cts.Cancel();
            try { await inputTask; } catch { }
            Console.WriteLine("Автоматизация остановлена командой stop.");
        }
        finally
        {
            RuntimeProfileStore.TryDeleteDirectory(runtimeUserDataDir);
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

    private static async Task ExecuteCommandsAsync(IPage page, AppConfig cfg, ConcurrentQueue<string> queue, CancellationTokenSource cts)
    {
        while (queue.TryDequeue(out var cmd))
        {
            switch (cmd)
            {
                case "like":
                    var likeKey = string.IsNullOrWhiteSpace(cfg.Automation.LikeKey) ? "KeyL" : cfg.Automation.LikeKey;
                    await page.Keyboard.PressAsync(likeKey);
                    Console.WriteLine($"Команда like выполнена: нажата клавиша {likeKey}");
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

internal static class RuntimeProfileStore
{
    public static string CreateTempProfileDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "TTWWorker", $"runtime-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            Console.WriteLine($"Не удалось удалить временный профиль: {path}");
        }
    }
}

internal sealed class AppConfig
{
    public string StartUrl { get; set; } = "https://www.tiktok.com/foryou";
    public string BrowserExecutablePath { get; set; } = @"C:\Program Files (x86)\Yandex\YandexBrowser\Application\browser.exe";
    public SimpleAutomation Automation { get; set; } = new();

    public static AppConfig CreateDefault() => new();
}

internal sealed class SimpleAutomation
{
    public int MinDelaySeconds { get; set; } = 3;
    public int MaxDelaySeconds { get; set; } = 12;
    public string ActionType { get; set; } = "keyPress";
    public string Key { get; set; } = "ArrowDown";
    public string Selector { get; set; } = "";
    public string LikeKey { get; set; } = "KeyL";
}
