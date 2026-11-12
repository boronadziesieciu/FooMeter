using System.Text.Json;
namespace AscendAPP.Core.Game;
public sealed record FieldBossDefinition(int Npc,int Slot,string Name);
public sealed record RecurringEventDefinition(string Id,string Name,int PeriodMinutes,int AnchorMinuteUtc,int WindowMinutes,string Icon,bool Possible=false);
public sealed class WorldTimerCatalog
{
    public int MapId {get;private set;}public int SlotCount {get;private set;}public string MapName {get;private set;}="";
    public IReadOnlyList<FieldBossDefinition> Bosses {get;private set;}=[];
    public IReadOnlyList<RecurringEventDefinition> Events {get;private set;}=[];
    public static WorldTimerCatalog Read(string bossPath,string eventPath)
    {
        var c=new WorldTimerCatalog();
        try
        {
            if(new FileInfo(bossPath).Length>100000)throw new JsonException();
            using var doc=JsonDocument.Parse(File.ReadAllText(bossPath));var r=doc.RootElement;
            int map=r.GetProperty("mapId").GetInt32(),count=r.GetProperty("slotCount").GetInt32();
            var names=r.GetProperty("bosses").EnumerateArray().Select(x=>new FieldBossDefinition(x.GetProperty("npc").GetInt32(),x.GetProperty("slot").GetInt32(),x.GetProperty("name").GetString()??"")).ToArray();
            if(map is <1000 or >99999||count is <1 or >64||names.Length is <1 or >64||names.Any(x=>x.Npc<=0||x.Slot<1||x.Slot>count||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>100)||names.Select(x=>x.Npc).Distinct().Count()!=names.Length||names.Select(x=>x.Slot).Distinct().Count()!=names.Length)throw new JsonException();
            c.MapId=map;c.SlotCount=count;c.MapName=r.GetProperty("mapName").GetString()??"";c.Bosses=names;
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){}
        try
        {
            if(new FileInfo(eventPath).Length>100000)throw new JsonException();
            using var doc=JsonDocument.Parse(File.ReadAllText(eventPath));var r=doc.RootElement;
            if(r.GetProperty("timeZone").GetString()!="UTC")throw new JsonException();
            var events=r.GetProperty("events").EnumerateArray().Select(x=>new RecurringEventDefinition(x.GetProperty("id").GetString()??"",x.GetProperty("name").GetString()??"",x.GetProperty("periodMinutes").GetInt32(),x.GetProperty("anchorMinuteUtc").GetInt32(),x.GetProperty("windowMinutes").GetInt32(),x.GetProperty("icon").GetString()??"Timer",x.TryGetProperty("possible",out var p)&&p.GetBoolean())).ToArray();
            if(events.Length>20||events.Any(x=>string.IsNullOrWhiteSpace(x.Id)||x.Id.Length>60||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>100||x.PeriodMinutes is <1 or >1440||x.AnchorMinuteUtc is <0 or >=1440||x.WindowMinutes<1||x.WindowMinutes>=x.PeriodMinutes)||events.Select(x=>x.Id).Distinct().Count()!=events.Length)throw new JsonException();
            c.Events=events;
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){}
        return c;
    }
}
