using AscendAPP.Core.Combat;
using System.Text.Json.Serialization;
using AscendAPP.Core.Storage;
using System.Text.Json;
namespace AscendAPP.Core.Settings;

public sealed class MeterPreferences
{

    [JsonExtensionData] public Dictionary<string,JsonElement>? AdditionalSettings {get;set;}
    public List<AscendAPP.Core.Game.ManualPlayerRating> OwnScoreEntries {get;set;}=[];
    public string AccentColor {get;set;}=AppearanceColors.DefaultAccent;
    public string PanelColor {get;set;}=AppearanceColors.DefaultPanel;
    public string LogoColor {get;set;}=AppearanceColors.DefaultLogo;
    public bool LogoFollowsAccent {get;set;}=true;
    public string PetCounterStyle {get;set;}="Beside loot rows";
    public double LootRegionLeft {get;set;}=.385;
    public double LootRegionTop {get;set;}=.28;
    public double LootRegionWidth {get;set;}=.20;
    public double LootRegionHeight {get;set;}=.48;
    public int LootCounterGap {get;set;}=44;
    public bool ShowPetPickups {get;set;}=true;
    public double? PetPickupLeft {get;set;}
    public double? PetPickupTop {get;set;}
    public string Region { get; set; } = "Europe";
    public bool AutoCapture { get; set; } = true;
    public bool AutoAdapters { get; set; } = true;
    public string Adapter { get; set; } = "";
    public string Port { get; set; } = "";
    public int IdleSeconds { get; set; } = 20;
    public bool BossResetDetection { get; set; } = true;
    public string Targeting { get; set; } = "Bosses and dummies";
    public string CaptureScope { get; set; } = "Self and party";
    public int CapturePolicyVersion { get; set; }
    public bool PartyOnly { get; set; } = true;
    public int DummySeconds { get; set; }
    public string DummyNpcIds { get; set; } = "";
    public string DpsCalculation { get; set; } = "Encounter";
    public bool HideTotalDamage { get; set; }
    public bool ShowPartyDps { get; set; } = true;
    public bool ShowBerserk { get; set; } = true;
    public bool HideNames { get; set; }
    public string CopyFormat { get; set; } = "Single line";
    public bool Clock24 { get; set; } = true;
    public double OverlayOpacity { get; set; } = 0.96;
    public double UiScale { get; set; } = 1;
    public int UpdateMs { get; set; } = 250;
    public bool SmoothNumbers {get;set;}=true;
    public int VisiblePlayers { get; set; } = 6;
    public bool ShowPetOverlay {get;set;}
    public bool PetOverlayLocked {get;set;}
    public bool PetOnlyPinned {get;set;}
    public bool PetHideCompleted {get;set;}=true;
    public double PetOverlayScale {get;set;}=1;
    public double PetOverlayOpacity {get;set;}=.96;
    public double? PetOverlayLeft {get;set;}
    public double? PetOverlayTop {get;set;}
    public List<uint> PinnedPets {get;set;}=[];
    public bool CombatStrip {get;set;}
    public string StripHotkey {get;set;}="Ctrl+Shift+F6";
    public bool FocusMode { get; set; }
    public bool FadeIdle { get; set; } = true;
    public double FadedOpacity { get; set; } = 0.45;
    public int FadeAfterSeconds { get; set; } = 60;
    public bool OnlyAboveGame { get; set; }
    public string GameProcess { get; set; } = "AION2";
    public bool RememberClickThrough { get; set; }
    public bool ClickThrough { get; set; }
    public bool PositionLocked { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool TrayWhenGameCloses { get; set; }
    public double EventVolume { get; set; } = 0.2;
    public string EventVisibility { get; set; } = "Always";
    public bool ShowEventOverlay {get;set;}
    public double? EventOverlayScale {get;set;}
    public double? EventOverlayOpacity {get;set;}
    public bool EventOverlayLocked {get;set;}
    public double? EventOverlayLeft {get;set;}
    public double? EventOverlayTop {get;set;}
    public bool ShowShugoTimer {get;set;}=true;
    public bool ShowRiftTimer {get;set;}=true;
    public bool ShowInvasionTimer {get;set;}
    public bool ShowFieldBossTimers {get;set;}=true;
    public int? WorldTimerServerId {get;set;}
    public int RiftShiftMinutes {get;set;}
    public bool GameTimerAlerts {get;set;}
    public string GameTimerLeadMinutes {get;set;}="5,1";
    public string EventsHotkey {get;set;}="Ctrl+Shift+F7";
    public string HideHotkey { get; set; } = "Ctrl+Shift+F10";
    public string ResetHotkey { get; set; } = "Ctrl+Shift+F9";
    public string ClearHotkey { get; set; } = "Ctrl+Shift+F8";
    public string CopyHotkey { get; set; } = "Ctrl+Shift+F11";
    public string ClickThroughHotkey { get; set; } = "Ctrl+Shift+F12";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public List<EventReminder> Reminders { get; set; } = [];
    private sealed record DummyCache(string Text,IReadOnlySet<int> Ids);
    private DummyCache _dummyCache=new("",System.Collections.Frozen.FrozenSet<int>.Empty);
    public IReadOnlySet<int> DummyIds()
    {
        string text=DummyNpcIds??"";
        var cache=_dummyCache;if(cache.Text==text)return cache.Ids;
        var ids=new HashSet<int>();
        foreach(var value in text.Split([',',';',' '],StringSplitOptions.RemoveEmptyEntries))if(int.TryParse(value,out int id)&&id>0)ids.Add(id);
        var parsed=System.Collections.Frozen.FrozenSet.ToFrozenSet(ids);_dummyCache=new(text,parsed);return parsed;
    }
    public void Normalize()
    {

        if(AdditionalSettings is {} extra)foreach(var key in extra.Keys.Where(k=>k.StartsWith("TargetSkills",StringComparison.OrdinalIgnoreCase)||k.Equals("ShowTargetSkillsOverlay",StringComparison.OrdinalIgnoreCase)||k.Equals("TargetProfileRegion",StringComparison.OrdinalIgnoreCase)||k.Equals("Trackers",StringComparison.OrdinalIgnoreCase)||k.Equals("TrackerVisibility",StringComparison.OrdinalIgnoreCase)||k.Equals("TrackerVolume",StringComparison.OrdinalIgnoreCase)||k.StartsWith("Radar",StringComparison.OrdinalIgnoreCase)||k.StartsWith("ForceMap",StringComparison.OrdinalIgnoreCase)||k.Equals("ShowForceMapOverlay",StringComparison.OrdinalIgnoreCase)||k.Equals("ContinuousSelfPosition",StringComparison.OrdinalIgnoreCase)).ToArray())extra.Remove(key);
        OwnScoreEntries=(OwnScoreEntries??[]).Where(x=>x is not null&&x.Valid).GroupBy(x=>(x.Name,x.ServerId)).Select(g=>g.MaxBy(x=>x.EnteredAt)!).TakeLast(32).ToList();
        PetCounterStyle=PetCounterStyle=="Floating card"?PetCounterStyle:"Beside loot rows";
        LootRegionLeft=double.IsFinite(LootRegionLeft)?Math.Clamp(LootRegionLeft,0,.95):.385;LootRegionTop=double.IsFinite(LootRegionTop)?Math.Clamp(LootRegionTop,0,.95):.28;
        LootRegionWidth=Math.Clamp(double.IsFinite(LootRegionWidth)?LootRegionWidth:.20,.04,1-LootRegionLeft);LootRegionHeight=Math.Clamp(double.IsFinite(LootRegionHeight)?LootRegionHeight:.48,.04,1-LootRegionTop);LootCounterGap=Math.Clamp(LootCounterGap,0,140);
        AccentColor=AppearanceColors.Normalize(AccentColor,AppearanceColors.DefaultAccent);PanelColor=AppearanceColors.Normalize(PanelColor,AppearanceColors.DefaultPanel);LogoColor=AppearanceColors.Normalize(LogoColor,AppearanceColors.DefaultLogo);
        if(PetPickupLeft.HasValue&&!double.IsFinite(PetPickupLeft.Value))PetPickupLeft=null;if(PetPickupTop.HasValue&&!double.IsFinite(PetPickupTop.Value))PetPickupTop=null;
        PinnedPets=(PinnedPets??[]).Where(id=>id is >0 and <=1000000).Distinct().Take(128).ToList();
        PetOverlayScale=double.IsFinite(PetOverlayScale)?Math.Clamp(PetOverlayScale,.8,1.5):1;
        PetOverlayOpacity=double.IsFinite(PetOverlayOpacity)?Math.Clamp(PetOverlayOpacity,.2,1):.96;
        if(PetOverlayLeft.HasValue&&!double.IsFinite(PetOverlayLeft.Value))PetOverlayLeft=null;
        if(PetOverlayTop.HasValue&&!double.IsFinite(PetOverlayTop.Value))PetOverlayTop=null;
        Region = new[]{"Europe","North America","Asia"}.Contains(Region)?Region:"Europe";
        IdleSeconds=Math.Clamp(IdleSeconds,5,600); DummySeconds=Math.Clamp(DummySeconds,0,3600);
        UiScale=double.IsFinite(UiScale)?Math.Clamp(UiScale,0.8,1.5):1;
        OverlayOpacity=double.IsFinite(OverlayOpacity)?Math.Clamp(OverlayOpacity,0.2,1):0.96;
        FadedOpacity=double.IsFinite(FadedOpacity)?Math.Clamp(FadedOpacity,0.1,1):0.45;
        if(EventOverlayScale.HasValue)EventOverlayScale=double.IsFinite(EventOverlayScale.Value)?Math.Clamp(EventOverlayScale.Value,.8,1.5):null;
        if(EventOverlayOpacity.HasValue)EventOverlayOpacity=double.IsFinite(EventOverlayOpacity.Value)?Math.Clamp(EventOverlayOpacity.Value,.2,1):null;
        EventVolume=double.IsFinite(EventVolume)?Math.Clamp(EventVolume,0,1):0.2;
        UpdateMs=Math.Clamp(UpdateMs,100,2000); VisiblePlayers=Math.Clamp(VisiblePlayers,1,12);
        FadeAfterSeconds=Math.Clamp(FadeAfterSeconds,10,600);
        // v0.8 saved broad defaults; they must never reopen nearby-player/all-target capture.
        if(CapturePolicyVersion<1){CaptureScope="Self and party";CapturePolicyVersion=1;}
        CaptureScope=CaptureScope=="Self only"?CaptureScope:"Self and party";
        PartyOnly=true;Targeting="Bosses and dummies";
        DpsCalculation=DpsCalculation=="Active"?DpsCalculation:"Encounter";
        CopyFormat=CopyFormat=="Multi-line"?CopyFormat:"Single line";
        EventVisibility=EventVisibility=="Outside fights"?EventVisibility:"Always";
        GameProcess=string.IsNullOrWhiteSpace(GameProcess)?"AION2":GameProcess.Trim();
        DummyNpcIds??=""; Adapter??=""; Port??="";
        RiftShiftMinutes=Math.Clamp(RiftShiftMinutes,-180,180);
        if(WorldTimerServerId is not (>=1000 and <=9999))WorldTimerServerId=null;
        GameTimerLeadMinutes=string.Join(",",new EventReminder{LeadMinutes=GameTimerLeadMinutes}.Leads());
        if(EventOverlayLeft.HasValue&&!double.IsFinite(EventOverlayLeft.Value))EventOverlayLeft=null;
        if(EventOverlayTop.HasValue&&!double.IsFinite(EventOverlayTop.Value))EventOverlayTop=null;
        if(Left.HasValue&&!double.IsFinite(Left.Value)) Left=null;
        if(Top.HasValue&&!double.IsFinite(Top.Value)) Top=null;
        Reminders=(Reminders??[]).Where(x=>x is not null).Take(30).ToList();
        var reminderIds=new HashSet<Guid>();
        foreach(var x in Reminders) { if(x.Id==Guid.Empty||!reminderIds.Add(x.Id)){x.Id=Guid.NewGuid();reminderIds.Add(x.Id);} x.Name=Clean(x.Name,"Reminder"); x.RepeatMinutes=Math.Clamp(x.RepeatMinutes,0,10080); x.LeadMinutes=string.Join(",",x.Leads()); }
    }
    private static string Clean(string? s,string fallback)=>string.IsNullOrWhiteSpace(s)?fallback:s.Trim()[..Math.Min(60,s.Trim().Length)];
}
public sealed class EventReminder
{
    [JsonExtensionData] public Dictionary<string,JsonElement>? AdditionalSettings {get;set;}
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Reminder";
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now.AddMinutes(30);
    public int RepeatMinutes { get; set; }
    public string LeadMinutes { get; set; } = "5,1";
    public bool Enabled { get; set; } = true;
    public int[] Leads() => (LeadMinutes??"").Split(',',StringSplitOptions.RemoveEmptyEntries)
        .Select(s=>int.TryParse(s.Trim(),out var n)?n:-1).Where(n=>n>=0&&n<=45).Append(0).Distinct().OrderDescending().ToArray();
}
public static class PreferencesStore
{
    private static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
    public static bool TryParse(string json,out MeterPreferences preferences)
    {
        preferences=new();
        if(json.Length>1_048_576)return false;
        try
        {
            using var document=JsonDocument.Parse(json);
            if(document.RootElement.ValueKind!=JsonValueKind.Object)return false;
            var parsed=JsonSerializer.Deserialize<MeterPreferences>(json,Options);
            if(parsed is null)return false;
            parsed.Normalize();preferences=parsed;return true;
        }catch(Exception e)when(e is JsonException or ArgumentException or InvalidOperationException){return false;}
    }
    public static bool TryRead(string path,out MeterPreferences preferences)
    {
        preferences=new();
        try{return new FileInfo(path).Length<=1_048_576&&TryParse(File.ReadAllText(path),out preferences);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){return false;}
    }
    public static MeterPreferences Read(string path)
    {
        if(TryRead(path,out var preferences))return preferences;
        preferences=new();preferences.Normalize();return preferences;
    }
    public static void Write(string path,MeterPreferences preferences)
    {
        preferences.Normalize();AtomicFile.WriteAllText(path,JsonSerializer.Serialize(preferences,Options));
    }
}
