using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
namespace AscendAPP.Core.Combat;

public sealed record PartyMember(string Name, int ServerId, string? ClassName=null, int? Level=null, int? HomeServerId=null,int? GearScore=null,int? CombatPower=null,int? ScoreServerId=null);
public sealed record PartyRoster(IReadOnlyList<PartyMember> Members, DateTimeOffset ObservedAt);
public sealed record BossEngagement(uint Id, bool InCombat, bool ExactStart, DateTimeOffset At);
public sealed record BossForecast(uint? TargetId, double DamagePerSecond, double? KillSeconds,
    double? BerserkSeconds, double? MarginSeconds, string Scope, string Status, string BerserkBasis, bool IsTrainingDummy=false);

/// <summary>Per-target rates, metadata and pull clocks. Never projects health loss or missing damage.</summary>
public sealed class BossForecastTracker(BossTimingCatalog catalog)
{
    private readonly record struct Sample(CombatEvent Event,bool ScopeAdmitted);
    private sealed class Target
    {
        public int? Npc;
        public string Name = "";
        public long? Hp, Maximum;
        public DateTimeOffset? HpAt, FirstHit, LastHit, Start;
        public bool ExactStart, Ended, IsBoss, IsDummy;
        public readonly List<Sample> Hits = new();
        public readonly Dictionary<(uint Actor,bool Self,bool Admitted),(double Amount,int Count)> Groups=new();
        public void ClearHits(){Hits.Clear();Groups.Clear();}
        public void Total(Sample sample)
        {
            var key=(sample.Event.ActorId,sample.Event.IsSelf,sample.ScopeAdmitted);
            var prior=Groups.GetValueOrDefault(key);Groups[key]=(prior.Amount+sample.Event.Amount,prior.Count+1);
        }
    }
    private readonly object _gate = new();
    private readonly Dictionary<uint,Target> _targets = new();
    private readonly Dictionary<uint,PlayerIdentity> _players = new();
    private uint? _active;
    private PartyRoster? _party;
    private Target Get(uint id)
    {
        if (!_targets.TryGetValue(id, out var t))
        {
            if (_targets.Count >= 256) { _targets.Clear(); _active = null; }
            _targets[id] = t = new();
        }
        return t;
    }
    public void Reset() { lock (_gate) { _targets.Clear(); _active = null; _players.Clear(); _party = null; } }
    public void ResetEncounter() { lock (_gate) { _targets.Clear(); _active = null; } }
    public void ClearDamage() { lock (_gate) { foreach (var t in _targets.Values) { t.ClearHits(); t.FirstHit = t.LastHit = null; } } }
    public void Identity(PlayerIdentity p) { lock (_gate) { if (_players.Count >= 16000) _players.Clear(); _players[p.Id] = p; } }
    public void Forget(uint id){lock(_gate)_players.Remove(id);}
    public void Party(PartyRoster roster) { lock (_gate) _party = roster; }
    public void Engage(BossEngagement e)
    {
        lock (_gate)
        {
            var t = Get(e.Id);
            if (!e.InCombat) { t.Ended = true; t.Start = null; t.ClearHits(); t.FirstHit = t.LastHit = null; return; }
            if (t.Ended) { t.ClearHits(); t.FirstHit = t.LastHit = null; }
            t.Ended = false;
            if (t.Start is null || e.ExactStart && !t.ExactStart) { t.Start = e.At; t.ExactStart = e.ExactStart; }
        }
    }
    public void Health(uint id, long? hp, long? maximum, DateTimeOffset at)
    {
        lock (_gate)
        {
            var t = Get(id);
            if (hp < 0 || maximum <= 0 || hp > maximum || t.HpAt.HasValue && at < t.HpAt.Value) return;
            // A reset to full health starts a fresh pull. Small heals below full do not restart a timer.
            if (!t.IsDummy && hp is > 0 && maximum is > 0 && hp == maximum && t.Hp < hp && (t.Hits.Count > 0 || t.Start.HasValue || t.Ended))
            { t.ClearHits(); t.FirstHit = t.LastHit = t.Start = null; t.Ended = false; }
            t.Hp = hp ?? t.Hp; t.Maximum = maximum ?? t.Maximum;
            if (hp.HasValue) t.HpAt = at;
            if (hp == 0) t.Ended = true;
        }
    }
    public void Add(CombatEvent e,bool scopeAdmitted=false)
    {
        if (e.Kind != CombatKind.Damage || e.Amount <= 0) return;
        lock (_gate)
        {
            var t = Get(e.TargetId);
            t.Npc = e.TargetNpcCode ?? t.Npc; t.Name = e.TargetName;
            t.IsDummy=e.TargetIsTrainingDummy; t.IsBoss=!t.IsDummy&&(e.TargetIsBoss||catalog.Find(t.Npc).HasValue);
            // Once a known boss is active, damage to an add does not change the projection target.
            if (_active is null || !_targets.TryGetValue(_active.Value, out var current) ||
                current.Ended || t.IsBoss || !current.IsBoss)
                _active = e.TargetId;
            if (t.LastHit is { } prior && e.Time < prior) return;
            if (e.TargetCurrentHp.HasValue) Health(e.TargetId, e.TargetCurrentHp, e.TargetMaxHp, e.TargetHpObservedAt ?? e.Time);
            if (t.Ended && t.Hp == 0) return;
            t.Ended = false; t.FirstHit ??= e.Time; t.LastHit = e.Time;
            if (t.Hits.Count >= 100000)
            {
                t.Hits.RemoveRange(0,50000);t.FirstHit=t.Hits[0].Event.Time;
                t.Groups.Clear();foreach(var sample in t.Hits)t.Total(sample);
            }
            var added=new Sample(e,scopeAdmitted);t.Hits.Add(added);t.Total(added);
        }
    }
    public BossForecast Snapshot(DateTimeOffset now,string? captureScope=null,DateTimeOffset? rateEnd=null)
    {
        lock (_gate)
        {
            if (_active is not { } id || !_targets.TryGetValue(id,out var t))
                return new(null,0,null,null,null,captureScope=="Self only"?"Your boss / dummy DPS":captureScope is not null?"Your + party boss / dummy DPS":"Tracked boss DPS","Waiting for eligible damage","Unknown");
            bool rosterKnown = _party is {Members.Count:>0} party && now >= party.ObservedAt;
            double sum=0;int hitCount=0;bool observedParty=false;
            void Include(uint actor,bool explicitSelf,bool admitted,double amount,int count)
            {
                bool own=explicitSelf||_players.TryGetValue(actor,out var identity)&&identity.IsSelf;
                bool member=false;
                if(!own&&rosterKnown&&_party is {} roster&&_players.TryGetValue(actor,out var p))
                    member=PartyMembership.Find(p,roster,_players.Values) is not null;
                bool include=captureScope=="Self only"?own:captureScope is not null?admitted||own||member:!rosterKnown||own||member;
                if(include){sum+=amount;hitCount+=count;if(!own)observedParty=true;}
            }
            // Normal refreshes inspect a handful of source groups, independent of fight length.
            // Preserve exact time-cutoff semantics for explicit historical queries.
            if(t.LastHit is null||now>=t.LastHit)
                foreach(var group in t.Groups)Include(group.Key.Actor,group.Key.Self,group.Key.Admitted,group.Value.Amount,group.Value.Count);
            else foreach(var sample in t.Hits)if(sample.Event.Time<=now)Include(sample.Event.ActorId,sample.Event.IsSelf,sample.ScopeAdmitted,sample.Event.Amount,1);
            string targetKind=t.IsDummy?"dummy":t.IsBoss?"boss":"target";
            string scope=captureScope=="Self only"?$"Your {targetKind} DPS":
                captureScope is not null?(rosterKnown?$"Your + party {targetKind} DPS":observedParty?$"Your + observed party {targetKind} DPS":$"Your {targetKind} DPS · party not identified"):
                rosterKnown?$"Observed party {targetKind} DPS":$"Tracked {targetKind} DPS · party unknown";
            var rateAt=rateEnd is {} end&&end<now?end:now;
            double seconds = t.FirstHit is { } first ? (rateAt - first).TotalSeconds : 0;
            double rate = seconds >= 3 ? sum/seconds : 0;
            string status = t.Ended ? (t.Hp == 0 ? "Target defeated" : "Combat ended") :
                t.LastHit is null || now - t.LastHit > TimeSpan.FromSeconds(12) ? "No recent damage" :
                seconds < 3 || hitCount < 2 ? "Measuring damage" :
                !t.Hp.HasValue ? "Waiting for target HP" :
                t.HpAt is null || now - t.HpAt > TimeSpan.FromSeconds(8) ? "Target HP is stale" :
                rate <= 0 ? "No matching group damage" : "Estimate at current measured rate";
            bool canEstimate = !t.Ended && hitCount >= 2 && seconds >= 3 && rate > 0 && t.Hp is > 0 &&
                t.HpAt.HasValue && now - t.HpAt.Value <= TimeSpan.FromSeconds(8) && t.LastHit.HasValue && now - t.LastHit.Value <= TimeSpan.FromSeconds(12);
            double? kill = t.IsDummy?null:canEstimate ? t.Hp!.Value/rate : t.Hp==0 ? 0 : null;
            if(t.IsDummy)status=t.Ended?"Dummy parse ended":"Training dummy · measured damage";
            int? rage = t.IsDummy?null:catalog.Find(t.Npc);
            double? remaining = !t.Ended && rage.HasValue && t.Start.HasValue && now>=t.Start
                ? Math.Max(0,rage.Value-(now-t.Start.Value).TotalSeconds) : null;
            string basis = t.IsDummy?"Training dummy; no boss countdown":!rage.HasValue ? "No catalogue entry for this NPC" :
                t.Start is null ? "Pull start not observed; no countdown assumed" :
                t.ExactStart ? "Catalogue duration from an observed start message" :
                "Approximate: first observed in-combat notice; may have joined mid-pull";
            double? margin = kill.HasValue && remaining.HasValue ? remaining-kill : null;
            return new(id,rate,kill,remaining,margin,scope,status,basis,t.IsDummy);
        }
    }
}
