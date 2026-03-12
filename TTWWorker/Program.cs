using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

const string defaultConfigPath = "scenario.json";
var configPath = args.FirstOrDefault() ?? defaultConfigPath;

var app = new ControlPanel(configPath);
await app.RunAsync();

internal sealed class ControlPanel(string configPath)
{
    private readonly string _configPath = configPath;
    private readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private readonly BrowserProfileStore _profileStore = new(Path.Combine(AppContext.BaseDirectory, "managed-profiles"));


    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task RunAsync()
    {
        EnsureConfigExists();
        _profileStore.EnsureStoreExists();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== ПУНКТ УПРАВЛЕНИЯ TTWWorker ===");
            Console.WriteLine("1) Запустить профиль");
            Console.WriteLine("2) Настроить глобальные значения по умолчанию");
            Console.WriteLine("3) Показать текущий JSON");
            Console.WriteLine("4) Управление профилями");
            Console.WriteLine("0) Выход");
            Console.Write("Выбор: ");

            var input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1":
                    await RunProfileAsync();
                    break;
                case "2":
                    await ConfigureGlobalDefaultsAsync();
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

    private async Task ConfigureGlobalDefaultsAsync()
    {
        var config = LoadConfig();
        ConfigureAutomationValues(config.DefaultAutomation, config.StartUrl, allowStartUrlEdit: true, out var updatedUrl);
        if (!string.IsNullOrWhiteSpace(updatedUrl))
        {
            config.StartUrl = updatedUrl;
        }

        Console.Write($"Режим запуска (PlaywrightPersistent / AutoStartAndAttach / AttachToExisting), сейчас: {config.LaunchMode}: ");
        var launchMode = Console.ReadLine()?.Trim();
        if (Enum.TryParse<BrowserLaunchMode>(launchMode, ignoreCase: true, out var parsedMode))
        {
            config.LaunchMode = parsedMode;
        }

        Console.Write($"CDP порт (сейчас: {config.CdpPort}): ");
        if (int.TryParse(Console.ReadLine(), out var cdpPort) && cdpPort > 0)
        {
            config.CdpPort = cdpPort;
        }

        await SaveConfigAsync(config);
        Console.WriteLine("Глобальные значения по умолчанию сохранены в scenario.json");
    }

    private void ConfigureAutomationValues(AutomationConfig automation, string currentUrl, bool allowStartUrlEdit, out string? updatedStartUrl)
    {
        updatedStartUrl = null;

        if (allowStartUrlEdit)
        {
            Console.Write($"URL (сейчас: {currentUrl}): ");
            var url = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(url)) updatedStartUrl = url;
        }

        Console.Write($"Время работы в минутах (сейчас: {automation.WorkDurationMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var workMin) && workMin > 0) automation.WorkDurationMinutes = workMin;

        Console.Write($"Мин. задержка листания в сек (сейчас: {automation.ScrollDelayMinSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var minDelay) && minDelay > 0) automation.ScrollDelayMinSeconds = minDelay;

        Console.Write($"Макс. задержка листания в сек (сейчас: {automation.ScrollDelayMaxSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var maxDelay) && maxDelay >= automation.ScrollDelayMinSeconds) automation.ScrollDelayMaxSeconds = maxDelay;

        Console.Write($"Действие листания keyPress/click (сейчас: {automation.ScrollActionType}): ");
        var actionType = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (actionType is "keypress" or "click") automation.ScrollActionType = actionType;

        if (automation.ScrollActionType == "keypress")
        {
            Console.Write($"Клавиша листания (сейчас: {automation.ScrollKey}): ");
            var key = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(key)) automation.ScrollKey = key;
        }
        else
        {
            Console.Write($"Селектор кнопки листания (сейчас: {automation.ScrollSelector}): ");
            var selector = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(selector)) automation.ScrollSelector = selector;
        }

        Console.Write($"Лайков за период (сейчас: {automation.LikesPerPeriod}): ");
        if (int.TryParse(Console.ReadLine(), out var likesPerPeriod) && likesPerPeriod >= 0) automation.LikesPerPeriod = likesPerPeriod;

        Console.Write($"Длина периода лайков в минутах (сейчас: {automation.LikePeriodMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var likePeriod) && likePeriod > 0) automation.LikePeriodMinutes = likePeriod;

        Console.Write($"Клавиша лайка (сейчас: {automation.LikeKey}): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey)) automation.LikeKey = likeKey;
    }

    private void ManageProfiles()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== УПРАВЛЕНИЕ ПРОФИЛЯМИ ===");
            var profiles = _profileStore.GetProfiles();
            if (profiles.Count == 0) Console.WriteLine("Профилей нет. Создайте новый (команда n).");
            else foreach (var p in profiles) Console.WriteLine($"- {p.Name}");

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
                continue;
            }

            if (cmd == "d")
            {
                Console.Write("Имя профиля для удаления: ");
                var name = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && _profileStore.DeleteProfile(name)) Console.WriteLine($"Профиль удалён: {name}");
                else Console.WriteLine("Не удалось удалить профиль.");
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
            for (var i = 0; i < profiles.Count; i++) Console.WriteLine($"{i + 1}) {profiles[i].Name}");
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

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= profiles.Count) return profiles[idx - 1];
            Console.WriteLine("Некорректный выбор.");
        }
    }

    private async Task RunProfileAsync()
    {
        var selectedProfile = SelectProfileForRun();
        var config = LoadConfig();

        if (!File.Exists(config.BrowserExecutablePath))
        {
            Console.WriteLine($"Браузер не найден: {config.BrowserExecutablePath}");
            return;
        }

        var profileSettings = ProfileSettingsStore.LoadOrCreate(selectedProfile, config.DefaultAutomation, _jsonOptions);

        using var playwright = await Playwright.CreateAsync();
        await using var session = await StartSessionWithFallbackAsync(playwright, config, selectedProfile);

        var page = session.Page;
        Console.WriteLine($"Открыт TikTok для профиля {selectedProfile.Name} (ручной режим запуска браузера).");
        Console.WriteLine("Сейчас введите/подтвердите настройки (Enter = оставить текущее сохранённое значение).");

        ConfigureAutomationValues(profileSettings.Automation, config.StartUrl, allowStartUrlEdit: false, out _);
        ProfileSettingsStore.Save(selectedProfile, profileSettings, _jsonOptions);
        Console.WriteLine("Настройки профиля сохранены навсегда и будут использоваться в следующих запусках.");

        Console.WriteLine("Откройте/проверьте VPN-вкладку вручную в этом окне браузера.");
        Console.WriteLine("Когда VPN готов, нажмите Enter для старта автоматизации...");
        Console.ReadLine();

        Console.WriteLine("Автоматизация запущена. Команды: like | stop");

        var queue = new ConcurrentQueue<string>();
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() => ReadCommands(queue, cts.Token), cts.Token);

        var runStartedAt = DateTime.UtcNow;
        var likesPlan = LikeScheduler.CreatePlan(DateTime.UtcNow, profileSettings.Automation);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                config = LoadConfig();
                profileSettings = ProfileSettingsStore.LoadOrCreate(selectedProfile, config.DefaultAutomation, _jsonOptions);
                var a = profileSettings.Automation;

                if (DateTime.UtcNow >= runStartedAt.AddMinutes(Math.Max(1, a.WorkDurationMinutes)))
                {
                    Console.WriteLine("Время работы вышло. Автоматизация завершена.");
                    break;
                }

                await ExecuteCommandsAsync(page, a, queue, cts);
                await ExecuteScheduledLikesAsync(page, a, likesPlan);

                var delaySec = Random.Shared.Next(Math.Max(1, a.ScrollDelayMinSeconds), Math.Max(a.ScrollDelayMinSeconds, a.ScrollDelayMaxSeconds) + 1);
                Console.WriteLine($"Ожидание {delaySec} сек...");
                await page.WaitForTimeoutAsync(delaySec * 1000);

                await ExecuteCommandsAsync(page, a, queue, cts);
                await ExecuteScheduledLikesAsync(page, a, likesPlan);
                if (cts.Token.IsCancellationRequested) break;

                if (a.ScrollActionType == "click" && !string.IsNullOrWhiteSpace(a.ScrollSelector))
                {
                    await page.ClickAsync(a.ScrollSelector);
                    Console.WriteLine($"Листание: click по {a.ScrollSelector}");
                }
                else
                {
                    var key = string.IsNullOrWhiteSpace(a.ScrollKey) ? "ArrowDown" : a.ScrollKey;
                    await page.Keyboard.PressAsync(key);
                    Console.WriteLine($"Листание: keyPress {key}");
                }
            }
        }
        catch (PlaywrightException ex)
        {
            Console.WriteLine("Сессия браузера была закрыта во время работы.");
            Console.WriteLine($"Детали: {ex.Message}");
            Console.WriteLine("Рекомендация: попробуйте режим PlaywrightPersistent или AttachToExisting.");
        }

        cts.Cancel();
    }

    private async Task<ManualBrowserSession> StartSessionWithFallbackAsync(IPlaywright playwright, AppConfig config, BrowserProfile selectedProfile)
    {
        var modes = new List<BrowserLaunchMode>
        {
            config.LaunchMode,
            BrowserLaunchMode.PlaywrightPersistent,
            BrowserLaunchMode.AutoStartAndAttach,
            BrowserLaunchMode.AttachToExisting
        }.Distinct().ToList();

        Exception? last = null;
        foreach (var mode in modes)
        {
            try
            {
                Console.WriteLine($"Пробую режим запуска: {mode}");
                return mode switch
                {
                    BrowserLaunchMode.PlaywrightPersistent => await ManualBrowserConnector.StartPersistentAsync(
                        playwright,
                        config.BrowserExecutablePath,
                        selectedProfile.UserDataDir,
                        config.StartUrl),
                    BrowserLaunchMode.AttachToExisting => await ManualBrowserConnector.AttachToExistingAsync(
                        playwright,
                        config.BrowserExecutablePath,
                        selectedProfile.UserDataDir,
                        config.StartUrl,
                        config.CdpPort),
                    _ => await ManualBrowserConnector.StartAndConnectAsync(
                        playwright,
                        config.BrowserExecutablePath,
                        selectedProfile.UserDataDir,
                        config.StartUrl,
                        config.CdpPort),
                };
            }
            catch (Exception ex)
            {
                last = ex;
                Console.WriteLine($"Режим {mode} не сработал: {ex.Message}");
            }
        }

        throw new InvalidOperationException($"Не удалось запустить сессию ни в одном режиме. Последняя ошибка: {last?.Message}");
    }

    private static void ReadCommands(ConcurrentQueue<string> queue, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var command = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(command)) queue.Enqueue(command.Trim().ToLowerInvariant());
        }
    }

    private static async Task ExecuteScheduledLikesAsync(IPage page, AutomationConfig cfg, LikeScheduleState state)
    {
        if (cfg.LikesPerPeriod <= 0) return;
        if (DateTime.UtcNow >= state.PeriodEndUtc) state.Reset(cfg);

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

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? AppConfig.CreateDefault();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Ошибка чтения scenario.json: {ex.Message}");
            Console.WriteLine("Использую настройки по умолчанию для продолжения работы.");
            return AppConfig.CreateDefault();
        }
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
        var next = 1;
        var existing = GetProfiles().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (existing.Contains($"Profile {next}")) next++;

        var name = $"Profile {next}";
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

