using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

const string defaultScenarioPath = "scenario.json";
var scenarioPath = args.FirstOrDefault() ?? defaultScenarioPath;

if (!File.Exists(scenarioPath))
{
    Console.WriteLine($"Файл сценария не найден: {scenarioPath}");
    Console.WriteLine("Пример запуска: dotnet run -- <путь-к-scenario.json>");
    return;
}

var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
};
options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

var json = await File.ReadAllTextAsync(scenarioPath);
var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, options);

if (scenario is null)
{
    Console.WriteLine("Не удалось разобрать JSON сценария.");
    return;
}

if (string.IsNullOrWhiteSpace(scenario.StartUrl))
{
    Console.WriteLine("В сценарии обязательно поле startUrl.");
    return;
}

if (!BrowserPathResolver.TryResolveExecutablePath(scenario.Browser.ExecutablePath, out var executablePath, out var checkedExecutablePaths))
{
    Console.WriteLine("Не удалось найти исполняемый файл браузера.");
    Console.WriteLine("Проверенные пути:");
    foreach (var path in checkedExecutablePaths)
    {
        Console.WriteLine($" - {path}");
    }

    return;
}

if (!BrowserPathResolver.TryResolveUserDataDir(scenario.Browser.UserDataDir, out var userDataDir, out var checkedUserDataDirs))
{
    Console.WriteLine("Не удалось найти папку данных профиля браузера (userDataDir).");
    Console.WriteLine("Проверенные пути:");
    foreach (var path in checkedUserDataDirs)
    {
        Console.WriteLine($" - {path}");
    }

    return;
}

Console.WriteLine($"Сценарий загружен: {scenario.Name}");
Console.WriteLine($"Шагов в сценарии: {scenario.Steps.Count}");
Console.WriteLine($"Браузер: {executablePath}");
Console.WriteLine($"Папка профилей: {userDataDir}");
Console.WriteLine($"Профиль: {scenario.Browser.ProfileDirectoryName}");
Console.WriteLine($"Режим запуска: {scenario.Browser.LaunchMode}");

using var playwright = await Playwright.CreateAsync();
BrowserSession? session = null;

try
{
    session = await BrowserLauncher.LaunchAsync(playwright, scenario, executablePath, userDataDir);

    Console.WriteLine($"Переход на: {scenario.StartUrl}");
    await session.Page.GotoAsync(scenario.StartUrl);

    if (scenario.LoginWaitSeconds > 0)
    {
        Console.WriteLine($"Ожидание перед стартом: {scenario.LoginWaitSeconds} сек.");
        await session.Page.WaitForTimeoutAsync(scenario.LoginWaitSeconds * 1000);
    }

    var runner = new ScenarioRunner(session.Page);
    await runner.RunAsync(scenario);

    Console.WriteLine("Сценарий выполнен.");
}
catch (Exception ex) when (ex is PlaywrightException or InvalidOperationException)
{
    Console.WriteLine("Ошибка запуска/выполнения сценария.");
    Console.WriteLine(ex.Message);
    Console.WriteLine("Подсказка: в режиме cdp закрой старые браузеры или смени cdpPort.");
    return;
}
finally
{
    if (session is not null)
    {
        await session.DisposeAsync();
    }
}

internal static class BrowserLauncher
{
    public static async Task<BrowserSession> LaunchAsync(IPlaywright playwright, ScenarioDefinition scenario, string executablePath, string userDataDir)
    {
        return scenario.Browser.LaunchMode switch
        {
            BrowserLaunchMode.Cdp => await LaunchCdpAsync(playwright, scenario, executablePath, userDataDir),
            BrowserLaunchMode.Persistent => await LaunchPersistentWithFallbackAsync(playwright, scenario, executablePath, userDataDir),
            _ => throw new InvalidOperationException($"Неизвестный launchMode: {scenario.Browser.LaunchMode}"),
        };
    }

    private static async Task<BrowserSession> LaunchPersistentWithFallbackAsync(IPlaywright playwright, ScenarioDefinition scenario, string executablePath, string userDataDir)
    {
        try
        {
            Console.WriteLine("Запуск Playwright persistent...");
            var context = await LaunchPersistentContextAsync(playwright, scenario, executablePath, userDataDir);
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
            return new BrowserSession(page, async () => await context.CloseAsync());
        }
        catch (PlaywrightException ex) when (scenario.Browser.UseProfileClone)
        {
            Console.WriteLine("Persistent не удался. Пробую копию профиля...");
            Console.WriteLine($"Причина: {ex.Message}");

            var cloneDir = ProfileCloneStore.CreateProfileClone(userDataDir, scenario.Browser.ProfileDirectoryName);
            var context = await LaunchPersistentContextAsync(playwright, scenario, executablePath, cloneDir);
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            return new BrowserSession(page, async () =>
            {
                await context.CloseAsync();
                ProfileCloneStore.TryDeleteDirectory(cloneDir);
            });
        }
    }

