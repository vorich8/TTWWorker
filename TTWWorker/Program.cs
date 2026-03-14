using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

const string defaultConfigPath = "scenario.json";
var configPath = args.FirstOrDefault() ?? defaultConfigPath;

var app = new DolphinControlPanel(configPath);
await app.RunAsync();

internal sealed class DolphinControlPanel(string configPath)
{
    private readonly string _configPath = configPath;
    private readonly JsonSerializerOptions _json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public async Task RunAsync()
    {
        EnsureConfigExists();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== DOLPHIN ANTY CONTROL ===");
            Console.WriteLine("1) Запустить автоматизацию профиля");
            Console.WriteLine("2) Настроить автоматизацию");
            Console.WriteLine("3) Показать scenario.json");
            Console.WriteLine("0) Выход");
            Console.Write("Выбор: ");

            var cmd = Console.ReadLine()?.Trim();
            switch (cmd)
            {
                case "1":
                    await RunProfileAutomationAsync();
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
        if (File.Exists(_configPath)) return;
        File.WriteAllText(_configPath, JsonSerializer.Serialize(DolphinScenario.CreateDefault(), _json));
    }

    private async Task ConfigureAsync()
    {
        var cfg = LoadConfig();

        Console.Write($"Dolphin API URL (сейчас: {cfg.Dolphin.ApiBaseUrl}): ");
        var api = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(api)) cfg.Dolphin.ApiBaseUrl = api;

        Console.Write($"ID профиля Dolphin (сейчас: {cfg.Dolphin.ProfileId}): ");
        var profileId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(profileId)) cfg.Dolphin.ProfileId = profileId;