internal static class ProfileSettingsStore
{
    private const string FileName = "automation-settings.json";

    public static ProfileSettings LoadOrCreate(BrowserProfile profile, AutomationConfig defaults, JsonSerializerOptions options)
    {
        var path = GetPath(profile);
        if (!File.Exists(path))
        {
            var created = new ProfileSettings { Automation = defaults.Clone() };
            Save(profile, created, options);
            return created;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProfileSettings>(json, options)
            ?? new ProfileSettings { Automation = defaults.Clone() };
    }

    public static void Save(BrowserProfile profile, ProfileSettings settings, JsonSerializerOptions options)
    {
        var path = GetPath(profile);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, options));
    }

    private static string GetPath(BrowserProfile profile) => Path.Combine(profile.UserDataDir, FileName);
}

internal sealed class ProfileSettings
{
    public AutomationConfig Automation { get; set; } = new();
}

internal sealed class ManualBrowserSession(IBrowser browser, IPage page, Process process, IBrowserContext? ownedContext = null) : IAsyncDisposable
{
    public IPage Page { get; } = page;

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (ownedContext is not null)
            {
                await ownedContext.CloseAsync();
            }
            else
            {
                await browser.CloseAsync();
            }
        }
        catch { }
        try
        {
            if (process.Id != Process.GetCurrentProcess().Id && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch { }
    }
}

