using System.Text.Json;
namespace AscendAPP.Core.Game;
public sealed class BossTimingCatalog
{
    private readonly IReadOnlyDictionary<int,int> _seconds;
    public BossTimingCatalog(IReadOnlyDictionary<int,int> seconds) => _seconds = seconds;
    public int? Find(int? npc) => npc is { } id && _seconds.TryGetValue(id, out int seconds) && seconds is > 0 and <= 86400 ? seconds : null;
    public static BossTimingCatalog Read(string path)
    {
        var result = new Dictionary<int,int>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var item in doc.RootElement.GetProperty("seconds").EnumerateObject())
                if (int.TryParse(item.Name, out int id) && item.Value.TryGetInt32(out int seconds) && seconds is > 0 and <= 86400) result[id] = seconds;
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return new(result); // unknown boss => no timer; never assume 300 seconds
    }
}