    private static async Task<IBrowserContext> LaunchPersistentContextAsync(IPlaywright playwright, ScenarioDefinition scenario, string executablePath, string userDataDir)
    {
        var args = BuildCommonArgs(scenario.Browser.ProfileDirectoryName, scenario.Browser.AdditionalArgs);

        return await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            SlowMo = scenario.SlowMoMs,
            ExecutablePath = executablePath,
            Args = args,
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });
    }

    private static async Task<BrowserSession> LaunchCdpAsync(IPlaywright playwright, ScenarioDefinition scenario, string executablePath, string userDataDir)
    {
        var mustStartDedicatedBrowser = scenario.Browser.ForceNewWindow;
        var sourceUserDataDir = userDataDir;
        var runtimeUserDataDir = userDataDir;

        if (scenario.Browser.CloneProfileForCdp)
        {
            runtimeUserDataDir = ProfileCloneStore.CreateProfileClone(sourceUserDataDir, scenario.Browser.ProfileDirectoryName);
            Console.WriteLine($"Для CDP создана отдельная копия профиля: {runtimeUserDataDir}");
        }

        var selectedPort = await PickPortAsync(scenario.Browser.CdpPort, mustStartDedicatedBrowser);
        var endpoint = $"http://127.0.0.1:{selectedPort}";

        Process? process = null;
        if (mustStartDedicatedBrowser)
        {
            Console.WriteLine($"Запускаю отдельное окно браузера (CDP): {endpoint}");
            process = StartBrowserProcess(executablePath, runtimeUserDataDir, scenario, selectedPort, openNewWindow: true);
            await CdpProbe.WaitUntilReadyAsync(selectedPort, 15000);
        }
        else if (!await CdpProbe.IsEndpointReadyAsync(selectedPort))
        {
            Console.WriteLine($"CDP endpoint {endpoint} не найден. Запускаю Яндекс.Браузер вручную...");
            process = StartBrowserProcess(executablePath, runtimeUserDataDir, scenario, selectedPort, openNewWindow: false);
            await CdpProbe.WaitUntilReadyAsync(selectedPort, 15000);
        }
        else
        {
            Console.WriteLine($"Использую уже доступный CDP endpoint: {endpoint}");
        }

        try
        {
            var browser = await playwright.Chromium.ConnectOverCDPAsync(endpoint);
            var context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            return new BrowserSession(page, async () =>
            {
                await browser.CloseAsync();

                if (process is not null && !scenario.Browser.KeepBrowserOpen)
                {
                    TryKill(process);
                }

                if (scenario.Browser.CloneProfileForCdp)
                {
                    ProfileCloneStore.TryDeleteDirectory(runtimeUserDataDir);
                }
            });
        }
        catch
        {
            if (process is not null && !scenario.Browser.KeepBrowserOpen)
            {
                TryKill(process);
            }

            if (scenario.Browser.CloneProfileForCdp)
            {
                ProfileCloneStore.TryDeleteDirectory(runtimeUserDataDir);
            }

            throw;
        }
    }

    private static Process StartBrowserProcess(string executablePath, string userDataDir, ScenarioDefinition scenario, int cdpPort, bool openNewWindow)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add($"--remote-debugging-port={cdpPort}");
        startInfo.ArgumentList.Add($"--user-data-dir={userDataDir}");
        startInfo.ArgumentList.Add($"--profile-directory={scenario.Browser.ProfileDirectoryName}");
        startInfo.ArgumentList.Add("--no-first-run");
        startInfo.ArgumentList.Add("--no-default-browser-check");

        if (openNewWindow)
        {
            startInfo.ArgumentList.Add("--new-window");
        }

        foreach (var arg in scenario.Browser.AdditionalArgs)
        {
            if (!string.IsNullOrWhiteSpace(arg))
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        startInfo.ArgumentList.Add("about:blank");

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Не удалось запустить процесс браузера.");
        }

        Console.WriteLine($"Браузер запущен вручную. PID: {process.Id}");
        return process;
    }

    private static async Task<int> PickPortAsync(int preferredPort, bool dedicatedWindow)
    {
        if (!dedicatedWindow)
        {
            return preferredPort;
        }

        if (!await CdpProbe.IsEndpointReadyAsync(preferredPort))
        {
            return preferredPort;
        }

        for (var offset = 1; offset <= 20; offset++)
        {
            var candidatePort = preferredPort + offset;
            if (!await CdpProbe.IsEndpointReadyAsync(candidatePort))
            {
                Console.WriteLine($"Порт {preferredPort} уже занят. Выбран свободный порт: {candidatePort}");
                return candidatePort;
            }
        }

        throw new InvalidOperationException("Не удалось подобрать свободный CDP порт для отдельного окна браузера.");
    }

    private static List<string> BuildCommonArgs(string profileDirectoryName, List<string> additionalArgs)
    {
        var args = new List<string>
        {
            "--no-first-run",
            "--no-default-browser-check",
            $"--profile-directory={profileDirectoryName}",
        };

        args.AddRange(additionalArgs.Where(x => !string.IsNullOrWhiteSpace(x)));
        return args;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignore
        }
    }
}

