namespace TTWWorker.Models;

public record KeyboardProfile(
    Guid Id,
    string Name,
    int DownMinIntervalSeconds,
    int DownMaxIntervalSeconds,
    int AltTabIntervalSeconds,
    int DefaultMinutes,
    int ClickDelayMilliseconds,
    ClickTimingMode ClickTiming,
    IReadOnlyList<PixelClickPoint> AltTabClickPoints);
