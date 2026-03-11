using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

const string defaultScenarioPath = "scenario.json";
var scenarioPath = args.FirstOrDefault() ?? defaultScenarioPath;

if (!File.Exists(scenarioPath))
{
    Console.WriteLine($"Scenario file not found: {scenarioPath}");
    Console.WriteLine("Usage: dotnet run -- <path-to-scenario.json>");
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
    Console.WriteLine("Unable to parse scenario JSON.");
    return;
}

if (string.IsNullOrWhiteSpace(scenario.StartUrl))
{
    Console.WriteLine("StartUrl is required in scenario JSON.");
    return;
}

Console.WriteLine($"Loaded scenario: {scenario.Name}");
Console.WriteLine($"Steps: {scenario.Steps.Count}");

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
    SlowMo = scenario.SlowMoMs,
    Channel = scenario.BrowserChannel,
});

var context = await browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
});

var page = await context.NewPageAsync();
await page.GotoAsync(scenario.StartUrl);

if (scenario.LoginWaitSeconds > 0)
{
    Console.WriteLine($"Login wait: {scenario.LoginWaitSeconds}s (do manual auth if needed)");
    await page.WaitForTimeoutAsync(scenario.LoginWaitSeconds * 1000);
}

var runner = new ScenarioRunner(page);
await runner.RunAsync(scenario);

Console.WriteLine("Scenario completed.");

internal sealed class ScenarioRunner(IPage page)
{
    private readonly IPage _page = page;

    public async Task RunAsync(ScenarioDefinition scenario)
    {
        foreach (var step in scenario.Steps)
        {
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
                    throw new InvalidOperationException($"Unknown action: {step.Action}");
            }
        }
    }

    private static void EnsureNotNull(string? value, StepAction action, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{field} is required for action '{action}'.");
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
    public string Name { get; init; } = "TikTok automation";
    public string StartUrl { get; init; } = "https://www.tiktok.com/";
    public int LoginWaitSeconds { get; init; } = 20;
    public float? SlowMoMs { get; init; }
    public string? BrowserChannel { get; init; }
    public List<StepDefinition> Steps { get; init; } = [];
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
