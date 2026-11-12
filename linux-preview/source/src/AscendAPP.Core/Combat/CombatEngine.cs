namespace AscendAPP.Core.Combat;

/// <summary>
/// Deterministic, thread-safe combat accumulator. Incoming CombatEvent data are prevalidated,
/// and metadata (class, boss HP/name) is only used when explicitly supplied.
/// </summary>
public sealed class CombatEngine
{
    private sealed class SkillState
    {
        public long Damage, Healing, MaxHit, MaxHeal, RawHealing, ExcludedHealing, UnverifiedHealing, LegacyHealing;
        public int HealingHits, HealingCrits;
        public int Hits, Crits;
        public string? Name;
    }
    private sealed class PlayerState
    {
        public required uint Id;
        public required string Name;
        public string? ClassName;
        public bool IsSelf;
        public long Damage, Healing, RawHealing, ExcludedHealing, UnverifiedHealing, LegacyHealing;
        public int Hits, Crits, HealingHits;
        public readonly Dictionary<int, SkillState> Skills = new();
        public readonly Dictionary<int, long> Seconds = new();
        public readonly Queue<(DateTimeOffset Time, long Damage)> Recent = new();
        public DateTimeOffset? FirstDamage, LastDamage;
    }

    private readonly object _gate = new();
    private readonly Dictionary<uint, PlayerState> _players = new();
    private readonly List<EncounterSnapshot> _history = new();
    private Guid _fightId = Guid.NewGuid();
    private DateTimeOffset? _start, _last;
    private string? _targetName;
    private uint _lastTargetId;
    private int? _lastTargetNpc;
    private BossSnapshot? _boss, _targetHealth;
    private EncounterSnapshot? _lastFinished;
    private long _topHit;
    private string _topHitBy = "";
    private const int MaxHistory = 80;
    public Guid EncounterId {get{lock(_gate)return _fightId;}}
    public int IdleTimeoutSeconds { get; set; } = 12;
    public bool KeepBossOpen { get; set; }
    public bool ActiveDps { get; set; }
    private TimeSpan Idle => TimeSpan.FromSeconds(Math.Clamp(IdleTimeoutSeconds,5,600));

    public event Action<EncounterSnapshot>? EncounterCompleted;
    public void RestoreHistory(IEnumerable<EncounterSnapshot> fights){lock(_gate){foreach(var f in fights.Where(f=>!f.IsActive&&f.Id!=Guid.Empty).OrderBy(f=>f.StartedAt)){_history.RemoveAll(x=>x.Id==f.Id);_history.Add(f);}while(_history.Count>MaxHistory)_history.RemoveAt(0);}}
    public void RemoveHistory(Guid id){lock(_gate)_history.RemoveAll(f=>f.Id==id);}
    public void FinishAtLastActivity(bool interrupted=false){lock(_gate)EndAndArchive(_last??DateTimeOffset.UtcNow,interrupted);}
    public IReadOnlyList<EncounterSnapshot> History { get { lock (_gate) return _history.Select(s=>RateView(s)).ToArray(); } }

