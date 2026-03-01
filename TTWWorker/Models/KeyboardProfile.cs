namespace TTWWorker.Models;

public record KeyboardProfile(
    Guid Id,
    string Name,
    int DownMinIntervalSeconds,
    int DownMaxIntervalSeconds,
    int AltTabIntervalSeconds,
    int DefaultMinutes,
    int ClickDelayMilliseconds,
    int ClickPeriodSeconds,
    int ClicksPerPeriod,
    IReadOnlyList<PixelClickPoint> AltTabClickPoints);