        var tokenPreview = string.IsNullOrWhiteSpace(cfg.Dolphin.ApiToken)
            ? "<не задан>"
            : MaskToken(cfg.Dolphin.ApiToken);
        Console.Write($"Dolphin API Token (сейчас: {tokenPreview}): ");
        var apiToken = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(apiToken)) cfg.Dolphin.ApiToken = apiToken;

        Console.Write($"URL старта (сейчас: {cfg.StartUrl}): ");
        var startUrl = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(startUrl)) cfg.StartUrl = startUrl;

        Console.Write($"Время работы, минут (сейчас: {cfg.Automation.WorkDurationMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var minutes) && minutes > 0)
            cfg.Automation.WorkDurationMinutes = minutes;

        Console.Write($"Мин. задержка листания, сек (сейчас: {cfg.Automation.ScrollDelayMinSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var minDelay) && minDelay > 0)
            cfg.Automation.ScrollDelayMinSeconds = minDelay;

        Console.Write($"Макс. задержка листания, сек (сейчас: {cfg.Automation.ScrollDelayMaxSeconds}): ");
        if (int.TryParse(Console.ReadLine(), out var maxDelay) && maxDelay >= cfg.Automation.ScrollDelayMinSeconds)
            cfg.Automation.ScrollDelayMaxSeconds = maxDelay;

        Console.Write($"Лайков за период (сейчас: {cfg.Automation.LikesPerPeriod}): ");
        if (int.TryParse(Console.ReadLine(), out var likes) && likes >= 0)
            cfg.Automation.LikesPerPeriod = likes;

        Console.Write($"Период лайков, минут (сейчас: {cfg.Automation.LikePeriodMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var periodMin) && periodMin > 0)
            cfg.Automation.LikePeriodMinutes = periodMin;

        Console.Write($"Клавиша листания (сейчас: {cfg.Automation.ScrollKey}): ");
        var scrollKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(scrollKey)) cfg.Automation.ScrollKey = scrollKey;

        Console.Write($"Клавиша лайка (сейчас: {cfg.Automation.LikeKey}): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey)) cfg.Automation.LikeKey = likeKey;

        cfg.AutomationConfigured = true;
        await SaveConfigAsync(cfg);
        Console.WriteLine("Сценарий сохранён.");
    }

    private async Task RunProfileAutomationAsync()
    {
        var cfg = LoadConfig();
        if (string.IsNullOrWhiteSpace(cfg.Dolphin.ProfileId))
        {
            Console.WriteLine("Не указан Dolphin ProfileId.");
            return;
        }

        if (!cfg.AutomationConfigured)
        {
            Console.WriteLine("Настройки автоматизации ещё не сохранены. Запускаю первичную настройку...");
            PromptAutomationSettings(cfg.Automation);
            cfg.AutomationConfigured = true;
            await SaveConfigAsync(cfg);
        }

        using var dolphin = new DolphinClient(cfg.Dolphin.ApiBaseUrl, cfg.Dolphin.ApiToken);

        DolphinStartResponse? start = null;
        try
        {
            start = await dolphin.StartProfileAsync(cfg.Dolphin.ProfileId);
            if (start is null)
            {
                Console.WriteLine("Dolphin API вернул пустой ответ при старте профиля.");
                return;
            }

            var endpoint = ResolveCdpEndpoint(start);
            Console.WriteLine($"Профиль запущен. CDP: {endpoint}");

            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.ConnectOverCDPAsync(endpoint);
            var context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            await page.GotoAsync(cfg.StartUrl);

            Console.WriteLine("Откройте VPN/проверьте страницу, затем нажмите Enter.");
            Console.ReadLine();

            var queue = new ConcurrentQueue<string>();
            using var cts = new CancellationTokenSource();
            _ = Task.Run(() => ReadCommands(queue, cts.Token), cts.Token);

            var likesState = new LikeScheduleState(DateTime.UtcNow, cfg.Automation);
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
                await ProcessScheduledLikesAsync(page, cfg.Automation, likesState);

                var delay = Random.Shared.Next(
                    Math.Max(1, cfg.Automation.ScrollDelayMinSeconds),
                    Math.Max(cfg.Automation.ScrollDelayMinSeconds, cfg.Automation.ScrollDelayMaxSeconds) + 1);

                Console.WriteLine($"Ожидание {delay} сек...");
                await page.WaitForTimeoutAsync(delay * 1000);

                await ProcessCommandsAsync(page, cfg.Automation, queue, cts);
                await ProcessScheduledLikesAsync(page, cfg.Automation, likesState);
                if (cts.IsCancellationRequested) break;

                await page.Keyboard.PressAsync(cfg.Automation.ScrollKey);
                Console.WriteLine($"Листание: {cfg.Automation.ScrollKey}");
            }

            cts.Cancel();
            await browser.CloseAsync();
        }
        catch (PlaywrightException ex)
        {
            Console.WriteLine("Ошибка Playwright при работе с Dolphin профилем:");
            Console.WriteLine(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("Ошибка HTTP при работе с Dolphin API:");
            Console.WriteLine(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine("Ошибка запуска Dolphin профиля:");
            Console.WriteLine(ex.Message);
        }
        finally
        {
            if (start is not null)
            {
                try
                {
                    await dolphin.StopProfileAsync(cfg.Dolphin.ProfileId);
                    Console.WriteLine("Профиль Dolphin остановлен.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не удалось остановить профиль Dolphin: {ex.Message}");
                }
            }
        }
    }

    private static string ResolveCdpEndpoint(DolphinStartResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.AutomationWsEndpoint))
            return response.AutomationWsEndpoint!;

        if (!string.IsNullOrWhiteSpace(response.WsEndpoint))
            return response.WsEndpoint!;

        if (response.Port is > 0)
            return $"http://127.0.0.1:{response.Port}";

        throw new InvalidOperationException("Dolphin не вернул wsEndpoint/port для CDP подключения.");
    }

    private static async Task ProcessScheduledLikesAsync(IPage page, AutomationConfig cfg, LikeScheduleState state)
    {
        if (cfg.LikesPerPeriod <= 0) return;

        if (DateTime.UtcNow >= state.PeriodEndUtc)
            state.Reset(cfg);

        while (state.TryDequeueDueLike(DateTime.UtcNow, out _))
        {
            await page.Keyboard.PressAsync(cfg.LikeKey);
            Console.WriteLine($"Плановый лайк: {cfg.LikeKey}");
        }
    }

    private static async Task ProcessCommandsAsync(IPage page, AutomationConfig cfg, ConcurrentQueue<string> q, CancellationTokenSource cts)
    {
        while (q.TryDequeue(out var cmd))
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
                    Console.WriteLine($"Неизвестная команда: {cmd}");
                    break;
            }
        }
    }

    private static void ReadCommands(ConcurrentQueue<string> q, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var cmd = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(cmd)) q.Enqueue(cmd.Trim().ToLowerInvariant());
        }
    }

    private DolphinScenario LoadConfig()
    {
        var json = File.ReadAllText(_configPath);
        try
        {
            return JsonSerializer.Deserialize<DolphinScenario>(json, _json) ?? DolphinScenario.CreateDefault();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Ошибка JSON: {ex.Message}");
            return DolphinScenario.CreateDefault();
        }
    }

    private static void PromptAutomationSettings(AutomationConfig automation)
    {
        Console.Write($"Время работы, минут (сейчас: {automation.WorkDurationMinutes}): ");
        if (int.TryParse(Console.ReadLine(), out var minutes) && minutes > 0)
            automation.WorkDurationMinutes = minutes;

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
        if (int.TryParse(Console.ReadLine(), out var periodMin) && periodMin > 0)
            automation.LikePeriodMinutes = periodMin;

        Console.Write($"Клавиша листания (сейчас: {automation.ScrollKey}): ");
        var scrollKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(scrollKey)) automation.ScrollKey = scrollKey;

        Console.Write($"Клавиша лайка (сейчас: {automation.LikeKey}): ");
        var likeKey = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(likeKey)) automation.LikeKey = likeKey;
    }

    private Task SaveConfigAsync(DolphinScenario cfg) => File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(cfg, _json));

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "<не задан>";

        if (token.Length <= 10)
            return new string('*', token.Length);

        return $"{token[..6]}...{token[^4..]}";
    }
}

