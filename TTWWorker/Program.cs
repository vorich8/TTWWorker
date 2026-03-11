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

    Console.WriteLine("Укажи рабочий путь в browser.executablePath или установи Яндекс.Браузер.");
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

    Console.WriteLine("Укажи рабочий путь в browser.userDataDir.");
    return;
}

Console.WriteLine($"Сценарий загружен: {scenario.Name}");
Console.WriteLine($"Шагов в сценарии: {scenario.Steps.Count}");
Console.WriteLine($"Браузер: {executablePath}");
Console.WriteLine($"Папка профилей: {userDataDir}");
Console.WriteLine($"Профиль: {scenario.Browser.ProfileDirectoryName}");

using var playwright = await Playwright.CreateAsync();

string? tempUserDataDir = null;
IBrowserContext? context = null;

try
{
    context = await LaunchWithFallbackAsync(playwright, scenario, executablePath, userDataDir);
    var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

    Console.WriteLine($"Переход на: {scenario.StartUrl}");
    await page.GotoAsync(scenario.StartUrl);

    if (scenario.LoginWaitSeconds > 0)
    {
        Console.WriteLine($"Ожидание перед стартом: {scenario.LoginWaitSeconds} сек.");
        await page.WaitForTimeoutAsync(scenario.LoginWaitSeconds * 1000);
    }

    var runner = new ScenarioRunner(page);
    await runner.RunAsync(scenario);

    Console.WriteLine("Сценарий выполнен.");
}
catch (PlaywrightException ex)
{
    Console.WriteLine("Ошибка Playwright при запуске/выполнении сценария.");
    Console.WriteLine(ex.Message);
    Console.WriteLine("Подсказка: закрой все окна Яндекс.Браузера и попробуй снова.");
    Console.WriteLine("Если проблема повторяется, оставь browser.useProfileClone=true (или включи его) для запуска через копию профиля.");
    return;
}
finally
{
    if (context is not null)
    {
        await context.CloseAsync();
    }

    tempUserDataDir = ProfileCloneStore.LastTempUserDataDir;
    if (!string.IsNullOrWhiteSpace(tempUserDataDir) && Directory.Exists(tempUserDataDir))
    {
        TryDeleteDirectory(tempUserDataDir);
    }
}

async Task<IBrowserContext> LaunchWithFallbackAsync(IPlaywright playwrightInstance, ScenarioDefinition scenarioDefinition, string browserExePath, string baseUserDataDir)
{
    try
    {
        Console.WriteLine("Запуск браузера с основным профилем...");
        return await LaunchContextAsync(playwrightInstance, scenarioDefinition, browserExePath, baseUserDataDir);
    }
    catch (PlaywrightException ex) when (scenarioDefinition.Browser.UseProfileClone)
    {
        Console.WriteLine("Основной запуск не удался. Пробую запуск через временную копию профиля...");
        Console.WriteLine($"Причина: {ex.Message}");

        var cloneUserDataDir = ProfileCloneStore.CreateProfileClone(baseUserDataDir, scenarioDefinition.Browser.ProfileDirectoryName);
        Console.WriteLine($"Временная копия профиля создана: {cloneUserDataDir}");

        return await LaunchContextAsync(playwrightInstance, scenarioDefinition, browserExePath, cloneUserDataDir);
    }
}

async Task<IBrowserContext> LaunchContextAsync(IPlaywright playwrightInstance, ScenarioDefinition scenarioDefinition, string browserExePath, string resolvedUserDataDir)
{
    var launchArgs = new List<string>();
    if (!string.IsNullOrWhiteSpace(scenarioDefinition.Browser.ProfileDirectoryName))
    {
        launchArgs.Add($"--profile-directory={scenarioDefinition.Browser.ProfileDirectoryName}");
    }

    launchArgs.Add("--no-first-run");
    launchArgs.Add("--no-default-browser-check");

    return await playwrightInstance.Chromium.LaunchPersistentContextAsync(
        userDataDir: resolvedUserDataDir,
        new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            SlowMo = scenarioDefinition.SlowMoMs,
            ExecutablePath = browserExePath,
            Args = launchArgs,
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });
}

void TryDeleteDirectory(string path)
{
    try
    {
        Directory.Delete(path, recursive: true);
    }
    catch
    {
        Console.WriteLine($"Не удалось удалить временную папку профиля: {path}");
    }
}

internal static class ProfileCloneStore
{
    public static string? LastTempUserDataDir { get; private set; }

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

        LastTempUserDataDir = tempRoot;
        return tempRoot;
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
    public bool UseProfileClone { get; init; } = true;
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