    public void Add(CombatEvent e)
    {
        if (e.HitCount is < 1 or > 9 || e.Amount <= 0 || e.Amount > 10_000_000_000_000L ||
            e.EffectiveHealing is <0 || e.EffectiveHealing>e.Amount ||
            string.IsNullOrWhiteSpace(e.ActorName) || e.ActorName.Length > 100 ||
            e.TargetName is null || e.TargetName.Length > 100 ||
            e.TargetCurrentHp is < 0 || e.TargetMaxHp is <= 0 ||
            (e.TargetMaxHp.HasValue && e.TargetCurrentHp > e.TargetMaxHp)) return;
        lock (_gate)
        {
            // Reconciled heals arrive up to 500 ms after newer damage. Never backdate the
            // encounter clock, resurrect a closed fight, or carry a heal into another pull.
            if(e.Kind==CombatKind.Heal&&e.HealingAssessment is not null&&(_start is null||e.Time<_start))return;
            if (_last is { } prior && e.Time < prior && (e.Kind!=CombatKind.Heal||e.Time<_start)) return;
            if (_last is { } last && e.Time - last > Idle && !(KeepBossOpen && _boss is { CurrentHp: not 0 }))
            {if(e.Kind==CombatKind.Heal&&e.ScoredAmount==0)return;EndAndArchive(last);}
            _lastFinished = null;
            _start ??= e.Time;
            if(e.Kind==CombatKind.Damage||e.ScoredAmount>0)_last=_last is {} old&&old>e.Time?old:e.Time;
            if (e.Kind == CombatKind.Damage && !string.IsNullOrWhiteSpace(e.TargetName))
                { _targetName = e.TargetName; _lastTargetId = e.TargetId; _lastTargetNpc = e.TargetNpcCode; if (_targetHealth?.ActorId != e.TargetId) _targetHealth = null; }
            if (e.Kind == CombatKind.Damage && e.TargetIsBoss)
            {
                if (_boss is null || _boss.ActorId != e.TargetId)
                    _boss = new BossSnapshot(e.TargetId, e.TargetNpcCode,
                        string.IsNullOrWhiteSpace(e.TargetName) ? $"NPC #{e.TargetId}" : e.TargetName,
                        e.TargetCurrentHp, e.TargetMaxHp);
                else
                    _boss = _boss with
                    {
                        Name = !string.IsNullOrWhiteSpace(e.TargetName) ? e.TargetName : _boss.Name,
                        NpcCode = e.TargetNpcCode ?? _boss.NpcCode,
                        CurrentHp = e.TargetCurrentHp ?? _boss.CurrentHp,
                        MaxHp = e.TargetMaxHp ?? _boss.MaxHp
                    };
            }
            if (e.Kind == CombatKind.Damage && e.TargetMaxHp is > 0)
                _targetHealth = new(e.TargetId, e.TargetNpcCode, e.TargetName, e.TargetCurrentHp, e.TargetMaxHp);
            if (!_players.TryGetValue(e.ActorId, out var p))
            {
                p = new PlayerState { Id = e.ActorId, Name = e.ActorName };
                _players.Add(p.Id, p);
            }
            else p.Name = e.ActorName;
            if (!string.IsNullOrWhiteSpace(e.ActorClass)) p.ClassName = e.ActorClass;
            if (e.IsSelf) p.IsSelf = true;
            if (!p.Skills.TryGetValue(e.SkillId, out var skill))
            {
                skill = new SkillState();
                p.Skills.Add(e.SkillId, skill);
            }
            if (!string.IsNullOrWhiteSpace(e.SkillName)) skill.Name = e.SkillName;
            if (e.Kind == CombatKind.Damage)
            {
                p.FirstDamage ??= e.Time; p.LastDamage = e.Time;
                p.Damage += e.Amount;
                p.Hits += e.HitCount;
                skill.Damage += e.Amount;
                skill.Hits += e.HitCount;
                skill.MaxHit = Math.Max(skill.MaxHit, e.Amount);
                if (e.Tags.HasFlag(HitTags.Critical)) { p.Crits += e.HitCount; skill.Crits += e.HitCount; }
                if (e.Amount > _topHit) { _topHit = e.Amount; _topHitBy = p.Name; }
                int sec = (int)Math.Clamp((long)(e.Time - _start.Value).TotalSeconds, 0, 86400);
                p.Seconds[sec] = p.Seconds.GetValueOrDefault(sec) + e.Amount;
                p.Recent.Enqueue((e.Time, e.Amount));
                while (p.Recent.Count > 0 && e.Time - p.Recent.Peek().Time > TimeSpan.FromSeconds(5))
                    p.Recent.Dequeue();
            }
            else
            {
                long scored=e.ScoredAmount,unknown=e.HealingAssessment is not null&&e.EffectiveHealing is null?e.Amount:0;
                long excluded=unknown==0?e.Amount-scored:0;
                if(e.HealingAssessment is null){p.LegacyHealing+=e.Amount;skill.LegacyHealing+=e.Amount;}
                p.RawHealing+=e.Amount;p.ExcludedHealing+=excluded;p.UnverifiedHealing+=unknown;
                skill.RawHealing+=e.Amount;skill.ExcludedHealing+=excluded;skill.UnverifiedHealing+=unknown;
                p.Healing+=scored;skill.Healing+=scored;
                if(scored>0){p.HealingHits+=e.HitCount;skill.HealingHits+=e.HitCount;if(e.Tags.HasFlag(HitTags.Critical))skill.HealingCrits+=e.HitCount;skill.MaxHeal=Math.Max(skill.MaxHeal,scored);}
            }
        }
    }

