namespace TTWWorker.Models;

public record PublicationPlanItem(
    Guid Id,
    DateTime ScheduledAt,
    string Topic,
    string Format,
    string Status);