internal static class CdpProbe
{
    public static async Task<bool> IsEndpointReadyAsync(int port)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };

        try
        {
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/json/version");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task WaitUntilReadyAsync(int port, int timeoutMs)
    {
        var started = DateTime.UtcNow;

        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (await IsEndpointReadyAsync(port))
            {
                return;
            }

            await Task.Delay(300);
        }

        throw new InvalidOperationException($"CDP endpoint не поднялся за {timeoutMs} мс на порту {port}.");
    }
}

internal sealed class BrowserSession(IPage page, Func<Task> dispose)
{
    private readonly Func<Task> _dispose = dispose;
    public IPage Page { get; } = page;

    public Task DisposeAsync() => _dispose();
}

internal static class ProfileCloneStore
{
    public static string CreateProfileClone(string sourceUserDataDir, string profileDirectoryName)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "TTWWorker", $"profile-clone-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(tempRoot);

        var sourceProfileDir = Path.Combine(sourceUserDataDir, profileDirectoryName);
        if (!Directory.Exists(sourceProfileDir))
        {
            throw new DirectoryNotFoundException($"Папка профиля не найдена: {sourceProfileDir}");
        }

        var targetProfileDir = Path.Combine(tempRoot, profileDirectoryName);
        CopyDirectory(sourceProfileDir, targetProfileDir);

        var localStatePath = Path.Combine(sourceUserDataDir, "Local State");
        if (File.Exists(localStatePath))
        {
            File.Copy(localStatePath, Path.Combine(tempRoot, "Local State"), overwrite: true);
        }

        return tempRoot;
    }

    public static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            Console.WriteLine($"Не удалось удалить временную папку профиля: {path}");
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destinationFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var directoryName = Path.GetFileName(directory);
            var destinationSubDir = Path.Combine(destinationDir, directoryName);
            CopyDirectory(directory, destinationSubDir);
        }
    }
}

internal static class BrowserPathResolver
{
    public static bool TryResolveExecutablePath(string? configuredPath, out string executablePath, out List<string> checkedPaths)
    {
        checkedPaths = [];

        var candidates = new List<string>();
        AddIfNotEmpty(candidates, configuredPath);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddIfNotEmpty(candidates, Path.Combine(localAppData, "Yandex", "YandexBrowser", "Application", "browser.exe"));
        AddIfNotEmpty(candidates, Path.Combine(localAppData, "Yandex", "YandexBrowser", "Application", "yandex.exe"));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        AddIfNotEmpty(candidates, Path.Combine(programFiles, "Yandex", "YandexBrowser", "Application", "browser.exe"));
        AddIfNotEmpty(candidates, Path.Combine(programFiles, "Yandex", "YandexBrowser", "Application", "yandex.exe"));

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        AddIfNotEmpty(candidates, Path.Combine(programFilesX86, "Yandex", "YandexBrowser", "Application", "browser.exe"));
        AddIfNotEmpty(candidates, Path.Combine(programFilesX86, "Yandex", "YandexBrowser", "Application", "yandex.exe"));

        foreach (var rawCandidate in candidates)
        {
            var candidate = ExpandPath(rawCandidate);
            checkedPaths.Add(candidate);
            if (File.Exists(candidate))
            {
                executablePath = candidate;
                return true;
            }
        }

        executablePath = string.Empty;
        return false;
    }