    public void UpdateIdentity(uint id, string name, string? className, bool isSelf)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(id, out var p)) return;
            if (_topHitBy == p.Name) _topHitBy = name;
            p.Name = name; p.ClassName = className ?? p.ClassName;
            if (isSelf) { foreach (var other in _players.Values) other.IsSelf = false; p.IsSelf = true; }
        }
    }
    public void UpdateHealth(uint id, long? current, long? maximum)
    {
        lock (_gate)
        {
            if (_boss?.ActorId == id) _boss = _boss with { CurrentHp = current, MaxHp = maximum };
            if (_lastTargetId == id && maximum is > 0) _targetHealth = new(id, _lastTargetNpc, _targetName ?? "#" + id, current, maximum);
        }
    }

    public EncounterSnapshot Snapshot(DateTimeOffset now,bool includeDetails=true)
    {
        lock (_gate)
        {
            if (_start is null) return RateView(_lastFinished ?? Empty(now),includeDetails);
            if (_last.HasValue && now - _last.Value >= Idle && !(KeepBossOpen && _boss is { CurrentHp: not 0 }))
            {
                var completed = Build(_last.Value, false);
                Archive(completed);
                Clear();
                _lastFinished = completed;
                return RateView(completed,includeDetails);
            }
            return Build(now, true,includeDetails);
        }
    }

    public void Finish(DateTimeOffset now) { lock(_gate) EndAndArchive(now); }
    public void ClearHistory() { lock(_gate) _history.Clear(); }

    public EncounterSnapshot Reset(DateTimeOffset now)
    {
        lock (_gate)
        {
            var completed = _start is null ? (_lastFinished ?? Empty(now)) : Build(_last ?? now, false) with{Interrupted=true};
            if (_start is not null) Archive(completed);
            Clear();
            _lastFinished = null;
            return completed;
        }
    }

    private EncounterSnapshot Build(DateTimeOffset now, bool active,bool includeDetails=true)
    {
        if (_start is null) return Empty(now);
        double duration = Math.Max(1, (now - _start.Value).TotalSeconds);
        long total = _players.Values.Sum(p => p.Damage);
        long healing = _players.Values.Sum(p => p.Healing);
        var players = _players.Values.Select(p =>
        {
            double window = Math.Min(5, duration);
            double burst = p.Recent.Where(h => h.Time <= now && now - h.Time <= TimeSpan.FromSeconds(5))
                .Sum(h => (double)h.Damage) / window;
            return new PlayerSnapshot(p.Id, p.Name, p.Damage, p.Healing, p.Damage / (ActiveDps && p.FirstDamage is {} a && p.LastDamage is {} z ? Math.Max(1,(z-a).TotalSeconds) : duration),
                burst, total == 0 ? 0 : (double)p.Damage / total,
                p.Hits, p.Hits == 0 ? 0 : (double)p.Crits / p.Hits,
                includeDetails?p.Skills.Select(x => new SkillSnapshot(x.Key, x.Value.Damage, x.Value.Healing,
                    x.Value.Hits, x.Value.Crits, x.Value.Name, x.Value.MaxHit,
                    x.Value.Hits == 0 ? 0 : (double)x.Value.Damage / x.Value.Hits,x.Value.HealingHits,x.Value.HealingCrits,x.Value.MaxHeal,x.Value.RawHealing,x.Value.ExcludedHealing,x.Value.UnverifiedHealing,x.Value.LegacyHealing))
                    .OrderByDescending(x => x.Damage).ToArray():[],
                p.ClassName, p.IsSelf, includeDetails?p.Seconds.OrderBy(x => x.Key)
                    .Select(x => new DamagePoint(x.Key, x.Value)).ToArray():null,p.FirstDamage is {} first&&p.LastDamage is {} last?Math.Max(1,(last-first).TotalSeconds):0,p.RawHealing,p.ExcludedHealing,p.UnverifiedHealing,p.LegacyHealing,p.Skills.Count,p.HealingHits);
        }).OrderByDescending(x => x.Damage).ToArray();
        return new EncounterSnapshot(_fightId, _start.Value, duration, active,
            total, healing, total / duration, players, _topHit, _topHitBy, _boss, _targetName, _targetHealth);
    }

    private EncounterSnapshot RateView(EncounterSnapshot s,bool includeDetails=true)
    {
        if(s.Players.Count==0)return s;
        return s with{Players=s.Players.Select(p=>p with{Dps=p.Damage/(ActiveDps&&p.ActiveSeconds>0?p.ActiveSeconds:Math.Max(1,s.DurationSeconds)),
            Skills=includeDetails?p.Skills:[],Timeline=includeDetails?p.Timeline:null}).ToArray()};
    }

    private void EndAndArchive(DateTimeOffset t,bool interrupted=false)
    {
        if (_start is not null)
        {
            var completed = Build(t, false) with{Interrupted=interrupted};
            Archive(completed);
            Clear();
            _lastFinished = completed;
        }
    }

    private void Archive(EncounterSnapshot snapshot)
    {
        _history.Add(snapshot);
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        EncounterCompleted?.Invoke(snapshot);
    }

    private void Clear()
    {
        _players.Clear(); _boss = _targetHealth = null; _lastTargetId = 0; _lastTargetNpc = null; _targetName = null;
        _topHit = 0; _topHitBy = "";
        _fightId = Guid.NewGuid(); _start = _last = null;
    }

    private static EncounterSnapshot Empty(DateTimeOffset now) =>
        new(Guid.Empty, now, 0, false, 0, 0, 0, []);
}
