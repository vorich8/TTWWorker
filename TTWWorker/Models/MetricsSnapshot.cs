namespace TTWWorker.Models;

public record MetricsSnapshot(
    DateTime CapturedAt,
    int Views,
    int Likes,
    int Comments,
    int Shares,
    double AvgWatchSeconds,
    double VideoLengthSeconds);
