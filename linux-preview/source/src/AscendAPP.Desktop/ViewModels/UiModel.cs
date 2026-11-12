using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Settings;
using AscendAPP.Desktop.Services;

namespace AscendAPP.Desktop.ViewModels;

public abstract class NotifyModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Changed(name);
        return true;
    }
}

public sealed class PlayerRow : NotifyModel
{
    private string _name = "", _rank = "", _damage = "", _dps = "", _hps = "", _crit = "", _share = "", _info = "";
    private double _fill, _overlayFill;
    private Brush _bar = Brushes.SlateBlue;
    private bool _isSelf;
    private string _className = "Unknown", _displayRate = "—", _displayAmount = "—";
    private string _displayName="";
    public string DisplayName {get=>_displayName;set=>Set(ref _displayName,value);}
    public string ClassIcon => ClassName switch {"Ranger"=>"Bow","Sorcerer"=>"Spark","Assassin"=>"Combat","Templar"=>"Shield","Cleric"=>"Heal","Chanter"=>"Wings","Spiritmaster" or "Elementalist"=>"Flame","Gladiator"=>"Combat","Brawler"=>"Fist",_=>"Account"};
    private string _gearScore="—",_combatPower="—",_ratingInfo="Gear score and combat power have not been observed.";
    public string GearScore {get=>_gearScore;set=>Set(ref _gearScore,value);}
    public string CombatPower {get=>_combatPower;set=>Set(ref _combatPower,value);}
    public string RatingInfo {get=>_ratingInfo;set=>Set(ref _ratingInfo,value);}
    internal int SortOrder;
    public uint ActorId {get;set;}
    public int? ServerId {get;set;}
    public bool HasCombat {get;set;}
    public long RawDamage { get; set; }
    public long RawHealing { get; set; }
    public string ClassName { get => _className; set { if (Set(ref _className,value)) {Changed(nameof(ClassTag)); Changed(nameof(ClassIcon));} } }
    public string ClassTag => ClassName switch { "Ranger"=>"RA", "Sorcerer"=>"SO", "Assassin"=>"AS", "Templar"=>"TE", "Cleric"=>"CL", "Chanter"=>"CH", "Spiritmaster" or "Elementalist"=>"SM", "Gladiator"=>"GL", "Brawler"=>"BR", _=>"?" };
    public string DisplayRate { get=>_displayRate; set=>Set(ref _displayRate,value); }
    public string DisplayAmount { get=>_displayAmount; set=>Set(ref _displayAmount,value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Rank { get => _rank; set => Set(ref _rank, value); }
    public string Damage { get => _damage; set => Set(ref _damage, value); }
    public string Dps { get => _dps; set => Set(ref _dps, value); }
    public string Hps { get => _hps; set => Set(ref _hps, value); }
    public string Crit { get => _crit; set => Set(ref _crit, value); }
    public string Share { get => _share; set => Set(ref _share, value); }
    public double OverlayFill { get => _overlayFill; set => Set(ref _overlayFill,value); }
    public double Fill { get => _fill; set => Set(ref _fill, value); }
    public Brush Bar { get => _bar; set => Set(ref _bar, value); }
    public string Info { get => _info; set => Set(ref _info, value); }
    public bool IsSelf { get => _isSelf; set => Set(ref _isSelf, value); }
}

public sealed class UiModel : NotifyModel
{
    private MeterPreferences _preferences=new();
    public MeterPreferences Preferences {get=>_preferences;set{_preferences=value;Changed();Changed(nameof(RateTitle));}}
    private string _headerState="Idle",_selfName="Detecting character",_notice="";
    private string _captureLabel="Stopped",_captureDetail="Capture stopped",_captureColor="#87989A",_captureState="Stopped";
    public string CaptureLabel {get=>_captureLabel;set=>Set(ref _captureLabel,value);}
    public string CaptureDetail {get=>_captureDetail;set=>Set(ref _captureDetail,value);}
    public string CaptureColor {get=>_captureColor;set=>Set(ref _captureColor,value);}
    public string CaptureState {get=>_captureState;set=>Set(ref _captureState,value);}
    public void ApplyCaptureHealth(CaptureHealth h){CaptureState=h.State;CaptureLabel=h.Label;CaptureDetail=h.Detail;CaptureColor=h.Color;}
    public string StripSelfRate=>FeaturedPlayer is {HasCombat:true} p?p.DisplayRate:"—";
    public string StripSelfTitle=>ShowHealing?"You HPS":"You DPS";
    public string StripHealth=>HasBossHealth?BossHealthFraction.ToString("P0"):"HP —";
    public string StripPartyRate=>Latest?.Players.Count>0?PartyDps:"—";
    public string HeaderState {get=>_headerState;set=>Set(ref _headerState,value);}
    private string? _selfClass;public string? SelfClass {get=>_selfClass;set=>Set(ref _selfClass,value);}
    private string _selfGearScore="—",_selfCombatPower="—",_selfRatingInfo="No verified character profile yet.";
    public string SelfGearScore {get=>_selfGearScore;set=>Set(ref _selfGearScore,value);}
    public string SelfCombatPower {get=>_selfCombatPower;set=>Set(ref _selfCombatPower,value);}
    public string SelfRatingInfo {get=>_selfRatingInfo;set=>Set(ref _selfRatingInfo,value);}
    public void ApplyOwnRating(AscendAPP.Core.Game.PlayerRating? rating,DateTimeOffset at)
    {
        SelfGearScore=ScoreText(rating);SelfCombatPower=PowerText(rating);
        SelfRatingInfo=rating is null?"Waiting for automatic own-character or party score data. Keep capture running before entering the character. Click here for source diagnostics.":RatingDetails(rating,at);
    }
    private bool _identityRefreshPending,_selfIdentified;
    public bool IdentityRefreshPending {get=>_identityRefreshPending;set=>Set(ref _identityRefreshPending,value);}
    public bool SelfIdentified {get=>_selfIdentified;set=>Set(ref _selfIdentified,value);}
    public string SelfName {get=>_selfName;set=>Set(ref _selfName,value);}
    public string Notice {get=>_notice;set=>Set(ref _notice,value);}
    public ObservableCollection<TimerRow> Timers {get;} = new();
    public ObservableCollection<TicketRow> Tickets {get;} = new();
    public ObservableCollection<WorldTimerRow> ScheduledEvents {get;}=new();
    public ObservableCollection<WorldTimerRow> FieldBossTimers {get;}=new();
    private string _worldTimerClock="",_worldTimerInfo="Open the in-game map → Field bosses to sync";
    public string WorldTimerClock {get=>_worldTimerClock;set=>Set(ref _worldTimerClock,value);}
    public string WorldTimerInfo {get=>_worldTimerInfo;set=>Set(ref _worldTimerInfo,value);}
        private bool _showHealing;
    private string _energy = "—", _energyDetail = "Waiting for energy", _energyTooltip = "Odyle Energy has not been decoded for this character.";
    private string _kill = "—", _berserk = "—", _margin = "—", _forecastStatus = "Waiting for boss damage", _forecastScope = "Tracked boss DPS", _forecastTooltip = "";
    private bool _forecastWarning,_forecastIsDummy;
    public bool ForecastIsDummy {get=>_forecastIsDummy;set=>Set(ref _forecastIsDummy,value);}
    public bool ShowHealing { get=>_showHealing; set { if (Set(ref _showHealing,value)) { Changed(nameof(RateTitle)); Changed(nameof(SelfHeading)); if (_latest is { } s) Apply(s); } } }
    public string RateTitle => ShowHealing ? "eHPS · HP" : Preferences.DpsCalculation=="Active"?"aDPS":"eDPS";
    public string SelfHeading => ShowHealing ? "YOUR HEALING" : "YOUR DAMAGE";
    public string FeaturedRate => FeaturedPlayer is { } p ? (ShowHealing ? p.Hps + "/s" : p.Dps) : "—";
    public string FeaturedAmount => FeaturedPlayer is { } p ? (ShowHealing ? Compact(p.RawHealing) + " healing" : p.Damage + " damage") : "Waiting for player";
    public string OdyleAvailable { get=>_energy; set=>Set(ref _energy,value); }
    public string OdyleDetail { get=>_energyDetail; set=>Set(ref _energyDetail,value); }
    public string OdyleTooltip { get=>_energyTooltip; set=>Set(ref _energyTooltip,value); }
    private string _forecastRate="—";
    public string ForecastRate {get=>_forecastRate;set=>Set(ref _forecastRate,value);}
    public string TimeToKill { get=>_kill; set=>Set(ref _kill,value); }
    public string BerserkIn { get=>_berserk; set=>Set(ref _berserk,value); }
    public string ForecastMargin { get=>_margin; set=>Set(ref _margin,value); }
    public string ForecastStatus { get=>_forecastStatus; set=>Set(ref _forecastStatus,value); }
    public string ForecastScope { get=>_forecastScope; set=>Set(ref _forecastScope,value); }
    public string ForecastTooltip { get=>_forecastTooltip; set=>Set(ref _forecastTooltip,value); }
    public bool ForecastWarning { get=>_forecastWarning; set=>Set(ref _forecastWarning,value); }
    public ObservableCollection<PlayerRow> OverlayRows {get;}=new();
    private readonly Dictionary<uint,PlayerRow> _actorRows=new();
    private readonly Dictionary<(string Name,int Server),PlayerRow> _rosterRows=new();
    private readonly List<PlayerRow> _nextRows=new(),_orderedRows=new();
    private Guid _rowEncounter;
    private static void SyncRows(ObservableCollection<PlayerRow> target,List<PlayerRow> wanted)
    {
        for(int i=0;i<wanted.Count;i++)
        {
            if(i<target.Count&&ReferenceEquals(target[i],wanted[i]))continue;
            int existing=target.IndexOf(wanted[i]);
            if(existing>=0)target.Move(existing,i);else target.Insert(i,wanted[i]);
        }
        while(target.Count>wanted.Count)target.RemoveAt(target.Count-1);
    }
    private static readonly Comparison<PlayerRow> DamageOrder=(a,b)=>CompareRows(a,b,false),HealingOrder=(a,b)=>CompareRows(a,b,true);
    private static int CompareRows(PlayerRow a,PlayerRow b,bool healing)
    {
        int comparison=(healing?b.RawHealing:b.RawDamage).CompareTo(healing?a.RawHealing:a.RawDamage);
        if(comparison!=0)return comparison;
        comparison=b.HasCombat.CompareTo(a.HasCombat);
        return comparison!=0?comparison:a.SortOrder.CompareTo(b.SortOrder);
    }
    public void ApplyEnergy(OdyleEnergy? energy, bool demo = false)
    {
        OdyleAvailable = energy is null ? "—" : energy.Available.ToString("N0");
        OdyleDetail = energy is null ? "Waiting for energy" : $"{energy.Regular:N0} regular + {energy.Bonus:N0} bonus";
        OdyleTooltip = energy is null ? "No energy snapshot for the current character. Start capture before character entry." :
            $"{(demo ? "DEMO · " : "")}Last received {energy.ObservedAt.ToLocalTime():g}. {OdyleDetail}. No assumed cap, refill or dungeon cost.";
    }
    private static string Duration(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "—";
        if (seconds > 86400) return ">24 h";
        var t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
    public void ApplyForecast(BossForecast f)
    {
        ForecastRate=f.DamagePerSecond>0?Compact(f.DamagePerSecond)+"/s":"—";
        TimeToKill = f.KillSeconds is { } kill ? (kill == 0 ? "Defeated" : "≈ " + Duration(kill)) : "—";
        BerserkIn = f.BerserkSeconds is { } rage ? (rage == 0 ? "Expected now" : "≈ " + Duration(rage)) : "—";
        ForecastMargin = f.MarginSeconds is { } margin ? (margin >= 0 ? "≈ +" : "≈ −") + Duration(Math.Abs(margin)) : "—";
        ForecastIsDummy=f.IsTrainingDummy;
        ForecastStatus = f.Status; ForecastScope = f.Scope; ForecastWarning = f.MarginSeconds < 0;
        ForecastTooltip = f.Status + ". Kill estimate = current target HP / measured damage per second to this target. " +
            f.Scope + "; missing packets or members may make it incomplete. " + f.BerserkBasis + ".";
    }

    private string _status = "Ready to capture";
    private string _mode = "OFFLINE";
    private string _partyDps = "0";
    private string _damage = "0";
    private string _heal = "0";
    private string _clock = "00:00";
    private string _traffic = "0";
    private string _frames = "0";
    private string _decoded = "0";
    private string _selection = "Select an interface to start capture.";
    private bool _hasRows;
    public void ApplyRatings(Func<PlayerRow,AscendAPP.Core.Game.PlayerRating?> lookup,DateTimeOffset at)
    {
        foreach(var row in Rows)
        {
            var value=lookup(row);row.GearScore=ScoreText(value);row.CombatPower=PowerText(value);
            row.RatingInfo=value is null?"Waiting for automatic party or player profile scores. Keep capture running while joining the party. Unknown fields are not estimated.":RatingDetails(value,at);
        }
    }
    internal static string ScoreText(AscendAPP.Core.Game.PlayerRating? value)=>value?.GearScore is {} n?n.ToString("N0")+(value.ManualGearScore?"*":""):"—";
    internal static string PowerText(AscendAPP.Core.Game.PlayerRating? value)=>value?.CombatPower is {} n?Compact(n)+(value.ManualCombatPower?"*":""):"—";
    private static string RatingDetails(AscendAPP.Core.Game.PlayerRating value,DateTimeOffset at)
    {
        string Field(string label,int? score,DateTimeOffset observed)=>score is {} n?$"{label}: {n:N0} · seen {Math.Max(0,(at-observed).TotalSeconds):F0}s ago":$"{label}: — · not observed or expired";
        return value.Source+"\n"+Field("GS",value.GearScore,value.GearScoreAt??value.At)+"\n"+Field("CP",value.CombatPower,value.CombatPowerAt??value.At)+(value.CombatPower.HasValue?" · may be normalized in PvP arenas":"");
    }
    private PlayerRow? _featuredPlayer;
    private string _encounterName = "Waiting for combat";
    private string _fightState = "AWAITING COMBAT";
    private string _topHit = "—";
    private string _topHitBy = "";
    private string _bossHealthText = "TARGET HP  ·  Waiting for decoded health";
    private double _bossHealthFraction;
    private bool _hasBossHealth;
    private EncounterSnapshot? _latest;
    private IReadOnlyList<PartyDisplayMember> _party=[];
    private string _healingInfo="HP-based estimate · unknown healing is unscored";
    public string HealingInfo {get=>_healingInfo;set=>Set(ref _healingInfo,value);}
    public void SetParty(IReadOnlyList<PartyDisplayMember> members,string scope)
    { _party=scope=="Self and party"?members:[]; }

    private int _playerCount;
    private DeviceChoice? _selectedDevice;
    private string _port = "";
    public ObservableCollection<PlayerRow> Rows { get; } = new();
    public ObservableCollection<DeviceChoice> Devices { get; } = new();

    public string Status { get => _status; set => Set(ref _status, value); }
    public string Mode { get => _mode; set => Set(ref _mode, value); }
    public string PartyDps { get => _partyDps; set => Set(ref _partyDps, value); }
    public string Damage { get => _damage; set => Set(ref _damage, value); }
    public string Healing { get => _heal; set => Set(ref _heal, value); }
    public string Clock { get => _clock; set => Set(ref _clock, value); }
    public string Traffic { get => _traffic; set => Set(ref _traffic, value); }
    public string Frames { get => _frames; set => Set(ref _frames, value); }
    public string Decoded { get => _decoded; set => Set(ref _decoded, value); }
    public string Selection { get => _selection; set => Set(ref _selection, value); }
    public bool HasRows { get => _hasRows; set => Set(ref _hasRows, value); }
    public PlayerRow? FeaturedPlayer { get => _featuredPlayer; private set => Set(ref _featuredPlayer, value); }
    public string EncounterName { get => _encounterName; set => Set(ref _encounterName, value); }
    public string FightState { get => _fightState; set => Set(ref _fightState, value); }
    public string TopHit
    {
        get => _topHit;
        set { if (Set(ref _topHit, value)) Changed(nameof(TopHitDisplay)); }
    }
    public string TopHitDisplay => $"TOP HIT {TopHit}";
    public string TopHitBy { get => _topHitBy; set => Set(ref _topHitBy, value); }
    public string BossHealthText { get => _bossHealthText; set => Set(ref _bossHealthText, value); }
    public double BossHealthFraction { get => _bossHealthFraction; set => Set(ref _bossHealthFraction, value); }
    public bool HasBossHealth { get => _hasBossHealth; set => Set(ref _hasBossHealth, value); }
    public EncounterSnapshot? Latest => _latest;

    public int PlayerCount
    {
        get => _playerCount;
        set { if (Set(ref _playerCount, value)) Changed(nameof(PlayerCountLabel)); }
    }
    public string PlayerCountLabel => $"{PlayerCount} players";
    public DeviceChoice? SelectedDevice { get => _selectedDevice; set => Set(ref _selectedDevice, value); }
    public bool AutoAdapters { get; set; } = true;
    public string Port { get => _port; set => Set(ref _port, value); }

    private static readonly Dictionary<string,Brush> ClassBrushes = new()
    {
        ["Ranger"]=NewBrush("#8BC8A3"), ["Sorcerer"]=NewBrush("#83BFFF"), ["Assassin"]=NewBrush("#CBA3E7"),
        ["Templar"]=NewBrush("#D3B781"), ["Cleric"]=NewBrush("#7FCBD0"), ["Chanter"]=NewBrush("#DDC780"),
        ["Gladiator"]=NewBrush("#DA9D83"), ["Spiritmaster"]=NewBrush("#BBA7DF"), ["Elementalist"]=NewBrush("#BBA7DF"), ["Brawler"]=NewBrush("#B6C4D9")
    };
    private static readonly Brush UnknownClass = NewBrush("#A9B4C3");
    private static Brush ClassBrush(string? name) => name is not null && ClassBrushes.TryGetValue(name,out var b) ? b : UnknownClass;
    private static Brush NewBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
    private static string Compact(double value) => value >= 1_000_000 ? $"{value / 1_000_000:0.00}M" :
        value >= 1000 ? $"{value / 1000:0.0}K" : $"{value:0}";

    public void Apply(EncounterSnapshot snap)
    {
        _latest = snap;
        Changed(nameof(Latest));
        if (!string.IsNullOrWhiteSpace(snap.Boss?.Name)) EncounterName = snap.Boss.Name;
        else if (!string.IsNullOrWhiteSpace(snap.TargetName)) EncounterName = snap.TargetName;
        var health = snap.Boss ?? snap.TargetHealth;
        HasBossHealth = health?.HealthFraction.HasValue == true;
        BossHealthFraction = health?.HealthFraction ?? 0;
        BossHealthText = HasBossHealth && health is { CurrentHp: { } hp, MaxHp: { } max }
            ? $"{Compact(hp)} / {Compact(max)} HP    {BossHealthFraction:P1}"
            : "TARGET HP  ·  Waiting for decoded health";
        PartyDps = Compact(snap.PartyDps) + "/s";
        Damage = Compact(snap.TotalDamage);
        Healing = Compact(snap.TotalHealing);
        Clock = TimeSpan.FromSeconds(snap.DurationSeconds).ToString(snap.DurationSeconds>=3600?@"hh\:mm\:ss":@"mm\:ss");
        PlayerCount = snap.Players.Count;
        FightState = PlayerCount == 0 ? "AWAITING COMBAT" : snap.IsActive ? "IN PROGRESS" : "FIGHT ENDED";
        TopHit = snap.TopHit > 0 ? Compact(snap.TopHit) : "—";
        TopHitBy = snap.TopHitBy;
        double top = Math.Max(1, snap.Players.FirstOrDefault()?.Damage ?? 1);
        if(_rowEncounter!=snap.Id){_rowEncounter=snap.Id;_actorRows.Clear();}
        if(_rosterRows.Count>64)_rosterRows.Clear();
        _nextRows.Clear();
        long topHealing=1;foreach(var player in snap.Players)topHealing=Math.Max(topHealing,player.Healing);
        for (int i = 0; i < snap.Players.Count; i++)
        {
            var p = snap.Players[i];
            if(!_actorRows.TryGetValue(p.ActorId,out var row))_actorRows[p.ActorId]=row=new PlayerRow();
            _nextRows.Add(row);
            row.Rank = (i + 1).ToString();
            row.ActorId=p.ActorId;row.HasCombat=true;
            row.Name = p.Name;
            row.DisplayName = Preferences.HideNames && !p.IsSelf ? p.ClassName ?? "Unknown" : p.Name;
            row.Damage = Compact(p.Damage);
            row.Dps = Compact(p.Dps) + "/s";
            row.Hps = Compact(p.Healing / Math.Max(1, snap.DurationSeconds));
            row.Crit = $"{p.CritRate:P0}";
            row.Share = $"{p.Share:P1}";
            row.Bar = ClassBrush(p.ClassName);
            row.ClassName = p.ClassName ?? "Unknown";
            row.RawDamage = p.Damage; row.RawHealing = p.Healing;
            row.DisplayRate = ShowHealing ? row.Hps + "/s" : row.Dps;
            row.DisplayAmount = ShowHealing ? Compact(p.Healing) : row.Damage;
            row.Fill = Math.Clamp(p.Damage / top, 0, 1);
            row.OverlayFill = ShowHealing ? Math.Clamp((double)p.Healing / topHealing,0,1) : row.Fill;
            row.IsSelf = p.IsSelf;
            row.Info = ShowHealing?$"{p.ClassName??"Unknown class"} · {(p.HealingHits>0?p.HealingHits:p.Skills.Sum(s=>s.HealingHits))} scored heals · {Compact(p.RawHealing)} raw · {Compact(p.ExcludedHealing)} excluded · {Compact(p.UnverifiedHealing)} unverified":$"{p.ClassName ?? "Unknown class"}  •  Burst: {Compact(p.Burst5s)}/s  •  {p.Hits} hits  •  {(p.SkillCount>0?p.SkillCount:p.Skills.Count)} skills";
        }
        foreach(var row in _nextRows)
        {
            var member=_party.FirstOrDefault(m=>m.ActorId==row.ActorId);
            row.ServerId=member?.ServerId;
            if(member is not null)row.Info+=$" · level {member.Level?.ToString()??"—"} · server {member.ServerId} · {member.Binding}";
        }
        // Roster-only rows carry no fake CombatEvent, encounter statistics or invented combat ID.
        foreach(var member in _party)
        {
            if(member.ActorId is {} id&&_nextRows.Any(r=>r.ActorId==id))continue;
            var key=(member.Name,member.ServerId);
            if(!_rosterRows.TryGetValue(key,out var row))_rosterRows[key]=row=new PlayerRow();
            row.ActorId=member.ActorId??0;row.ServerId=member.ServerId;row.Name=member.Name;row.HasCombat=false;
            row.DisplayName=Preferences.HideNames&&!member.IsSelf?member.ClassName??"Unknown":member.Name;
            row.ClassName=member.ClassName??"Unknown";row.IsSelf=member.IsSelf;row.Bar=ClassBrush(member.ClassName);
            row.DisplayRate="—";row.DisplayAmount="—";row.Dps="—";row.Hps="—";row.Damage="—";
            row.Info=$"{member.ClassName??"Unknown class"} · level {member.Level?.ToString()??"—"} · server {member.ServerId} · {member.Binding} · no admitted combat yet";
            _nextRows.Add(row);
        }
        for(int i=0;i<_nextRows.Count;i++)_nextRows[i].SortOrder=i;
        SyncRows(Rows,_nextRows);
        HealingInfo=snap.Players.Any(p=>p.LegacyHealing>0)
            ?"Replay / demo · original healing totals may include overheal"
            :$"HP estimate · {Compact(snap.Players.Sum(p=>p.ExcludedHealing))} excluded · {Compact(snap.Players.Sum(p=>p.UnverifiedHealing))} unverified";
        HasRows = Rows.Count > 0;
        FeaturedPlayer = Rows.FirstOrDefault(r => r.IsSelf);
        _orderedRows.Clear();_orderedRows.AddRange(_nextRows);
        _orderedRows.Sort(ShowHealing?HealingOrder:DamageOrder);SyncRows(OverlayRows,_orderedRows);
        Changed(nameof(FeaturedRate)); Changed(nameof(FeaturedAmount));Changed(nameof(StripSelfRate));Changed(nameof(StripSelfTitle));Changed(nameof(StripHealth));Changed(nameof(StripPartyRate));
    }
}
