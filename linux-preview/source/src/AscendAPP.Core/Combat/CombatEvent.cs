namespace AscendAPP.Core.Combat;

public enum CombatKind { Damage, Heal }

[Flags]
public enum HitTags { None = 0, Critical = 1, Back = 2, Dot = 4 }

/// <summary>
/// Version-neutral normalized record. Optional fields are only set when confirmed by the
/// decoder, fixture, or user-supplied event log. Never infer these fields from damage.
/// </summary>
public sealed record CombatEvent(
    DateTimeOffset Time,
    uint ActorId,
    string ActorName,
    uint TargetId,
    string TargetName,
    int SkillId,
    long Amount,
    CombatKind Kind,
    HitTags Tags = HitTags.None,
    string? SkillName = null,
    string? ActorClass = null,
    bool IsSelf = false,
    bool TargetIsBoss = false,
    int? TargetNpcCode = null,
    long? TargetCurrentHp = null,
    long? TargetMaxHp = null,
    int HitCount = 1, DateTimeOffset? TargetHpObservedAt = null, bool TargetIsTrainingDummy=false,
    long? EffectiveHealing=null,string? HealingAssessment=null)
{
    // Old logs / explicitly labelled demos retain their original raw amount.
    public long ScoredAmount=>Kind==CombatKind.Heal&&HealingAssessment is not null?EffectiveHealing??0:Amount;
}

public sealed record SkillSnapshot(int SkillId, long Damage, long Healing, int Hits, int CriticalHits,
    string? Name = null, long MaxHit = 0, double AverageHit = 0, int HealingHits=0,int HealingCriticalHits=0,long MaxHeal=0,long RawHealing=0,long ExcludedHealing=0,long UnverifiedHealing=0,long LegacyHealing=0);
public sealed record DamagePoint(int Second, long Damage);
public sealed record PlayerSnapshot(
    uint ActorId, string Name, long Damage, long Healing, double Dps,
    double Burst5s, double Share, int Hits, double CritRate,
    IReadOnlyList<SkillSnapshot> Skills,
    string? ClassName = null, bool IsSelf = false,
    IReadOnlyList<DamagePoint>? Timeline = null, double ActiveSeconds=0,long RawHealing=0,long ExcludedHealing=0,long UnverifiedHealing=0,long LegacyHealing=0,int SkillCount=0,int HealingHits=0)
{
    public double Hps(double seconds) => Healing / Math.Max(1, seconds);
}
public sealed record BossSnapshot(uint ActorId, int? NpcCode, string Name, long? CurrentHp, long? MaxHp)
{
    public double? HealthFraction => MaxHp is > 0 && CurrentHp is >= 0
        ? Math.Clamp((double)CurrentHp.Value / MaxHp.Value, 0, 1) : null;
}
public sealed record EncounterSnapshot(
    Guid Id, DateTimeOffset StartedAt, double DurationSeconds, bool IsActive,
    long TotalDamage, long TotalHealing, double PartyDps,
    IReadOnlyList<PlayerSnapshot> Players, long TopHit = 0, string TopHitBy = "",
    BossSnapshot? Boss = null, string? TargetName = null, BossSnapshot? TargetHealth = null,bool Interrupted=false);
