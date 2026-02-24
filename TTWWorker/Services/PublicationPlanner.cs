using TTWWorker.Models;

namespace TTWWorker.Services;

public class PublicationPlanner(JsonStorage storage)
{
    public PublicationPlanItem AddItem(DateTime at, string topic, string format)
    {
        var items = storage.LoadPlan();
        var item = new PublicationPlanItem(Guid.NewGuid(), at, topic, format, "planned");
        items.Add(item);
        storage.SavePlan(items.OrderBy(x => x.ScheduledAt).ToList());
        return item;
    }

    public IReadOnlyList<PublicationPlanItem> GetUpcoming()
    {
        var now = DateTime.Now;
        return storage.LoadPlan().Where(x => x.ScheduledAt >= now).OrderBy(x => x.ScheduledAt).ToList();
    }
}
