using AscendAPP.Core.Storage;
using System.Text.Json;
namespace AscendAPP.Core.Game;
public sealed record EventTimerStatus(RecurringEventDefinition Event,DateTimeOffset Start,DateTimeOffset End,bool InWindow)
{
    public DateTimeOffset CountdownAt=>InWindow?End:Start;
}
public static class EventTimetable
{
    public static EventTimerStatus Evaluate(RecurringEventDefinition e,DateTimeOffset now,int shiftMinutes=0)
    {
        var anchor=DateTimeOffset.UnixEpoch.AddMinutes(e.AnchorMinuteUtc+Math.Clamp(shiftMinutes,-180,180));
        long period=TimeSpan.FromMinutes(e.PeriodMinutes).Ticks;
        long elapsed=now.UtcTicks-anchor.UtcTicks;
        long cycle=Math.DivRem(elapsed,period,out long remainder);if(remainder<0)cycle--;
        var start=anchor.AddTicks(cycle*period);var end=start.AddMinutes(e.WindowMinutes);
        if(now>=end){start=start.AddTicks(period);end=start.AddMinutes(e.WindowMinutes);}
        return new(e,start,end,now>=start&&now<end);
    }
}
public sealed record FieldBossObservation(int ServerId,int MapId,int Npc,bool Alive,long TimeMs,DateTimeOffset ObservedAt);
public sealed record FieldBossTimerStatus(FieldBossDefinition Boss,FieldBossObservation? Observation,DateTimeOffset? Due)
{
    public bool IsDueAt(DateTimeOffset now)=>Due is {} due&&due<=now;
}
public sealed class FieldBossTracker(WorldTimerCatalog catalog)
{
    private readonly object _gate=new();private readonly Dictionary<(int Server,int Npc),FieldBossObservation> _states=new();
    public int? LastServer {get;private set;}
    public bool Update(FieldBossList list)
    {
        if(list.ServerId is not (>=1000 and <=9999)||list.MapId!=catalog.MapId||list.Slots.Count is <1 or >64||list.Slots.Select(x=>x.Number).Distinct().Count()!=list.Slots.Count||list.Slots.Any(x=>x.Number<1||x.Number>catalog.SlotCount))return false;
        lock(_gate)
        {
            bool updated=false;
            foreach(var slot in list.Slots)
            {
                var boss=catalog.Bosses.FirstOrDefault(x=>x.Slot==slot.Number);if(boss is null)continue;
                var observation=new FieldBossObservation(list.ServerId.Value,list.MapId,boss.Npc,slot.Alive,slot.TimeMs,list.ObservedAt);
                if(!Valid(observation)||_states.TryGetValue((observation.ServerId,boss.Npc),out var old)&&old.ObservedAt>observation.ObservedAt)continue;
                _states[(observation.ServerId,boss.Npc)]=observation;updated=true;LastServer=observation.ServerId;
            }
            while(_states.Count>128)_states.Remove(_states.MinBy(x=>x.Value.ObservedAt).Key);
            return updated;
        }
    }
    private bool Valid(FieldBossObservation o)=>o.ServerId is >=1000 and <=9999&&o.MapId==catalog.MapId&&catalog.Bosses.Any(x=>x.Npc==o.Npc)&&
        (o.TimeMs==0||o.TimeMs is >=1_600_000_000_000 and <=2_600_000_000_000)&&o.ObservedAt>=DateTimeOffset.UnixEpoch&&o.ObservedAt.Year<=2100;
    public IReadOnlyList<FieldBossTimerStatus> Snapshot(int? server)
    {
        lock(_gate)return catalog.Bosses.Select(b=>{
            var o=server is {} id?_states.GetValueOrDefault((id,b.Npc)):null;
            return new FieldBossTimerStatus(b,o,o is{Alive:false,TimeMs:>0}?DateTimeOffset.FromUnixTimeMilliseconds(o.TimeMs):null);
        }).ToArray();
    }
    public void Clear(){lock(_gate){_states.Clear();LastServer=null;}}
    public void Load(string path)
    {
        try
        {
            if(new FileInfo(path).Length>200000)return;
            var rows=JsonSerializer.Deserialize<FieldBossObservation[]>(File.ReadAllText(path))??[];
            lock(_gate)foreach(var o in rows.Where(x=>x is not null).TakeLast(128).Where(Valid).OrderBy(x=>x.ObservedAt)){_states[(o.ServerId,o.Npc)]=o;LastServer=o.ServerId;}
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){}
    }
    public void Save(string path)
    {
        FieldBossObservation[] rows;lock(_gate)rows=_states.Values.OrderBy(x=>x.ObservedAt).ToArray();
        AtomicFile.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