internal sealed class DolphinClient : IDisposable
{
    private readonly HttpClient _http;

    public DolphinClient(string apiBaseUrl, string? apiToken)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/")
        };

        if (!string.IsNullOrWhiteSpace(apiToken))
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<DolphinStartResponse?> StartProfileAsync(string profileId)
    {
        var attempts = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, $"v1.0/browser_profiles/{profileId}/start?automation=1"),
            (HttpMethod.Get, $"browser_profiles/{profileId}/start?automation=1"),
            (HttpMethod.Post, $"v1.0/browser_profiles/{profileId}/start?automation=1"),
            (HttpMethod.Post, $"browser_profiles/{profileId}/start?automation=1")
        };

        var errors = new List<string>();
        var sawFreePlanAutomationError = false;
        var sawInvalidSessionToken = false;
        var sawSuccessWithoutEndpoint = false;
        var sawAlreadyRunning = false;

        foreach (var attempt in attempts)
        {
            using var request = new HttpRequestMessage(attempt.Method, attempt.Path);
            using var response = await _http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired && raw.Contains("free plan", StringComparison.OrdinalIgnoreCase))
                    sawFreePlanAutomationError = true;

                if (raw.Contains("invalid session token", StringComparison.OrdinalIgnoreCase))
                    sawInvalidSessionToken = true;

                if (IsProfileAlreadyRunningError(raw))
                    sawAlreadyRunning = true;

                errors.Add($"{attempt.Method} {attempt.Path} -> {(int)response.StatusCode} {response.StatusCode}. Body: {TrimForLog(raw)}");
                continue;
            }

            var parsed = TryParseStartResponse(raw, out var parseError);
            if (parsed is not null)
                return parsed;

            if (raw.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase))
                sawSuccessWithoutEndpoint = true;

            errors.Add($"{attempt.Method} {attempt.Path} -> OK, но не удалось разобрать ответ API: {parseError}. Body: {TrimForLog(raw)}");
        }

        if (sawAlreadyRunning || sawSuccessWithoutEndpoint || sawFreePlanAutomationError)
        {
            var existing = await TryAttachToRunningProfileAsync(profileId, errors);
            if (existing is not null)
                return existing;
        }

        var hints = new List<string>();
        if (sawFreePlanAutomationError)
        {
            hints.Add("Dolphin вернул 'On free plan you can't use automation' — профиль может открываться, но CDP endpoint для Playwright не выдаётся на free-плане.");
            hints.Add("Варианты: подключить платный тариф с automation API или перейти на локальный браузерный режим без Dolphin API automation.");
        }

        if (sawAlreadyRunning)
            hints.Add("Профиль уже был запущен вручную. Приложение попыталось получить endpoint уже запущенного профиля без его остановки.");

        if ((sawSuccessWithoutEndpoint || sawFreePlanAutomationError) && !sawAlreadyRunning)
            hints.Add("Приложение дополнительно пыталось получить endpoint через info/automation endpoint уже после старта профиля.");

        if (sawInvalidSessionToken)
            hints.Add("Есть ответы 'invalid session token' — проверьте `dolphin.apiToken` в scenario.json и повторите запуск.");

        if (sawSuccessWithoutEndpoint)
            hints.Add("API сообщил success=true, но не вернул ws/port. Это означает: профиль запустился, но к автоматизации подключиться нельзя без automation endpoint.");

        throw new InvalidOperationException(
            "Не удалось запустить профиль через Dolphin API. " +
            "Проверьте корректность profileId и доступность локального API. Попытки:\n" +
            string.Join("\n", errors) +
            (hints.Count > 0 ? "\n\nЧто это значит:\n- " + string.Join("\n- ", hints) : string.Empty));
    }

    private async Task<DolphinStartResponse?> TryAttachToRunningProfileAsync(string profileId, List<string> errors)
    {
        var paths = new[]
        {
            $"v1.0/browser_profiles/{profileId}",
            $"browser_profiles/{profileId}",
            $"v1.0/browser_profiles/{profileId}/automation"
        };

        foreach (var path in paths)
        {
            try
            {
                using var response = await _http.GetAsync(path);
                var raw = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"GET {path} -> {(int)response.StatusCode} {response.StatusCode}. Body: {TrimForLog(raw)}");
                    continue;
                }

                var parsed = TryParseStartResponse(raw, out var parseError);
                if (parsed is not null)
                {
                    errors.Add($"GET {path} -> найден endpoint запущенного профиля.");
                    return parsed;
                }

                errors.Add($"GET {path} -> OK, но endpoint не найден: {parseError}. Body: {TrimForLog(raw)}");
            }
            catch (Exception ex)
            {
                errors.Add($"GET {path} -> ошибка: {ex.Message}");
            }
        }

        return null;
    }

    private static bool IsProfileAlreadyRunningError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return raw.Contains("E_BROWSER_RUN_DUPLICATE", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("already running", StringComparison.OrdinalIgnoreCase)
               || raw.Contains("уже запущ", StringComparison.OrdinalIgnoreCase);
    }

    public async Task StopProfileAsync(string profileId)
    {
        var response = await _http.GetAsync($"v1.0/browser_profiles/{profileId}/stop");
        response.EnsureSuccessStatusCode();
    }

    private static int? TryGetInt(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var si)) return si;
        return null;
    }

    private static string? TryGetString(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static DolphinStartResponse? TryParseStartResponse(string raw, out string? parseError)
    {
        parseError = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var data = root.TryGetProperty("automation", out var automation)
                ? automation
                : root.TryGetProperty("data", out var dataNode) ? dataNode : root;

            var ws = TryGetString(data, "wsEndpoint")
                ?? TryGetString(data, "ws_endpoint")
                ?? TryGetString(data, "browserWSEndpoint")
                ?? TryGetString(root, "wsEndpoint")
                ?? TryGetString(root, "browserWSEndpoint");

            var autoWs = TryGetString(data, "automationWsEndpoint")
                ?? TryGetString(data, "automation_ws_endpoint")
                ?? ws;

            var port = TryGetInt(data, "port")
                ?? TryGetInt(root, "port")
                ?? TryGetInt(data, "automationPort")
                ?? TryGetInt(root, "automationPort");

            if (string.IsNullOrWhiteSpace(autoWs) && string.IsNullOrWhiteSpace(ws) && port is null)
                TryExtractEndpointDeep(root, out ws, out port);

            if (string.IsNullOrWhiteSpace(autoWs))
                autoWs = ws;

            if (string.IsNullOrWhiteSpace(autoWs) && string.IsNullOrWhiteSpace(ws) && port is null)
            {
                parseError = "в ответе нет ws endpoint и порта";
                return null;
            }

            return new DolphinStartResponse
            {
                Port = port,
                WsEndpoint = ws,
                AutomationWsEndpoint = autoWs
            };
        }
        catch (JsonException ex)
        {
            parseError = ex.Message;
            return null;
        }
    }

    private static void TryExtractEndpointDeep(JsonElement node, out string? wsEndpoint, out int? port)
    {
        wsEndpoint = null;
        port = null;
        ExtractRecursive(node, ref wsEndpoint, ref port);
    }

    private static void ExtractRecursive(JsonElement node, ref string? wsEndpoint, ref int? port)
    {
        if (!string.IsNullOrWhiteSpace(wsEndpoint) && port is not null)
            return;

        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in node.EnumerateObject())
                {
                    var name = prop.Name;
                    var val = prop.Value;

                    if (val.ValueKind == JsonValueKind.String)
                    {
                        var s = val.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (IsWsLikeString(s) && string.IsNullOrWhiteSpace(wsEndpoint))
                                wsEndpoint = s;

                            if (port is null && TryParsePortFromString(s, out var parsedPort))
                                port = parsedPort;
                        }
                    }
                    else if (val.ValueKind == JsonValueKind.Number && port is null
                             && name.Contains("port", StringComparison.OrdinalIgnoreCase)
                             && val.TryGetInt32(out var p))
                    {
                        port = p;
                    }

                    ExtractRecursive(val, ref wsEndpoint, ref port);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                    ExtractRecursive(item, ref wsEndpoint, ref port);
                break;

            case JsonValueKind.String:
                var str = node.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    if (IsWsLikeString(str) && string.IsNullOrWhiteSpace(wsEndpoint))
                        wsEndpoint = str;

                    if (port is null && TryParsePortFromString(str, out var parsedPort))
                        port = parsedPort;
                }
                break;
        }
    }

    private static bool IsWsLikeString(string value)
    {
        return value.Contains("ws://", StringComparison.OrdinalIgnoreCase)
               || value.Contains("wss://", StringComparison.OrdinalIgnoreCase)
               || value.Contains("/devtools/browser/", StringComparison.OrdinalIgnoreCase)
               || value.Contains("127.0.0.1:", StringComparison.OrdinalIgnoreCase)
               || value.Contains("localhost:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePortFromString(string value, out int port)
    {
        port = 0;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Port > 0)
        {
            port = uri.Port;
            return true;
        }

        var chunks = value.Split(new[] { ':', '/', '?', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var chunk in chunks.Reverse())
        {
            if (int.TryParse(chunk, out var parsed) && parsed is > 0 and <= 65535)
            {
                port = parsed;
                return true;
            }
        }

        return false;
    }

    private static string TrimForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        const int max = 500;
        var singleLine = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= max ? singleLine : singleLine[..max] + "...";
    }

    public void Dispose() => _http.Dispose();
}