internal static class ManualBrowserConnector
{
    public static async Task<ManualBrowserSession> StartPersistentAsync(IPlaywright playwright, string executablePath, string userDataDir, string startUrl)
    {
        var context = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                ExecutablePath = executablePath,
                Headless = false,
                Args = ["--new-window", "--no-first-run", "--no-default-browser-check"]
            });

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        await page.GotoAsync(startUrl);

        var browser = context.Browser ?? throw new InvalidOperationException("Не удалось получить Browser из persistent context.");
        return new ManualBrowserSession(browser, page, Process.GetCurrentProcess(), context);
    }

    public static async Task<ManualBrowserSession> StartAndConnectAsync(IPlaywright playwright, string executablePath, string userDataDir, string startUrl, int preferredPort)
    {
        Directory.CreateDirectory(userDataDir);

        Exception? lastError = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var requestedPort = await PickFreePortAsync(preferredPort + (attempt - 1) * 20);
            Process? process = null;

            try
            {
                var startInfo = new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = false,
                };

                startInfo.ArgumentList.Add($"--remote-debugging-port={requestedPort}");
                startInfo.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
                startInfo.ArgumentList.Add($"--user-data-dir={userDataDir}");
                startInfo.ArgumentList.Add("--new-window");
                startInfo.ArgumentList.Add(startUrl);

                process = Process.Start(startInfo) ?? throw new InvalidOperationException("Не удалось запустить браузер вручную.");
                Console.WriteLine($"Запущен процесс браузера PID={process.Id}, ожидаю CDP...");

                var timeoutMs = attempt == 1 ? 30000 : 60000;
                var actualPort = await WaitForCdpAsync(requestedPort, userDataDir, timeoutMs, process);

                Console.WriteLine($"CDP поднялся на порту {actualPort}.");
                var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{actualPort}");
                var context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();
                var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
                return new ManualBrowserSession(browser, page, process);
            }
            catch (Exception ex)
            {
                lastError = ex;
                Console.WriteLine($"Не удалось поднять CDP (попытка {attempt}/2): {ex.Message}");
                Console.WriteLine("Если включен VPN/расширения — запуск может быть медленнее, выполняю повторную попытку...");

                try
                {
                    if (process is not null && !process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
            }
        }

        throw new InvalidOperationException(
            "CDP endpoint не поднялся даже после повторной попытки. " +
            $"Проверьте, не открыт ли этот же профиль в другом окне, и что путь браузера корректный. Детали: {lastError?.Message}");
    }

    public static async Task<ManualBrowserSession> AttachToExistingAsync(
        IPlaywright playwright,
        string executablePath,
        string userDataDir,
        string startUrl,
        int cdpPort)
    {
        Console.WriteLine("Режим AttachToExisting: основной браузер не закрывается и новый процесс не стартует.");
        Console.WriteLine("1) В отдельном окне/профиле запустите браузер вручную с CDP:");
        Console.WriteLine($"   \"{executablePath}\" --remote-debugging-port={cdpPort} --remote-debugging-address=127.0.0.1 --user-data-dir=\"{userDataDir}\" --new-window {startUrl}");
        Console.WriteLine("2) Дождитесь открытия TikTok и нажмите Enter для подключения...");
        Console.ReadLine();

        await WaitForCdpPortOnlyAsync(cdpPort, 90000);
        Console.WriteLine($"CDP обнаружен на порту {cdpPort}, подключаюсь...");

        var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{cdpPort}");
        var context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

        return new ManualBrowserSession(browser, page, Process.GetCurrentProcess());
    }

    private static async Task WaitForCdpPortOnlyAsync(int port, int timeoutMs)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (await IsPortReadyAsync(port)) return;
            await Task.Delay(400);
        }

        throw new InvalidOperationException($"CDP endpoint на порту {port} не поднялся за {timeoutMs / 1000} сек.");
    }

    private static async Task<int> PickFreePortAsync(int preferred)
    {
        if (!await IsPortReadyAsync(preferred)) return preferred;
        for (var p = preferred + 1; p < preferred + 50; p++)
        {
            if (!await IsPortReadyAsync(p)) return p;
        }
        throw new InvalidOperationException("Не удалось подобрать свободный порт CDP.");
    }

    private static async Task<bool> IsPortReadyAsync(int port)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(700) };
        try
        {
            using var r = await client.GetAsync($"http://127.0.0.1:{port}/json/version");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static bool TryReadDevToolsPort(string userDataDir, out int port)
    {
        port = 0;
        var file = Path.Combine(userDataDir, "DevToolsActivePort");
        if (!File.Exists(file)) return false;

        try
        {
            var firstLine = File.ReadLines(file).FirstOrDefault();
            return int.TryParse(firstLine, out port) && port > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int> WaitForCdpAsync(int requestedPort, string userDataDir, int timeoutMs, Process process)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (await IsPortReadyAsync(requestedPort)) return requestedPort;

            if (TryReadDevToolsPort(userDataDir, out var discoveredPort) && await IsPortReadyAsync(discoveredPort))
            {
                return discoveredPort;
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException($"Браузер завершился до поднятия CDP. Код выхода: {process.ExitCode}. Возможно, уже был запущен основной браузер с тем же профилем и новый процесс передал ему команду.");
            }

            await Task.Delay(300);
        }

        var devToolsFile = Path.Combine(userDataDir, "DevToolsActivePort");
        var devToolsHint = File.Exists(devToolsFile) ? $"Файл {devToolsFile} существует, но порт недоступен." : $"Файл {devToolsFile} не создан.";
        throw new InvalidOperationException($"CDP endpoint не поднялся за {timeoutMs / 1000} сек. {devToolsHint}");
    }
}

