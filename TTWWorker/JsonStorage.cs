using System.Text.Json;
using TTWWorker.Models;

namespace TTWWorker;

public class JsonStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _baseDir;

    public JsonStorage()
    {
        _baseDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_baseDir);
    }

    public List<PublicationPlanItem> LoadPlan()
        => Load<List<PublicationPlanItem>>("plan.json") ?? [];

    public void SavePlan(List<PublicationPlanItem> items)
        => Save("plan.json", items);

    public List<MetricsSnapshot> LoadMetrics()
        => Load<List<MetricsSnapshot>>("metrics.json") ?? [];

    public void SaveMetrics(List<MetricsSnapshot> metrics)
        => Save("metrics.json", metrics);

    public string SaveReport(string content)
    {
        var fileName = $"weekly-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
        var fullPath = Path.Combine(_baseDir, fileName);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private T? Load<T>(string file)
    {
        var path = Path.Combine(_baseDir, file);
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private void Save<T>(string file, T data)
    {
        var path = Path.Combine(_baseDir, file);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }
}
