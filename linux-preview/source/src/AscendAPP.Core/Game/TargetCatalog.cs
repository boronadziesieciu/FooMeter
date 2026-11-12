using System.Text.Json;
namespace AscendAPP.Core.Game;
/// <summary>Explicit NPC identities, independent from berserk timing and HP. No numeric-range or runtime name guessing.</summary>
public sealed class TargetCatalog
{
    private readonly IReadOnlyDictionary<int,string> _bosses,_dummies;
    public TargetCatalog(IReadOnlyDictionary<int,string> bosses,IReadOnlyDictionary<int,string> dummies){_bosses=bosses;_dummies=dummies;}
    public bool IsTrainingDummy(int? npc)=>npc is {} n&&_dummies.ContainsKey(n);
    public bool IsBoss(int? npc)=>npc is {} n&&!_dummies.ContainsKey(n)&&_bosses.ContainsKey(n);
    public string? NameOf(int? npc)=>npc is {} n?(_dummies.GetValueOrDefault(n)??_bosses.GetValueOrDefault(n)):null;
    public static TargetCatalog Read(string path)
    {
        var bosses=new Dictionary<int,string>();var dummies=new Dictionary<int,string>();
        try{
            if(new FileInfo(path).Length>2_000_000)return new(bosses,dummies);
            using var doc=JsonDocument.Parse(File.ReadAllText(path));
            ReadMap(doc.RootElement.GetProperty("bosses"),bosses);ReadMap(doc.RootElement.GetProperty("dummies"),dummies);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException){bosses.Clear();dummies.Clear();}
        return new(bosses,dummies);
    }
    private static void ReadMap(JsonElement map,Dictionary<int,string> result)
    {
        foreach(var e in map.EnumerateObject())if(int.TryParse(e.Name,out int id)&&id>0&&e.Value.ValueKind==JsonValueKind.String&&e.Value.GetString() is {} name&&!string.IsNullOrWhiteSpace(name)&&name.Length<=100)result[id]=name.Trim();
    }
}