internal static class LikeScheduler
{
    public static LikeScheduleState CreatePlan(DateTime nowUtc, AutomationConfig cfg) => new(nowUtc, cfg);
}

internal sealed class LikeScheduleState
{
    private readonly Queue<DateTime> _plannedLikesUtc = new();
    public DateTime PeriodEndUtc { get; private set; }

    public LikeScheduleState(DateTime periodStartUtc, AutomationConfig cfg)
    {
        Reset(periodStartUtc, cfg);
    }

    public void Reset(AutomationConfig cfg) => Reset(DateTime.UtcNow, cfg);

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

        foreach (var offset in offsets) _plannedLikesUtc.Enqueue(periodStartUtc.AddSeconds(offset));
    }

    public bool TryDequeueDueLike(DateTime nowUtc, out DateTime dueUtc)
    {
        dueUtc = default;
        if (_plannedLikesUtc.Count == 0 || _plannedLikesUtc.Peek() > nowUtc) return false;
        dueUtc = _plannedLikesUtc.Dequeue();
        return true;
    }
}

internal sealed class AppConfig
{
    public string StartUrl { get; set; } = "https://www.tiktok.com/foryou";
    public string BrowserExecutablePath { get; set; } = @"C:\Program Files (x86)\Yandex\YandexBrowser\Application\browser.exe";
    public BrowserLaunchMode LaunchMode { get; set; } = BrowserLaunchMode.PlaywrightPersistent;
    public int CdpPort { get; set; } = 9222;
    public AutomationConfig DefaultAutomation { get; set; } = new();

    public static AppConfig CreateDefault() => new();
}

internal enum BrowserLaunchMode
{
    PlaywrightPersistent,
    AutoStartAndAttach,
    AttachToExisting
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

    public AutomationConfig Clone() => new()
    {
        WorkDurationMinutes = WorkDurationMinutes,
        ScrollDelayMinSeconds = ScrollDelayMinSeconds,
        ScrollDelayMaxSeconds = ScrollDelayMaxSeconds,
        ScrollActionType = ScrollActionType,
        ScrollKey = ScrollKey,
        ScrollSelector = ScrollSelector,
        LikesPerPeriod = LikesPerPeriod,
        LikePeriodMinutes = LikePeriodMinutes,
        LikeKey = LikeKey,
    };
}
