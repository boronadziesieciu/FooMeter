using AscendAPP.Core.Protocol;
namespace AscendAPP.Core.Combat;

public sealed record HealingDiagnostics(long Raw,long Scored,long Excluded,long Unverified,int Pending);

/// <summary>Conservative HP-based estimate, not a protocol effective-heal field.
/// Waits 500 ms for the matching HP observation. Ambiguous overlaps, stale/missing
/// before/after HP and intervening damage are unscored, never credited at raw potency.</summary>
public sealed class HealingReconciler
{
    private sealed record Pending(long Sequence,CombatEvent Event,bool Admitted);
    private readonly object _gate=new();
    private readonly Dictionary<uint,List<TargetHealth>> _health=new();
    private readonly Dictionary<uint,List<DateTimeOffset>> _damage=new(),_ambiguous=new();
    private readonly List<Pending> _pending=new(),_recent=new();
    private long _sequence,_raw,_scored,_excluded,_unverified;
    public static readonly TimeSpan Window=TimeSpan.FromMilliseconds(500);
    public void Reset(){lock(_gate){_health.Clear();_damage.Clear();_ambiguous.Clear();_pending.Clear();_recent.Clear();_raw=_scored=_excluded=_unverified=0;}}
    public void Forget(uint id){lock(_gate){_health.Remove(id);_damage.Remove(id);_ambiguous.Remove(id);}}
    public void ClearPending(){lock(_gate){_pending.Clear();_recent.Clear();}}
    public HealingDiagnostics Diagnostics {get{lock(_gate)return new(_raw,_scored,_excluded,_unverified,_pending.Count);}}
    public void Observe(TargetHealth hp)
    {
        if(hp.Current is null or <0||hp.ObservedAt is null||hp.Maximum is <=0||hp.Current>hp.Maximum)return;
        lock(_gate)
        {
            if(!_health.TryGetValue(hp.Id,out var list)){if(_health.Count>=16000)_health.Clear();_health[hp.Id]=list=new();}
            if(list.Count>0&&hp.ObservedAt<=list[^1].ObservedAt)
            {
                if(hp.ObservedAt==list[^1].ObservedAt&&(hp.Current!=list[^1].Current||hp.Maximum!=list[^1].Maximum))
                {
                    if(!_ambiguous.TryGetValue(hp.Id,out var times)){if(_ambiguous.Count>=16000)_ambiguous.Clear();_ambiguous[hp.Id]=times=new();}
                    times.Add(hp.ObservedAt.Value);if(times.Count>128)times.RemoveAt(0);
                }
                return;
            }
            list.Add(hp);if(list.Count>128)list.RemoveAt(0);
        }
    }
    public void Damage(uint id,DateTimeOffset at)
    {
        lock(_gate)
        {
            if(!_damage.TryGetValue(id,out var list)){if(_damage.Count>=16000)_damage.Clear();_damage[id]=list=new();}
            list.Add(at);if(list.Count>256)list.RemoveAt(0);
        }
    }
    public void Add(CombatEvent heal,bool admitted)
    {
        if(heal.Kind!=CombatKind.Heal||heal.Amount<=0)return;
        lock(_gate)
        {
            // Overload fails closed: do not fall back to raw healing.
            if(_pending.Count>=8192){if(admitted){_raw+=heal.Amount;_unverified+=heal.Amount;}return;}
            var item=new Pending(++_sequence,heal,admitted);_pending.Add(item);_recent.Add(item);
            if(_recent.Count>16384)_recent.RemoveRange(0,8192);
        }
    }
    public IReadOnlyList<CombatEvent> Drain(DateTimeOffset now)
    {
        lock(_gate)
        {
            if(_pending.Count==0&&_recent.Count==0)return [];
            return DrainCore(now);
        }
    }
    private IReadOnlyList<CombatEvent> DrainCore(DateTimeOffset now)
    {
            var ready=_pending.Where(p=>now>=p.Event.Time+Window).OrderBy(p=>p.Event.Time).ToArray();
            var result=new List<CombatEvent>();
            foreach(var p in ready)
            {
                _pending.Remove(p);
                if(!p.Admitted)continue; // still participated in overlap detection
                var e=p.Event;var end=e.Time+Window;
                var samples=_health.GetValueOrDefault(e.TargetId);
                var before=samples?.LastOrDefault(h=>h.ObservedAt<e.Time&&e.Time-h.ObservedAt<=TimeSpan.FromSeconds(2));
                var after=samples?.LastOrDefault(h=>h.ObservedAt>=e.Time&&h.ObservedAt<=end);
                long? effective=null;string basis="Unverified: missing or stale HP";
                bool damaged=before is not null&&_damage.GetValueOrDefault(e.TargetId)?.Any(t=>t>=before.ObservedAt&&t<=end)==true;
                bool overlap=_recent.Any(other=>other.Sequence!=p.Sequence&&other.Event.TargetId==e.TargetId&&
                    (other.Event.Time-e.Time).Duration()<=Window);
                bool uncertainOrder=before is not null&&_ambiguous.GetValueOrDefault(e.TargetId)?.Any(t=>t>=before.ObservedAt&&t<=end)==true;
                if(uncertainOrder)basis="Unverified: HP ordering ambiguous";
                else if(damaged)basis="Unverified: intervening damage";
                else if(before is not null)
                {
                    bool sameMaximum=after is null||before.Maximum==after.Maximum;
                    if(sameMaximum&&before.Maximum is >0&&before.Current==before.Maximum&&
                        (after is null||after.Current==before.Current))
                    {effective=0;basis="Full health (HP estimate)";}
                    else if(overlap)basis="Unverified: overlapping heals";
                    else if(after is not null&&sameMaximum)
                    {
                        long gain=after.Current!.Value-before.Current!.Value;
                        if(gain>=0&&gain<=e.Amount){effective=gain;basis="Observed HP gain (estimate)";}
                        else basis="Unverified: HP change does not match";
                    }
                }
                _raw+=e.Amount;
                if(effective is {} n){_scored+=n;_excluded+=e.Amount-n;}else _unverified+=e.Amount;
                result.Add(e with{EffectiveHealing=effective,HealingAssessment=basis});
            }
            _recent.RemoveAll(p=>p.Event.Time<now-TimeSpan.FromSeconds(5)&&!_pending.Contains(p));
            return result;
    }
}
