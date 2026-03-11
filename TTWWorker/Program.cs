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
    Console.WriteLine("В сценарии обязательно поле StartUrl.");
    return;
}

if (string.IsNullOrWhiteSpace(scenario.Browser.ExecutablePath))
{
    Console.WriteLine("В сценарии обязательно поле browser.executablePath (путь к yandex.exe). ");
    return;
}

if (!File.Exists(scenario.Browser.ExecutablePath))
{
    Console.WriteLine($"Исполняемый файл браузера не найден: {scenario.Browser.ExecutablePath}");
    return;
}

if (string.IsNullOrWhiteSpace(scenario.Browser.UserDataDir))
{
    Console.WriteLine("В сценарии обязательно поле browser.userDataDir (путь к данным профиля браузера).");
    return;
}

if (!Directory.Exists(scenario.Browser.UserDataDir))
{
    Console.WriteLine($"Папка userDataDir не найдена: {scenario.Browser.UserDataDir}");
    return;
}

Console.WriteLine($"Сценарий загружен: {scenario.Name}");
Console.WriteLine($"Шагов в сценарии: {scenario.Steps.Count}");
Console.WriteLine($"Браузер: {scenario.Browser.ExecutablePath}");
Console.WriteLine($"Профиль: {scenario.Browser.ProfileDirectoryName}");

using var playwright = await Playwright.CreateAsync();

var launchArgs = new List<string>();
if (!string.IsNullOrWhiteSpace(scenario.Browser.ProfileDirectoryName))
{
    launchArgs.Add($"--profile-directory={scenario.Browser.ProfileDirectoryName}");
}

await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
    userDataDir: scenario.Browser.UserDataDir,
    new BrowserTypeLaunchPersistentContextOptions
    {
        Headless = false,
        SlowMo = scenario.SlowMoMs,
        ExecutablePath = scenario.Browser.ExecutablePath,
        Args = launchArgs,
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
    });

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
    public string ExecutablePath { get; init; } = @"C:\\Users\\%USERNAME%\\AppData\\Local\\Yandex\\YandexBrowser\\Application\\browser.exe";
    public string UserDataDir { get; init; } = @"C:\\Users\\%USERNAME%\\AppData\\Local\\Yandex\\YandexBrowser\\User Data";
    public string ProfileDirectoryName { get; init; } = "Default";
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