    public static bool TryResolveUserDataDir(string? configuredPath, out string userDataDir, out List<string> checkedPaths)
    {
        checkedPaths = [];

        var candidates = new List<string>();
        AddIfNotEmpty(candidates, configuredPath);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddIfNotEmpty(candidates, Path.Combine(localAppData, "Yandex", "YandexBrowser", "User Data"));

        foreach (var rawCandidate in candidates)
        {
            var candidate = ExpandPath(rawCandidate);
            checkedPaths.Add(candidate);
            if (Directory.Exists(candidate))
            {
                userDataDir = candidate;
                return true;
            }
        }

        userDataDir = string.Empty;
        return false;
    }

    private static string ExpandPath(string value)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value);
        return Path.GetFullPath(expanded);
    }

    private static void AddIfNotEmpty(List<string> list, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            list.Add(value.Trim());
        }
    }
}

internal sealed class ScenarioRunner(IPage page)
{
    private readonly IPage _page = page;

    public async Task RunAsync(ScenarioDefinition scenario)
    {
        for (var index = 0; index < scenario.Steps.Count; index++)
        {
            var step = scenario.Steps[index];
            Console.WriteLine($"Шаг {index + 1}/{scenario.Steps.Count}: {step.Action}");
            await RunStepAsync(step);
        }
    }

    private async Task RunStepAsync(StepDefinition step)
    {
        for (var i = 0; i < Math.Max(1, step.Repeat); i++)
        {
            switch (step.Action)
            {
                case StepAction.Wait:
                    await _page.WaitForTimeoutAsync(step.DurationMs ?? 500);
                    break;

                case StepAction.KeyPress:
                    EnsureNotNull(step.Key, step.Action, nameof(step.Key));
                    await _page.Keyboard.PressAsync(step.Key!);
                    await DelayAfterStep(step);
                    break;

                case StepAction.Click:
                    EnsureNotNull(step.Selector, step.Action, nameof(step.Selector));
                    await _page.ClickAsync(step.Selector!);
                    await DelayAfterStep(step);
                    break;

                case StepAction.Navigate:
                    EnsureNotNull(step.Url, step.Action, nameof(step.Url));
                    await _page.GotoAsync(step.Url!);
                    await DelayAfterStep(step);
                    break;

                default:
                    throw new InvalidOperationException($"Неизвестное действие: {step.Action}");
            }
        }
    }

    private static void EnsureNotNull(string? value, StepAction action, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Поле {field} обязательно для действия '{action}'.");
        }
    }

    private async Task DelayAfterStep(StepDefinition step)
    {
        if (step.AfterDelayMs is > 0)
        {
            await _page.WaitForTimeoutAsync(step.AfterDelayMs.Value);
        }
    }
}

internal sealed class ScenarioDefinition
{
    public string Name { get; init; } = "Автоматизация TikTok";
    public string StartUrl { get; init; } = "https://www.tiktok.com/foryou";
    public int LoginWaitSeconds { get; init; } = 10;
    public float? SlowMoMs { get; init; }
    public BrowserDefinition Browser { get; init; } = new();
    public List<StepDefinition> Steps { get; init; } = [];
}

internal sealed class BrowserDefinition
{
    public string ExecutablePath { get; init; } = "%LOCALAPPDATA%/Yandex/YandexBrowser/Application/browser.exe";
    public string UserDataDir { get; init; } = "%LOCALAPPDATA%/Yandex/YandexBrowser/User Data";
    public string ProfileDirectoryName { get; init; } = "Default";
    public BrowserLaunchMode LaunchMode { get; init; } = BrowserLaunchMode.Cdp;
    public int CdpPort { get; init; } = 9222;
    public bool KeepBrowserOpen { get; init; }
    public bool ForceNewWindow { get; init; } = true;
    public bool CloneProfileForCdp { get; init; } = true;
    public bool UseProfileClone { get; init; } = true;
    public List<string> AdditionalArgs { get; init; } = [];
}

internal enum BrowserLaunchMode
{
    Cdp,
    Persistent,
}

internal sealed class StepDefinition
{
    public StepAction Action { get; init; }
    public string? Key { get; init; }
    public string? Selector { get; init; }
    public string? Url { get; init; }
    public int? DurationMs { get; init; }
    public int? AfterDelayMs { get; init; }
    public int Repeat { get; init; } = 1;
}

internal enum StepAction
{
    Wait,
    KeyPress,
    Click,
    Navigate,
}
