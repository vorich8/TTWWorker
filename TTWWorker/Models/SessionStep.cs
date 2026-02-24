namespace TTWWorker.Models;

public record SessionStep(
    string Name,
    int DurationMinutes,
    string Action,
    string TargetUrl);