internal sealed class DolphinScenario
{
    public string StartUrl { get; set; } = "https://www.tiktok.com/foryou";
    public DolphinConfig Dolphin { get; set; } = new();
    public AutomationConfig Automation { get; set; } = new();
    public bool AutomationConfigured { get; set; }

    public static DolphinScenario CreateDefault() => new();
}

internal sealed class DolphinConfig
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:3001";
    public string ProfileId { get; set; } = "";
    public string ApiToken { get; set; } = "";
}

internal sealed class DolphinStartResponse
{
    public int? Port { get; set; }
    public string? WsEndpoint { get; set; }
    public string? AutomationWsEndpoint { get; set; }
}

internal static class LikeScheduler
{
    public static LikeScheduleState CreatePlan(DateTime startUtc, AutomationConfig cfg) => new(startUtc, cfg);
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
        if (cfg.LikesPerPeriod <= 0) return;

        var totalSec = (int)TimeSpan.FromMinutes(mins).TotalSeconds;
        var offsets = Enumerable.Range(0, cfg.LikesPerPeriod)
            .Select(_ => Random.Shared.Next(1, Math.Max(2, totalSec)))
            .OrderBy(x => x);

        foreach (var o in offsets)
            _queue.Enqueue(startUtc.AddSeconds(o));
    }

    public bool TryDequeueDueLike(DateTime nowUtc, out DateTime dueUtc)
    {
        dueUtc = default;
        if (_queue.Count == 0 || _queue.Peek() > nowUtc) return false;
        dueUtc = _queue.Dequeue();
        return true;
    }
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
