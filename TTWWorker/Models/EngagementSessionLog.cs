namespace TTWWorker.Models;

public record EngagementSessionLog(
    DateTime StartedAt,
    int PlannedMinutes,
    int WatchedVideos,
    int ManualLikes,
    int ManualComments,
    string Notes);
