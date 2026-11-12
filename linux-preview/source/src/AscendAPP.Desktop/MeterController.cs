using System.IO;
using AscendAPP.Core.Pets;
using System.Windows;
using System.Windows.Threading;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Storage;
using AscendAPP.Core.Settings;
using System.Text.Json;
using AscendAPP.Desktop.Services;
using AscendAPP.Desktop.ViewModels;
using Microsoft.Win32;

namespace AscendAPP.Desktop;

public sealed class MeterController : IDisposable
{
    private readonly UiModel _vm = new();
    private readonly EncounterHistoryStore? _history;private volatile bool _persistEncounter;private string? _captureFailure;private string _lastHistoryError="";
    internal string HistoryStatus=>_history?.Status??"Preview history is temporary";
    private readonly object _historyGate=new();private readonly HashSet<Guid> _capturedHistory=[];private readonly Queue<Guid> _capturedOrder=[];
    internal IReadOnlyList<EncounterSnapshot> HistoryItems
    {
        get{if(_history is null)return _engine.History.Reverse().ToArray();HashSet<Guid> ids;lock(_historyGate)ids=new(_capturedHistory);return _history.Snapshot().Concat(_engine.History.Where(f=>ids.Contains(f.Id))).GroupBy(f=>f.Id).Select(g=>g.Last()).OrderByDescending(f=>f.StartedAt).Take(EncounterHistoryStore.MaxFights).ToArray();}
    }
    internal Task<bool> FlushHistory()=>_history?.FlushAsync()??Task.FromResult(true);
    private readonly CombatEngine _engine = new();
    private readonly BossForecastTracker _forecast;
    private readonly TargetCatalog _targets;
    private readonly WorldTimerCatalog _worldCatalog;
    private readonly FieldBossTracker _fieldBosses;
    private readonly ReminderScheduler _gameAlertScheduler=new();
    private EventOverlayWindow? _eventOverlay;
    public PetProgressStore Pets {get;}
    private DateTimeOffset _lootPreviewUntil;
    private GameLootReader? _lootReader;private LootCounterWindow? _lootCounter;
    public string LootCounterStatus=>_lootReader?.Status??"Loot reader not active in preview";
    private PetPickupWindow? _petPickup;
    private PetOverlayWindow? _petOverlay;private DateTimeOffset _lastPetSave;private int _petWake;private bool _petsAutoHidden;
    public PetOverlayWindow? PetOverlay=>_petOverlay;
    private Guid? _forecastEncounter;
    private readonly PlayerRatingStore _ratings=new();private PlayerIdentity? _ownIdentity;
    internal bool CaptureRunning=>_npcap.IsRunning;
    internal bool NativePositionLookupAllowed=>false;
    private readonly string _fieldBossPath;
    private readonly UserDataStore _userData;
    private readonly bool _profileOverride;
    public string UserDataDirectory=>_userData.DirectoryPath;
    public string UserDataInfo=>"Settings, saved fights, character tickets and boss snapshots are shared across releases in:\n"+UserDataDirectory+(_userData.ImportedFrom is {} source?"\nImported from: "+source:"")+(_userData.Messages.Any(m=>!m.StartsWith("Imported"))?"\n"+string.Join("\n",_userData.Messages.Where(m=>!m.StartsWith("Imported"))):"");
    private int _fieldBossDirty,_pendingWorldTimerServer;private DateTimeOffset _lastWorldTick,_lastBossSave;private int? _worldServer;private bool _eventsAutoHidden;
    public EventOverlayWindow? EventOverlay=>_eventOverlay;

    private readonly ExperimentalGlobalDecoder _decoder;
    private readonly NetworkPipeline _pipeline;
    internal NetworkPipeline Pipeline=>_pipeline;
    private readonly NpcapCaptureService _npcap;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _demo = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Random _random = new(8273);
    private readonly object _eventLock = new();
    private readonly List<CombatEvent> _events = new();
    public OverlayWindow? Overlay {get;private set;}
    private SettingsWindow? _settings;
    public UiModel Model=>_vm;
    public MeterPreferences Preferences {get;private set;}
    public bool Preview {get;}
    public bool CanClickThrough=>Preview||_desktop?.HasTray==true;
    private readonly CaptureFilter _filter=new();
    private readonly HealingReconciler _healing=new();
    private readonly ReminderScheduler _scheduler=new();
    private readonly Dictionary<string,CharacterTickets> _characters=new();
    private readonly string _preferencesPath;
    private readonly string _charactersPath;
    private DesktopIntegration? _desktop;
    private readonly DispatcherTimer _save=new(){Interval=TimeSpan.FromMilliseconds(400)};
    private string? _policySignature;
    private int _combatWake;
    private readonly List<TimerRow> _timerRows=new();
    private DateTimeOffset _lastDiagnostics;
    private DateTimeOffset _lastEnergy,_lastActivity=DateTimeOffset.UtcNow,_noticeUntil;
    private DateTimeOffset? _dummyStart,_pendingBossEnd;
    private bool _dummyFinished,_disposed,_liveSession;
    private uint? _bossId; private long? _bossHp;
    private bool _ticketClock24=true;
    public event Action? Refreshed;
    public string IntegrationStatus {get;set;}="";
    public string DebugText=>_pipeline.Diagnostics.Summary+"\n"+_vm.Status+"\n"+PartyStatus+"\n"+HealingStatus+"\n"+ProfileStatus+"\n"+$"Own records: {_decoder.OwnScoreRecords}; party scores: {_decoder.PartyScoreRecords}. "+"\n"+HistoryStatus+"\n"+PetStatus+"\n"+LootCounterStatus+"\n"+IntegrationStatus;
    private string PetStatus=>$"Pet wire: {_decoder.PetStateFrames} collection frames, {_decoder.PetGainFrames} gain frames; last body {_decoder.LastPetStateBytes} bytes, {_decoder.PetStateResult}.\nPets: {_decoder.PetStates} collections, {_decoder.PetGains} soul gains, {_decoder.PetRejected} unsupported, {_decoder.PetUnbound} without identity. "+Pets.Status;
    public object PetDiagnostics=>new{Version="0.10.29",_decoder.PetStateFrames,_decoder.PetGainFrames,_decoder.PetStates,_decoder.PetGains,_decoder.PetRejected,_decoder.PetUnbound,_decoder.LastPetStateBytes,_decoder.PetStateResult,CharacterKnown=_pipeline.LiveDiagnostics?.SelfName is not null,Pets.Trusted,Status=Pets.Status,CounterStyle=Preferences.PetCounterStyle,ScreenReader=_lootReader?.Diagnostics};
    public void CopyPetDiagnostics(){try{Clipboard.SetText(JsonSerializer.Serialize(PetDiagnostics,new JsonSerializerOptions{WriteIndented=true}));Notice("Pet diagnostics copied.");}catch(Exception e){Notice("Copy unavailable: "+e.Message);}}
    private string PartyStatus=>$"Party: {_filter.Members(DateTimeOffset.UtcNow).Count} roster members; {_filter.Members(DateTimeOffset.UtcNow).Count(m=>m.ActorId.HasValue)} linked IDs; {_decoder.PartyLists} lists, {_decoder.RejectedPartyLists} rejected. Scope: {Preferences.CaptureScope}";
    private string HealingStatus{get{var h=_healing.Diagnostics;return $"Healing HP estimate: {h.Scored:N0} scored / {h.Raw:N0} raw; {h.Excluded:N0} excluded, {h.Unverified:N0} unverified, {h.Pending} pending";}}
    public sealed record CharacterTickets(string Character,int? ServerId,IReadOnlyList<TicketBalance> Balances,DateTimeOffset Seen);
    private int _demoTick;
    private DateTimeOffset? _replayAt;
    private string _targetName = "Waiting for combat";
    private long _demoBossHp = 80_000_000;


    public MeterController(bool preview=false,UserDataStore? profile=null)
    {
        Preview=preview;_profileOverride=profile is not null;
        _userData=profile??(preview?UserDataStore.Preview():UserDataStore.OpenDefault(AppContext.BaseDirectory));
        _preferencesPath=_userData.SettingsPath;_charactersPath=_userData.CharactersPath;_fieldBossPath=_userData.FieldBossesPath;
        if(!preview||_profileOverride){_history=new(_userData.HistoryPath);_engine.RestoreHistory(_history.Snapshot());}
        _engine.EncounterCompleted+=fight=>{if(!_persistEncounter)return;lock(_historyGate){if(_capturedHistory.Add(fight.Id))_capturedOrder.Enqueue(fight.Id);while(_capturedOrder.Count>160)_capturedHistory.Remove(_capturedOrder.Dequeue());}_history?.Enqueue(fight);};
        Preferences=preview&&!_profileOverride?new():PreferencesStore.Read(_preferencesPath);_vm.Preferences=Preferences;
        _targets = TargetCatalog.Read(Path.Combine(AppContext.BaseDirectory, "data", "targets.json"));
        var timing = BossTimingCatalog.Read(Path.Combine(AppContext.BaseDirectory,"data","boss_berserk.json"));
        _forecast = new(timing); _decoder = new(timing,_targets);
        _worldCatalog=WorldTimerCatalog.Read(Path.Combine(AppContext.BaseDirectory,"data","field_bosses.json"),Path.Combine(AppContext.BaseDirectory,"data","recurring_events.json"));
        _fieldBosses=new(_worldCatalog);if(!preview||_profileOverride){_fieldBosses.Load(_fieldBossPath);_worldServer=Preferences.WorldTimerServerId??_fieldBosses.LastServer;}
        _decoder.FieldBossesUpdated+=ObserveFieldBosses;

        Pets=new((!preview||_profileOverride)?_userData.PetsPath:null,ReadPetNames());
        _decoder.PetCollectionUpdated+=state=>{Pets.Observe(state);WakePets();};
        _decoder.PetSoulsReceived+=gain=>{Pets.Observe(gain);WakePets();};
        _decoder.PetSyncLost+=Pets.Invalidate;
        _pipeline = new NetworkPipeline(_decoder, e=>OnCombat(e));
        _pipeline.CaptureReset+=()=>Pets.Unbind("Capture changed; enter the world to synchronize.");
        _decoder.IdentityUpdated += ObserveIdentity;_decoder.RatingUpdated+=_ratings.Observe;_decoder.EntityRemoved+=_ratings.Forget;_decoder.ConnectionChanged+=()=>{_ratings.ClearWorld();_ownIdentity=null;};
        _decoder.EntityRemoved+=id=>{if(_ownIdentity?.Id==id)_ownIdentity=null;_filter.Forget(id);_forecast.Forget(id);_healing.Forget(id);};
        _decoder.TargetDamaged+=_healing.Damage;
        _decoder.CaptureContinuityLost+=_healing.Reset;
        _decoder.HealthUpdated += hp => { _healing.Observe(hp); _engine.UpdateHealth(hp.Id,hp.Current,hp.Maximum); _forecast.Health(hp.Id,hp.Current,hp.Maximum,hp.ObservedAt ?? DateTimeOffset.UtcNow); ObserveBossHealth(hp); };
        _decoder.PartyUpdated += ObserveParty;
        _decoder.EngagementUpdated += e=>{_forecast.Engage(e);if(Preferences.BossResetDetection&&!e.InCombat&&e.Id==_bossId)_pendingBossEnd=DateTimeOffset.UtcNow.AddMilliseconds(500);};
        _decoder.ConnectionChanged += () => { _forecast.Reset(); _filter.Reset();_healing.Reset(); _engine.Reset(DateTimeOffset.UtcNow); _bossId=null; _bossHp=null; _pendingBossEnd=null; _targetName="Waiting for combat"; _dummyStart=null; _dummyFinished=false; lock (_eventLock) _events.Clear(); };
        _pipeline.Initialize();
        _npcap = new NpcapCaptureService(_pipeline);
        if(!preview)_lootReader=new(ReadPetNames(),Pets.Progress,()=>Pets.RecentPickups(DateTimeOffset.UtcNow),frame=>_refresh.Dispatcher.BeginInvoke(new Action(()=>{if(_disposed)return;if(DateTimeOffset.UtcNow<_lootPreviewUntil&&(frame is null||frame.Rows.Count==0))return;if(frame?.Rows.Count>0)_lootPreviewUntil=default;if(frame is null||frame.Rows.Count==0||!Preferences.ShowPetPickups||Preferences.PetCounterStyle!="Beside loot rows")_lootCounter?.Hide();else{_lootCounter??=new();_lootCounter.Apply(frame);}})));
        _refresh.Tick += (_, _) => RefreshScreen();
        _demo.Tick += (_, _) => EmitDemo();
        _save.Tick+=(_,_)=>{_save.Stop();Save();};
        if(!preview||_profileOverride)ReadCharacters();
        if(!preview){RefreshDevices();_refresh.Start();}
        ApplyPreferences();
        if(!preview&&_userData.Messages.Count>0)Notice(_userData.ImportedFrom is not null?"Imported your earlier settings. Future versions will use the shared profile.":_userData.Messages[0]);
    }

    internal void ObserveIdentity(PlayerIdentity id){_ratings.Identity(id);if(id.IsSelf){_ownIdentity=id;if(id.ServerId is {} petServer)Pets.Bind(id.Name,petServer);}else if(_ownIdentity?.Id==id.Id)_ownIdentity=null;_engine.UpdateIdentity(id.Id,id.Name,id.ClassName,id.IsSelf);_forecast.Identity(id);_filter.Identity(id);if(id.IsSelf&&id.ServerId is >=1000 and <=9999)System.Threading.Interlocked.Exchange(ref _pendingWorldTimerServer,id.ServerId.Value);}
    internal void ObserveParty(PartyRoster roster){_forecast.Party(roster);_filter.Party(roster);}
    internal void ObserveHealth(TargetHealth hp){_healing.Observe(hp);}
    internal void OnCombat(CombatEvent evt,bool capture=true)
    {
        var dummy=Preferences.DummyIds();
        if(evt.TargetNpcCode is {} npc)
        {
            bool isDummy=_targets.IsTrainingDummy(npc)||dummy.Contains(npc);
            string name=_targets.NameOf(npc)??(isDummy?"Training Dummy":evt.TargetName);
            bool dummyTarget=isDummy||evt.TargetIsTrainingDummy,bossTarget=!isDummy&&(evt.TargetIsBoss||_targets.IsBoss(npc));
            if(name!=evt.TargetName||dummyTarget!=evt.TargetIsTrainingDummy||bossTarget!=evt.TargetIsBoss)
                evt=evt with{TargetName=name,TargetIsTrainingDummy=dummyTarget,TargetIsBoss=bossTarget};
        }
        if(capture&&_dummyFinished)return;
        if(capture)
        {
            bool admitted=_filter.Accept(evt,Preferences.CaptureScope,dummy,Preferences.IdleSeconds,Preferences.BossResetDetection);
            if(evt.Kind==CombatKind.Heal){if(admitted)_persistEncounter=true;_healing.Add(evt,admitted);return;}
            if(!admitted)return;
        }
        if(evt.Kind==CombatKind.Damage&&evt.TargetIsBoss)_bossId=evt.TargetId;
        if(capture&&evt.TargetIsTrainingDummy&&evt.Kind==CombatKind.Damage)
        {
            _dummyStart??=evt.Time;
            if(Preferences.DummySeconds>0&&evt.Time-_dummyStart>=TimeSpan.FromSeconds(Preferences.DummySeconds)){_engine.Finish(_dummyStart.Value.AddSeconds(Preferences.DummySeconds));_filter.EndEncounter();_healing.ClearPending();_dummyFinished=true;return;}
        }
        if(evt.ScoredAmount>0)
        {
            _lastActivity=DateTimeOffset.UtcNow;
        }
        if(capture)_persistEncounter=true;
        _engine.Add(evt);
        var encounter=_engine.EncounterId;if(_forecastEncounter is {} previous&&previous!=encounter)_forecast.ClearDamage();_forecastEncounter=encounter;
        _forecast.Add(evt,capture);
        if (evt.Kind == CombatKind.Damage && !string.IsNullOrWhiteSpace(evt.TargetName))
            _targetName = evt.TargetName;
        lock (_eventLock)
        {
            if (_events.Count < 200_000) _events.Add(evt);
        }
        // Publish the first paint only after its combat/forecast state is committed.
        if(evt.ScoredAmount>0&&!Preview&&System.Threading.Interlocked.Exchange(ref _combatWake,1)==0)
            _refresh.Dispatcher.BeginInvoke(new Action(()=>{if(!_disposed){_refresh.Interval=TimeSpan.FromMilliseconds(Preferences.UpdateMs);RefreshScreen();}}),DispatcherPriority.Background);
    }

    private void RefreshDevices()
    {
        try
        {
            _vm.Devices.Clear();
            foreach (var device in NpcapCaptureService.Devices()) _vm.Devices.Add(device);
            _vm.SelectedDevice = _vm.Devices.FirstOrDefault(x=>x.Device.Name==Preferences.Adapter)??_vm.Devices.FirstOrDefault();
            _vm.Port=Preferences.Port; _vm.AutoAdapters=Preferences.AutoAdapters;
            _captureFailure=_vm.Devices.Count==0?"No Npcap interfaces found. Open Settings → General to check capture.":null;
            _vm.Status = _vm.Devices.Count == 0
                ? "No Npcap interfaces found. Please install Npcap."
                : $"Found {_vm.Devices.Count} interfaces. Ready for capture.";
        }
        catch (Exception e)
        {
            _vm.Status = "Npcap unavailable: " + e.Message;_captureFailure=_vm.Status;
        }
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDevice is not { } choice)
        {
            _captureFailure="Npcap is unavailable. Open Settings → General to check capture.";RefreshCaptureHealth();Notice(_captureFailure);
            return;
        }
        int? port = null;
        if (!string.IsNullOrWhiteSpace(_vm.Port))
        {
            if (!int.TryParse(_vm.Port, out int n) || n is < 1 or > 65535)
            {
                Notice("TCP port must be 1–65535 or blank.");
                return;
            }
            port = n;
        }
        try
        {
            _captureFailure=null;_demo.Stop();
            _npcap.Stop();
            _replayAt = null;
            _engine.Reset(DateTimeOffset.UtcNow);
            lock (_eventLock) _events.Clear();
            _pipeline.ResetStreams();
            _forecast.Reset();
            _targetName = "Waiting for combat";
            Preferences.AutoAdapters=_vm.AutoAdapters; Preferences.Port=_vm.Port; Preferences.Adapter=choice.Device.Name;
            _filter.Reset();_healing.Reset(); _dummyStart=null;_dummyFinished=false;_bossId=null;_pendingBossEnd=null;
            _npcap.Start(choice, port, _vm.AutoAdapters);
            _liveSession=true;
            _vm.Mode = "LIVE • EXPERIMENTAL";
            _vm.Status = port is null
                ? "Npcap active. Inspecting TCP traffic in memory only. Waiting for game connection. Experimental decoder."
                : $"Npcap active on port {port}. Experimental decoder; waiting for game connection.";
        }
        catch (Exception ex)
        {
            _vm.Mode = "OFFLINE";
            _vm.Status = "Failed to start Npcap: " + ex.Message;_captureFailure=_vm.Status;
            Notice("Could not start capture: "+ex.Message);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _npcap.Stop();Pets.Invalidate("Capture stopped; totals are last observed.");_engine.FinishAtLastActivity(interrupted:true);
        _demo.Stop();
        _replayAt ??= DateTimeOffset.UtcNow;
        _vm.Mode = "OFFLINE";
        _captureFailure=null;_vm.Status = "Capture and demo stopped.";RefreshScreen();
    }

    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        _npcap.Stop();
        _pipeline.ResetStreams();
        _demo.Stop();
        _engine.Reset(DateTimeOffset.UtcNow);
        _filter.Reset();_healing.Reset();_dummyStart=null;_dummyFinished=false;_pendingBossEnd=null;_bossId=null;
        _forecast.Reset();
        lock (_eventLock) _events.Clear();
        _persistEncounter=false;_liveSession=false;_filter.Reset();_healing.Reset();
        _demoTick = 0;
        _demoBossHp = 80_000_000;
        _replayAt = null;
        _targetName = "Elyos Training Scarecrow";
        _vm.Mode = "DEMO";
        _vm.ApplyEnergy(new OdyleEnergy(75,520,DateTimeOffset.UtcNow),demo:true);
        _forecast.Engage(new BossEngagement(9999,true,false,DateTimeOffset.UtcNow));
        _vm.Status = "Demo mode: synthetic combat events, not live game data.";
        _demo.Start();
        EmitDemo();
    }

    private void Showcase_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", "elyos-showcase.jsonl");
        try
        {
            _npcap.Stop(); _demo.Stop();
            var events = JsonlCombatSession.Read(path);
            _engine.Reset(DateTimeOffset.UtcNow);
            _pipeline.ResetStreams();
            _forecast.Reset();_dummyStart=null;_dummyFinished=false;_pendingBossEnd=null;_bossId=null;_bossHp=null;
            lock (_eventLock) _events.Clear();
            _persistEncounter=false;_liveSession=false;_filter.Reset();_healing.Reset();
            foreach (var evt in events) OnCombat(evt,false);
            _replayAt = events[^1].Time;
            _vm.Mode = "SHOWCASE · DEMO";
            _vm.ApplyEnergy(null);
            _vm.Status = "Loaded a synthetic 9-player Elyos showcase. These are NOT actual packets.";
            RefreshScreen();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Showcase load failed"); }
    }

    private void EmitDemo()
    {
        string[] players = ["MyCharacter", "Sylvaen", "Borgrim", "Nyxara", "Talwyn", "Aegisa", "Morwen", "Grohm", "Celesta"];
        string[] classes = ["Ranger", "Sorcerer", "Templar", "Assassin", "Gladiator", "Cleric", "Chanter", "Elementalist", "Brawler"];
        string[] skills = ["Radiant Strike", "Aether Bolt", "Shield Break", "Shadow Cut"];

        var now = DateTimeOffset.UtcNow;
        _demoTick++;
        for (uint i = 0; i < players.Length; i++)
        {
            if (_random.NextDouble() < (i == 0 ? 0.95 : 0.75))
            {
                var damage = _random.Next(10_000, 42_000) * (i == 0 ? 2 : 1);
                var crit = _random.NextDouble() < 0.24;
                _demoBossHp = Math.Max(0, _demoBossHp - damage);
                int skill = _random.Next(0, 4);
                OnCombat(new CombatEvent(now, 100 + i, players[i], 9999, "Elyos Training Scarecrow",
                    1000 + (int)i * 10 + skill, damage, CombatKind.Damage,
                    crit ? HitTags.Critical : HitTags.None,
                    SkillName: skills[skill], ActorClass: classes[i], IsSelf: i == 0,
                    TargetIsBoss: true, TargetNpcCode: 9999,
                    TargetCurrentHp: _demoBossHp, TargetMaxHp: 80_000_000),false);
            }
            if (i == 3 && _demoTick % 3 == 0)
                OnCombat(new CombatEvent(now, 100 + i, players[i], 100, "MyCharacter", 7700,
                    18_000, CombatKind.Heal, SkillName: "Restoration", ActorClass: classes[i]),false);
        }
    }

    public void RefreshScreen()=>RefreshScreenAt(DateTimeOffset.UtcNow);
    internal TimeSpan RefreshInterval=>_refresh.Interval;
    internal void RefreshScreenAt(DateTimeOffset wall)
    {
        var now=_replayAt??wall;
        if(_pendingBossEnd is {} end&&wall>=end){_engine.Finish(now);_filter.EndEncounter();_healing.ClearPending();_pendingBossEnd=null;_bossId=null;}
        if(!_dummyFinished&&_dummyStart is {} start&&Preferences.DummySeconds>0&&now-start>=TimeSpan.FromSeconds(Preferences.DummySeconds)){_engine.Finish(start.AddSeconds(Preferences.DummySeconds));_filter.EndEncounter();_healing.ClearPending();_dummyFinished=true;Notice("Timed dummy parse complete. Reset to start another.");}
        foreach(var heal in _healing.Drain(wall))OnCombat(heal,false);
        var snapshot = _engine.Snapshot(_replayAt ?? wall,includeDetails:false);
        if(!snapshot.IsActive){_filter.EndEncounter();_healing.ClearPending();System.Threading.Interlocked.Exchange(ref _combatWake,0);}
        _vm.SetParty(_filter.Members(now),Preferences.CaptureScope);
        _vm.Apply(snapshot);_vm.ApplyRatings(row=>FindRowRating(row,now),now);RefreshOwnRating(now);
        _vm.ApplyForecast(ForecastSnapshot(now,snapshot));
        if (_npcap.IsRunning)
        {
            var live=_pipeline.LiveDiagnostics;_vm.ApplyEnergy(live?.Energy);
            _vm.IdentityRefreshPending=live?.IdentityRefreshPending==true;_vm.SelfIdentified=live?.SelfName is not null;
            _vm.SelfName=live?.SelfName??(_vm.IdentityRefreshPending?"Refreshing character…":"Detecting character");_vm.SelfClass=live?.ClassName;
            if(live?.SelfName is {} name&&live.Tickets is {Count:>0} balances&&balances.Max(x=>x.ObservedAt)>_lastEnergy)
            {
                _lastEnergy=balances.Max(x=>x.ObservedAt);_characters[$"{live.ServerId}:{name}"]=new(name,live.ServerId,balances,_lastEnergy);
                while(_characters.Count>50)_characters.Remove(_characters.MinBy(x=>x.Value.Seen).Key);
                RefreshTickets(); QueueSave();
            }
        }
        if (string.IsNullOrWhiteSpace(snapshot.Boss?.Name) && string.IsNullOrWhiteSpace(snapshot.TargetName))
            _vm.EncounterName = _targetName;
        _vm.Traffic = _pipeline.Segments.ToString("N0");
        _vm.Frames = _pipeline.Frames.ToString("N0");
        _vm.Decoded = _pipeline.Decoded.ToString("N0");
        if (_npcap.IsRunning&&wall-_lastDiagnostics>=TimeSpan.FromSeconds(1))
        {
            _lastDiagnostics=wall;
            _vm.Status = _pipeline.Diagnostics.Summary + " | " +
                (_pipeline.LiveDiagnostics is { GameStream: true } live
                    ? $"Game stream found; player {live.SelfName ?? "unknown"}; {live.IdentityState}; zone {live.ZoneId?.ToString()??"unknown"}, refreshes {live.ZoneRefreshes}, own records {live.CharacterRecords}; internal moves {live.InternalMoves}, party carries {live.RetainedParties}, verified rebinds {live.ProfileRebindings}; map lists {live.FieldBossLists}, unbound {live.UnboundFieldBossLists}; known messages {live.Recognized}, rejected {live.Rejected}"
                    : "Looking for game traffic — enter the world or change zone") +
                $" | {PartyStatus} | {HealingStatus} | Filter: {_filter.RejectedActors} outside self/party, {_filter.RejectedTargets} outside eligible encounter. Adapters {_npcap.ActiveDevices}, unavailable {_npcap.FailedDevices}. Experimental.";
        }
        RefreshCaptureHealth(wall,snapshot);
        _vm.HeaderState=_vm.Mode.Contains("DEMO")?"Demo":_vm.Mode=="REPLAY"?"Replay":snapshot.IsActive?"In combat":_npcap.IsRunning?"Idle":"Stopped";
        if(_vm.Mode.Contains("DEMO")){_vm.IdentityRefreshPending=false;_vm.SelfIdentified=true;_vm.SelfName="MyCharacter · demo";_vm.SelfClass="Ranger";}
        UpdateTimers();
        var interval=TimeSpan.FromMilliseconds(snapshot.IsActive?Preferences.UpdateMs:Math.Max(1000,Preferences.UpdateMs));
        if(_refresh.Interval!=interval)_refresh.Interval=interval;
        if(wall>_noticeUntil)_vm.Notice="";
        var historyError=_history?.LastError??"";if(historyError!=_lastHistoryError){_lastHistoryError=historyError;if(historyError.Length>0)Notice(historyError);}
        _desktop?.Tick(); Overlay?.ApplyPreferences(); RefreshWorldTimers();_eventOverlay?.ApplyPreferences();FlushFieldBosses();if(_petOverlay?.IsVisible==true){_petOverlay.Refresh();_petOverlay.ApplyPreferences();}RefreshPetPickup();if(wall-_lastPetSave>=TimeSpan.FromSeconds(5)){_lastPetSave=wall;Pets.Save();} Refreshed?.Invoke();
    }

    private PlayerRating? FindRowRating(PlayerRow row,DateTimeOffset at)
    {
        var value=_ratings.Find(row.ActorId,row.Name,at)??(!row.HasCombat&&row.ServerId is {} server?_ratings.FindRoster(row.Name,server,at):null);
        return value;
    }
    internal IReadOnlyList<object> ProfileRows=>_ratings.Snapshot(DateTimeOffset.UtcNow).Select(p=>(object)new{Name=Preferences.HideNames?"Hidden player":p.Name,Server=p.ServerId?.ToString()??"Unknown",GS=UiModel.ScoreText(p),CP=UiModel.PowerText(p),Source=p.Source}).ToArray();
    internal string ProfileStatus=>$"Observed nearby profiles: {_decoder.FullPlayerProfiles}; GS: {_decoder.ProfileScores}; CP: {_decoder.ProfilePowers}; matched inspect GS: {_decoder.InspectScores}. Current party: {_decoder.PartyMembers}. Own GS/CP: {(_ownIdentity is {} own&&_ratings.Find(own.Id,own.Name,DateTimeOffset.UtcNow)is {}?"observed":"not observed")}. Own login: {_decoder.OwnGearScores} GS / {_decoder.OwnCombatPowers} CP. Party roster: {_decoder.PartyGearScores} GS / {_decoder.PartyCombatPowers} CP. {_decoder.OwnScoreDetail}.";
    private void RefreshOwnRating(DateTimeOffset at){var own=_ownIdentity;_vm.ApplyOwnRating(own is null?null:_ratings.Find(own.Id,own.Name,at),at);if(own is not null)_vm.SelfClass=own.ClassName;}
    internal string CharacterProfileLabel=>_ownIdentity is {} p?$"{p.Name} · Server {p.ServerId?.ToString()??"unknown"} · {p.ClassName??"Unknown class"}":"Waiting for verified own character";
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _engine.Reset(DateTimeOffset.UtcNow);
        _forecast.ResetEncounter();_filter.EndEncounter();_healing.ClearPending();_bossId=null;_bossHp=null;_pendingBossEnd=null;
        _dummyStart=null;_dummyFinished=false;
        _replayAt = null;
        _targetName = "Waiting for combat";
        lock (_eventLock) _events.Clear();
        RefreshScreen();
    }

    private void Devices_Click(object sender, RoutedEventArgs e)
    {
        if (_npcap.IsRunning) _npcap.Stop();
        RefreshDevices();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Combat events (*.jsonl)|*.jsonl" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _npcap.Stop(); _demo.Stop();
            var events = JsonlCombatSession.Read(dialog.FileName); // parse before reset
            _engine.Reset(DateTimeOffset.UtcNow);
            _pipeline.ResetStreams();
            _forecast.Reset();_dummyStart=null;_dummyFinished=false;_pendingBossEnd=null;_bossId=null;_bossHp=null;
            lock (_eventLock) _events.Clear();
            _persistEncounter=false;_liveSession=false;_filter.Reset();_healing.Reset();
            foreach (var evt in events) OnCombat(evt,false);
            _replayAt = events.Count > 0 ? events[^1].Time : DateTimeOffset.UtcNow;
            _vm.Mode = "REPLAY";
            _vm.IdentityRefreshPending=false;_vm.SelfIdentified=events.Any(x=>x.IsSelf);
            _vm.ApplyEnergy(null);
            _vm.Status = $"Loaded {events.Count:N0} events. Note: JSONL contains readable combat logs.";
            // Snapshot at last event time, not current day: imported fixtures can be old.
            _vm.Apply(_engine.Snapshot(_replayAt.Value));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Import error"); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Combat events (*.jsonl)|*.jsonl", FileName = "foometer-session.jsonl" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            CombatEvent[] items;
            lock (_eventLock) items = _events.ToArray();
            JsonlCombatSession.Write(dialog.FileName, items);
            _vm.Status = $"Exported {items.Length:N0} events to JSONL.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export error"); }
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Combat report (*.json)|*.json", FileName = "foometer-report.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var reportAt=_replayAt??DateTimeOffset.UtcNow;var reportCombat=_engine.Snapshot(reportAt);
            File.WriteAllText(dialog.FileName, System.Text.Json.JsonSerializer.Serialize(new
            {
                Decoder = _pipeline.DecoderName,
                LiveDecoderVerified = false,
                Region=Preferences.Region,
                DpsCalculation=Preferences.DpsCalculation,
                CaptureScope=Preferences.CaptureScope,
                TargetRule="Bosses and training dummies only",
                Filter=new{_filter.RejectedActors,_filter.RejectedTargets},
                Party=new{_filter.RosterObservedAt,Members=_filter.Members(DateTimeOffset.UtcNow),_decoder.PartyLists,_decoder.RejectedPartyLists},
                Healing=new{Method="HP-based estimate; ambiguous/missing HP unscored",Totals=_healing.Diagnostics},
                Capture = _pipeline.Diagnostics,
                Protocol = _pipeline.LiveDiagnostics,
                PlayerProfiles=new{_decoder.OwnScoreRecords,_decoder.OwnGearScores,_decoder.OwnCombatPowers,_decoder.OwnScoreDetail,_decoder.PartyScoreRecords,_decoder.PartyGearScores,_decoder.PartyCombatPowers,_decoder.FullPlayerProfiles,_decoder.ProfileScores,_decoder.ProfilePowers,_decoder.InspectScores,Status=ProfileStatus,OwnGs=_vm.SelfGearScore,OwnCp=_vm.SelfCombatPower,VisibleRows=_vm.Rows.Select(r=>new{r.Name,r.ActorId,r.ServerId,r.IsSelf,r.GearScore,r.CombatPower,r.RatingInfo})},
                WorldTimers=new{ServerId=_worldServer,Schedule="UTC estimates",Preferences.RiftShiftMinutes,FieldBosses=_fieldBosses.Snapshot(_worldServer)},
                Pets=PetDiagnostics,
                Forecast = ForecastSnapshot(reportAt,reportCombat),
                Combat = reportCombat
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            _vm.Status = "Combat and capture diagnostics report saved (no packet bodies or endpoints).";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export error"); }
    }

    private void Csv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Comma separated values (*.csv)|*.csv", FileName = "foometer-ranking.csv" };
        if (dialog.ShowDialog() != true) return;
        try { CombatCsv.Save(dialog.FileName, _engine.Snapshot(_replayAt ?? DateTimeOffset.UtcNow));
            _vm.Status = "Saved local CSV ranking."; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export error"); }
    }

    internal BossForecast ForecastSnapshot(DateTimeOffset at,EncounterSnapshot? snapshot=null){snapshot??=_engine.Snapshot(at,includeDetails:false);return _forecast.Snapshot(at,_liveSession?Preferences.CaptureScope:null,snapshot.IsActive?null:snapshot.StartedAt.AddSeconds(snapshot.DurationSeconds));}
    internal EncounterSnapshot AnalysisSnapshot()=>_engine.Snapshot(_replayAt??DateTimeOffset.UtcNow);
    public void ShowPlayer(uint actorId){var snapshot=AnalysisSnapshot();if(snapshot?.Players.Any(p=>p.ActorId==actorId)==true)new SkillBreakdownWindow(snapshot,healing:_vm.ShowHealing,hideNames:Preferences.HideNames,selectedActor:actorId){Owner=Overlay}.Show();}
    public void ShowSkills() => Details_Click(this,new RoutedEventArgs());
    public void ResetEncounter() => Reset_Click(this,new RoutedEventArgs());

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _engine.Snapshot(_replayAt ?? DateTimeOffset.UtcNow);
        if (snapshot.Players.Count == 0) { _vm.Status = "No combat data to analyse."; return; }
        new SkillBreakdownWindow(snapshot,healing:_vm.ShowHealing,hideNames:Preferences.HideNames) { Owner = Overlay }.Show();
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var items = HistoryItems;
        new FightHistoryWindow(items,Preferences.Clock24,Preferences.HideNames,DeleteHistory,ClearSavedHistory,HistoryStatus) { Owner = Overlay }.Show();
    }

    public OverlayWindow CreateOverlay()
    {
        Overlay=new OverlayWindow(_vm,this);
        Overlay.ContentRendered+=(_,_)=>{SyncEventOverlay();SyncPetOverlay();};
        Overlay.SourceInitialized+=(_,_)=>{if(!Preview){_desktop=new(Overlay,this);_desktop.Apply();}};
        return Overlay;
    }
    public void ShowSettings(string page="General")
    {
        if(_settings is null){_settings=new(this);_settings.Closed+=(_,_)=>_settings=null;}
        _settings.SelectPage(page);_settings.Show();_settings.Activate();
    }
    public void ApplyPreferences()
    {
        Preferences.Normalize();AppearanceTheme.Apply(Preferences);_vm.Preferences=Preferences;
        _lootReader?.Configure(new(Preferences.ShowPetPickups&&Preferences.PetCounterStyle=="Beside loot rows",Preferences.GameProcess,Preferences.LootRegionLeft,Preferences.LootRegionTop,Preferences.LootRegionWidth,Preferences.LootRegionHeight,Preferences.LootCounterGap));
        if(!Preferences.ShowPetPickups||Preferences.PetCounterStyle!="Beside loot rows")_lootCounter?.Hide();
        string policy=$"{Preferences.CaptureScope}:{Preferences.DummyNpcIds}:{Preferences.DummySeconds}";
        if(_policySignature is not null&&policy!=_policySignature){_engine.Reset(DateTimeOffset.UtcNow);_forecast.ResetEncounter();_filter.EndEncounter();_healing.ClearPending();_bossId=null;_bossHp=null;_pendingBossEnd=null;_targetName="Waiting for combat";_dummyStart=null;_dummyFinished=false;lock(_eventLock)_events.Clear();Notice("Capture filter changed; a new encounter starts with the next hit.");}
        _policySignature=policy;
        _engine.IdleTimeoutSeconds=Preferences.IdleSeconds;_engine.KeepBossOpen=Preferences.BossResetDetection;_engine.ActiveDps=Preferences.DpsCalculation=="Active";
        if(_ticketClock24!=Preferences.Clock24){_ticketClock24=Preferences.Clock24;RefreshTickets();}
        _refresh.Interval=TimeSpan.FromMilliseconds(Preferences.UpdateMs);_desktop?.Apply();
        _vm.SetParty(_filter.Members(DateTimeOffset.UtcNow),Preferences.CaptureScope);_vm.Apply(_engine.Snapshot(_replayAt??DateTimeOffset.UtcNow,includeDetails:false));_vm.ApplyRatings(row=>FindRowRating(row,_replayAt??DateTimeOffset.UtcNow),_replayAt??DateTimeOffset.UtcNow);RefreshOwnRating(_replayAt??DateTimeOffset.UtcNow);Overlay?.ApplyPreferences();RefreshWorldTimers(force:true);SyncEventOverlay();SyncPetOverlay();_petPickup?.ApplyPreferences();QueueSave();
    }
    public void OpenUserDataFolder()
    {
        if(Preview){Notice("Preview mode does not open or change the shared profile.");return;}
        try
        {
            Directory.CreateDirectory(UserDataDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName=UserDataDirectory,UseShellExecute=true});
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){Notice("Could not open saved data: "+e.Message);}
    }
    public void ImportSettings()
    {
        if(Preview&&!_profileOverride){Notice("Preview mode does not change the shared profile.");return;}
        var dialog=new OpenFileDialog{Title="Import settings from an older AscendAPP folder",Filter="AscendAPP settings (settings.json)|settings.json|JSON files (*.json)|*.json"};
        if(dialog.ShowDialog()!=true)return;
        try{Notice(ImportSettingsFile(dialog.FileName));}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException){Notice("Could not import settings: "+e.Message);}
    }
    internal string ImportSettingsFile(string path)
    {
        if(Preview&&!_profileOverride)throw new InvalidOperationException("Preview mode cannot write the real profile.");
        string message=_userData.ImportSettings(path);
        Preferences=PreferencesStore.Read(_preferencesPath);_vm.AutoAdapters=Preferences.AutoAdapters;_vm.Port=Preferences.Port;
        _vm.SelectedDevice=_vm.Devices.FirstOrDefault(d=>d.Device.Name==Preferences.Adapter)??_vm.Devices.FirstOrDefault();
        ApplyPreferences();RefreshScreen();return message;
    }
    public void QueueSave(){if(!Preview&&!_disposed){_save.Stop();_save.Start();}}
    private void Save()
    {
        if(Preview)return;
        try{PreferencesStore.Write(_preferencesPath,Preferences);AtomicFile.WriteAllText(_charactersPath,JsonSerializer.Serialize(_characters.Values.ToArray(),new JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){Notice("Could not save preferences: "+e.Message);}
    }
    private void ReadCharacters()
    {
        try{if(new FileInfo(_charactersPath).Length>4_000_000)return;foreach(var c in (JsonSerializer.Deserialize<CharacterTickets[]>(File.ReadAllText(_charactersPath))??[]).TakeLast(50))if(c is not null&&!string.IsNullOrWhiteSpace(c.Character)&&c.Character.Length<=100&&c.Balances is not null){var valid=c.Balances.Where(b=>b is not null&&b.Regular is >=0 and <=10000000&&b.Bonus is >=0 and <=10000000).Take(255).ToArray();_characters[$"{c.ServerId}:{c.Character}"]=c with{Balances=valid};}RefreshTickets();}
        catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){}
    }
    private static readonly Dictionary<uint,string> TicketNames=ReadTicketNames();
    private static Dictionary<uint,string> ReadTicketNames(){try{using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"data","ticket_names.json")));return doc.RootElement.GetProperty("names").EnumerateObject().ToDictionary(x=>uint.Parse(x.Name),x=>x.Value.GetString()??x.Name);}catch{return new();}}
    private void RefreshTickets()
    {
        _vm.Tickets.Clear();
        foreach(var c in _characters.Values.OrderByDescending(c=>c.Seen))
            foreach(var b in c.Balances.Where(b=>TicketNames.ContainsKey(b.Id)).OrderBy(b=>b.Id==60000001?0:1).ThenBy(b=>b.Id))
                _vm.Tickets.Add(new(c.Character+(c.ServerId is {} s?$" · {s}":""),b.Available.ToString("N0"),TicketNames[b.Id]+$" · {b.Regular:N0} + {b.Bonus:N0}",b.ObservedAt.ToLocalTime().ToString(Preferences.Clock24?"g":"M/d/yyyy h:mm tt")));
    }
    public void ForgetTickets(){_characters.Clear();RefreshTickets();QueueSave();}
    private void ObserveBossHealth(TargetHealth hp)
    {
        if(hp.Id!=_bossId)return;
        if(Preferences.BossResetDetection&&(hp.Current==0||hp.Current==hp.Maximum&&_bossHp is >0&&hp.Current>_bossHp))_pendingBossEnd=DateTimeOffset.UtcNow.AddMilliseconds(500);
        _bossHp=hp.Current;
    }
    private void UpdateTimers()
    {
        var now=DateTimeOffset.UtcNow;
        if(!Preview)foreach(var alert in _scheduler.Poll(Preferences.Reminders,now)){Notice(alert.LeadMinutes==0?alert.Name+" starts now":alert.Name+$" in {alert.LeadMinutes} min");AlertSound.Play(Preferences.EventVolume);_desktop?.Notify(alert.Name,alert.LeadMinutes==0?"Starts now":$"In {alert.LeadMinutes} minutes");}
        _timerRows.Clear();
        bool fighting=_vm.Latest?.IsActive==true;
        if(Preferences.EventVisibility=="Always"||!fighting)foreach(var r in Preferences.Reminders.Where(r=>r.Enabled))if(ReminderScheduler.Next(r,now) is {} due)_timerRows.Add(new(r.Name,due.ToLocalTime().ToString(Preferences.Clock24?"HH:mm":"h:mm tt"),0,"Bell"));
        SyncTimers();
    }
    private void SyncTimers()
    {
        for(int i=0;i<_timerRows.Count;i++)
        {
            var source=_timerRows[i];
            if(i>=_vm.Timers.Count){_vm.Timers.Add(source);continue;}
            var target=_vm.Timers[i];target.Name=source.Name;target.Remaining=source.Remaining;target.Fraction=source.Fraction;target.Icon=source.Icon;
        }
        while(_vm.Timers.Count>_timerRows.Count)_vm.Timers.RemoveAt(_vm.Timers.Count-1);
    }
    public void Notice(string text){_vm.Notice=text;_noticeUntil=DateTimeOffset.UtcNow.AddSeconds(8);}
    public double VisualOpacity(bool mouseOver)=>!mouseOver&&Preferences.FadeIdle&&_vm.Latest?.IsActive!=true&&DateTimeOffset.UtcNow-_lastActivity>TimeSpan.FromSeconds(Preferences.FadeAfterSeconds)?Math.Min(Preferences.OverlayOpacity,Preferences.FadedOpacity):Preferences.OverlayOpacity;
    public void Touch()=>_lastActivity=DateTimeOffset.UtcNow;
    public void StartCapture()=>Capture_Click(this,new());
    public void Stop()=>Stop_Click(this,new());
    public void Demo()=>Demo_Click(this,new());
    public void Showcase()=>Showcase_Click(this,new());
    public void RefreshAdapters()=>Devices_Click(this,new());
    public void Import()=>Import_Click(this,new());
    public void ExportJson()=>Export_Click(this,new());
    public void ExportCsv()=>Csv_Click(this,new());
    public void ExportReport()=>Report_Click(this,new());
    public void History()=>History_Click(this,new());
    public void ClearMeter(){_engine.Reset(DateTimeOffset.UtcNow);_forecast.ResetEncounter();_filter.EndEncounter();_healing.ClearPending();_bossId=null;_bossHp=null;_pendingBossEnd=null;_targetName="Waiting for combat";lock(_eventLock)_events.Clear();_dummyStart=null;_dummyFinished=false;RefreshScreen();}
    public void CopySummary()
    {
        var snapshot=_vm.Latest;if(snapshot is null||snapshot.Players.Count==0){Notice("No combat result to copy yet.");return;}
        string Label(PlayerSnapshot p)=>Preferences.DpsCalculation=="Active"?"aDPS":"eDPS";
        var lines=_vm.ShowHealing?snapshot.Players.OrderByDescending(p=>p.Healing).Select(p=>$"{p.Name}: {p.Hps(snapshot.DurationSeconds):N0} eHPS / {p.Healing:N0} healing"):snapshot.Players.Select(p=>$"{p.Name}: {p.Dps:N0} {Label(p)} / {p.Damage:N0} damage");
        try{Clipboard.SetText(Preferences.CopyFormat=="Multi-line"?string.Join(Environment.NewLine,lines):string.Join(" | ",lines));Notice("Summary copied");}catch(Exception e){Notice("Copy unavailable: "+e.Message);}
    }
    private bool InputProtected=>Preferences.ClickThrough||Preferences.PositionLocked||Preferences.EventOverlayLocked||Preferences.PetOverlayLocked;
    private void RefreshOverlayInput(){Overlay?.RefreshInput();_eventOverlay?.RefreshInput();_petOverlay?.Interaction.Refresh();}
    private void RecoverOverlayInput(){Preferences.ClickThrough=false;Preferences.PositionLocked=false;Preferences.EventOverlayLocked=false;Preferences.PetOverlayLocked=false;RefreshOverlayInput();Overlay?.ApplyPreferences();_eventOverlay?.ApplyPreferences();_petOverlay?.ApplyPreferences();QueueSave();}
    public void ToggleOverlay(){if(InputProtected){Restore();return;}if(Overlay?.IsVisible==true)Minimize();else Restore();}
    public void Minimize(){if(Overlay is null)return;if(Preferences.MinimizeToTray&&_desktop?.HasTray==true)Overlay.Hide();else Overlay.WindowState=WindowState.Minimized;}
    public void Restore(){RecoverOverlayInput();ShowOverlay();}
    internal void ResumeAfterGame()=>ShowOverlay();
    private void ShowOverlay(){if(Overlay is null)return;Overlay.Show();Overlay.WindowState=WindowState.Normal;Overlay.Activate();_desktop?.Apply();RefreshOverlayInput();Touch();}
    public void SuspendShortcuts()=>_desktop?.SuspendKeys();
    public void ResumeShortcuts()=>_desktop?.Apply();
    public void ToggleClickThrough(){if(InputProtected)RecoverOverlayInput();else Preferences.ClickThrough=true;_desktop?.Apply();RefreshOverlayInput();QueueSave();}
    internal void ObserveFieldBosses(FieldBossList list)
    {
        if(_fieldBosses.Update(list)){System.Threading.Interlocked.Exchange(ref _fieldBossDirty,1);System.Threading.Interlocked.Exchange(ref _pendingWorldTimerServer,list.ServerId!.Value);}
    }
    public void ForgetFieldBosses(){_fieldBosses.Clear();System.Threading.Interlocked.Exchange(ref _fieldBossDirty,1);RefreshWorldTimers(force:true);FlushFieldBosses(force:true);}
    private void FlushFieldBosses(bool force=false)
    {
        if(Preview||!force&&DateTimeOffset.UtcNow-_lastBossSave<TimeSpan.FromSeconds(10)||System.Threading.Interlocked.Exchange(ref _fieldBossDirty,0)==0)return;
        try{_fieldBosses.Save(_fieldBossPath);_lastBossSave=DateTimeOffset.UtcNow;}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){System.Threading.Interlocked.Exchange(ref _fieldBossDirty,1);_lastBossSave=DateTimeOffset.UtcNow;Notice("Could not save field-boss snapshots: "+e.Message);}
    }
    private static Guid TimerId(string key)=>new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)).AsSpan(0,16));
    internal void RefreshWorldTimers(DateTimeOffset? at=null,bool force=false)
    {
        var now=at??DateTimeOffset.UtcNow;
        if(!force&&now>=_lastWorldTick&&now-_lastWorldTick<TimeSpan.FromSeconds(1))return;
        _lastWorldTick=now;
        // Character combat IDs can disappear during a teleport. The selected timer
        // server and absolute map deadlines are independent of those temporary IDs.
        // Consume before the live snapshot so a newer capture-thread notification
        // remains queued for the next refresh instead of being lost.
        int observed=System.Threading.Interlocked.Exchange(ref _pendingWorldTimerServer,0);
        var live=_pipeline.LiveDiagnostics;
        bool currentServer=live is{SelfName:not null,ServerId:>=1000 and <=9999};
        // A character/map record and teleport can arrive between two UI ticks.
        // Retain the last verified server event even if live self is already pending again.
        int? confirmed=currentServer?live!.ServerId:observed>0?observed:null;
        _worldServer=confirmed??Preferences.WorldTimerServerId??_worldServer??_fieldBosses.LastServer;
        if(_worldServer is {} selected&&Preferences.WorldTimerServerId!=selected)
        {Preferences.WorldTimerServerId=selected;QueueSave();}
        string Clock(DateTimeOffset t)=>t.ToLocalTime().ToString(Preferences.Clock24?"HH:mm":"h:mm tt");
        var eventRows=new List<WorldTimerRow>();var bossRows=new List<WorldTimerRow>();var alerts=new List<EventReminder>();
        foreach(var e in _worldCatalog.Events)
        {
            bool enabled=e.Id switch{"shugo_festival"=>Preferences.ShowShugoTimer,"spacetime_rift"=>Preferences.ShowRiftTimer,"dimensional_invasion"=>Preferences.ShowInvasionTimer,_=>false};
            if(!enabled)continue;
            var status=EventTimetable.Evaluate(e,now,e.Id=="spacetime_rift"?Preferences.RiftShiftMinutes:0);
            string detail=status.InWindow?(e.Possible?"Possible window until ":"Scheduled window until ")+Clock(status.End):"Scheduled "+Clock(status.Start);
            eventRows.Add(new(){Id=e.Id,Name=e.Name,Icon=e.Icon,Detail=detail,Remaining=WorldTimerRow.Countdown(status.CountdownAt,now),Tooltip="UTC schedule estimate, not a live event confirmation. "+(status.InWindow?"Countdown to the table's window end.":"Countdown to the next table start.")+" Rifts can be shifted in event settings."});
            alerts.Add(new(){Id=TimerId(e.Id),Name=e.Name+" (schedule)",At=status.Start,LeadMinutes=Preferences.GameTimerLeadMinutes});
        }
        if(Preferences.ShowFieldBossTimers)foreach(var status in _fieldBosses.Snapshot(_worldServer))
        {
            var o=status.Observation;string detail="Waiting for map list",remaining="—";
            if(o is not null)
            {
                detail="Map snapshot · "+Clock(o.ObservedAt);
                if(o.Alive)remaining="Seen alive";
                else if(status.Due is {} due){remaining=due>now?WorldTimerRow.Countdown(due,now):"Due · refresh";alerts.Add(new(){Id=TimerId("boss:"+status.Boss.Npc),Name=status.Boss.Name+" (map timer)",At=due,LeadMinutes=Preferences.GameTimerLeadMinutes});}
                else detail="Respawn time unknown · "+Clock(o.ObservedAt);
            }
            bossRows.Add(new(){Id=status.Boss.Npc.ToString(),Name=status.Boss.Name,Icon="Combat",Detail=detail,Remaining=remaining,Tooltip=o is null?"Detect your character, then open Map → Field bosses in Verteron.":$"Server {o.ServerId}. Last map observation {o.ObservedAt.ToLocalTime():g}. "+(o.Alive?"This says it was alive when observed, not that it is still alive.":"A passed deadline means due; refresh the game map to confirm.")});
        }
        void Sync(System.Collections.ObjectModel.ObservableCollection<WorldTimerRow> target,List<WorldTimerRow> rows)
        {
            if(!target.Select(x=>x.Id).SequenceEqual(rows.Select(x=>x.Id))){target.Clear();foreach(var row in rows)target.Add(row);return;}
            for(int i=0;i<rows.Count;i++){target[i].Name=rows[i].Name;target[i].Detail=rows[i].Detail;target[i].Remaining=rows[i].Remaining;target[i].Icon=rows[i].Icon;target[i].Tooltip=rows[i].Tooltip;}
        }
        Sync(_vm.ScheduledEvents,eventRows);Sync(_vm.FieldBossTimers,bossRows);
        _vm.WorldTimerClock="UTC schedule · local "+Clock(now)+(_worldServer is {} server?" · "+(currentServer?"S":"Saved S")+server:"");
        _vm.WorldTimerInfo=Preferences.ShowFieldBossTimers?"Open the in-game map → Field bosses to sync. "+(_worldServer is null?"Character/server not identified.":currentServer?"Saved map snapshots; reopen to refresh.":$"Saved server S{_worldServer}; countdowns continue while character detection refreshes."):"Scheduled estimates · adjust rifts in event settings if needed.";
        if(!Preview)foreach(var alert in _gameAlertScheduler.Poll(Preferences.GameTimerAlerts?alerts:[],now))
        {
            string text=alert.LeadMinutes==0?"Timer due — check the game":$"Timer due in {alert.LeadMinutes} minutes";
            Notice(alert.Name+" · "+text);AlertSound.Play(Preferences.EventVolume);_desktop?.Notify(alert.Name,text);
        }
    }
    internal EventOverlayWindow CreateEventOverlay()=>_eventOverlay??=new(this);
    private void SyncEventOverlay()
    {
        if(_disposed)return;_eventOverlay?.ApplyPreferences();
        if(Preview||Overlay is null)return;
        if(!Preferences.TrayWhenGameCloses)_eventsAutoHidden=false;
        if(Preferences.ShowEventOverlay&&!_eventsAutoHidden){CreateEventOverlay().Show();_eventOverlay!.ApplyPreferences();}
        else _eventOverlay?.Hide();
    }
    public void SetEventOverlayVisible(bool visible){Preferences.ShowEventOverlay=visible;_eventsAutoHidden=false;RefreshWorldTimers(force:true);SyncEventOverlay();Overlay?.ApplyPreferences();QueueSave();}
    public void ToggleEventOverlay()=>SetEventOverlayVisible(!Preferences.ShowEventOverlay);
    public void AutoHideEventOverlay(bool hidden){_eventsAutoHidden=hidden;SyncEventOverlay();}
    public void ResetEventOverlayPosition(){Preferences.EventOverlayLeft=Preferences.EventOverlayTop=null;_eventOverlay?.ApplyPreferences();QueueSave();}
    private static IReadOnlyDictionary<uint,string> ReadPetNames()
    {
        try{return JsonSerializer.Deserialize<Dictionary<uint,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"data","pet_names.json")))??new();}
        catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){return new Dictionary<uint,string>();}
    }
    private void WakePets()
    {
        if(Preview||_disposed||System.Threading.Interlocked.Exchange(ref _petWake,1)!=0)return;
        _refresh.Dispatcher.BeginInvoke(new Action(()=>{System.Threading.Interlocked.Exchange(ref _petWake,0);if(!_disposed){if(_petOverlay?.IsVisible==true)_petOverlay.Refresh();RefreshPetPickup();}}),DispatcherPriority.Background);
    }
    internal PetPickupWindow CreatePetPickup()=>_petPickup??=new(this);
    private void RefreshPetPickup(){if(Preview)return;if(Preferences.PetCounterStyle!="Floating card"){_petPickup?.Hide();return;}if(_petsAutoHidden){_petPickup?.Hide();return;}if(Preferences.ShowPetPickups&&Pets.Pickup is {} p&&DateTimeOffset.UtcNow-p.At<TimeSpan.FromSeconds(6))CreatePetPickup().Update();else _petPickup?.Update();}
    public void PreviewPetPickup()=>CreatePetPickup().PreviewCounter();
    public void UsePetCountersOnly(){Preferences.ShowPetPickups=true;Preferences.PetCounterStyle="Beside loot rows";SetPetOverlayVisible(false);ApplyPreferences();}
    public void PreviewLootCounters()
    {
        if(!GameClient.Find(Preferences.GameProcess,out var game)){Notice("Open your game first to preview counters beside its loot area.");return;}
        var crop=GameLootReader.Crop(game,new(true,Preferences.GameProcess,Preferences.LootRegionLeft,Preferences.LootRegionTop,Preferences.LootRegionWidth,Preferences.LootRegionHeight,Preferences.LootCounterGap));
        _lootPreviewUntil=DateTimeOffset.UtcNow.AddSeconds(8);_lootCounter??=new();
        _lootCounter.Apply(new(game,crop,new[]{new PetLootRow(1016,0,30,16,"DEMO 14/25",true,"Position preview"),new PetLootRow(1016,0,75,16,"DEMO 15/25",true,"Position preview")},Preferences.LootCounterGap));
        Notice("Demo row counters shown for 8 seconds. Choose the loot text area to position them.");
    }
    public void ConfigureLootArea()
    {
        if(Preview){Notice("Loot-area selection is available while your game window is open.");return;}
        if(!GameClient.Find(Preferences.GameProcess,out var game)){Notice("Open the game first, then choose the loot text area.");return;}
        var selection=new LootRegionWindow(game);if(selection.ShowDialog()==true&&selection.Selection is {} r){Preferences.LootRegionLeft=r.X;Preferences.LootRegionTop=r.Y;Preferences.LootRegionWidth=r.Width;Preferences.LootRegionHeight=r.Height;ApplyPreferences();Notice("Loot text area saved. Return to the game to read pickup rows.");}
    }
    public void ResetLootArea(){Preferences.LootRegionLeft=.385;Preferences.LootRegionTop=.28;Preferences.LootRegionWidth=.20;Preferences.LootRegionHeight=.48;ApplyPreferences();}
    public void ResetPetPickupPosition(){Preferences.PetPickupLeft=Preferences.PetPickupTop=null;_petPickup?.ApplyPreferences();QueueSave();}
    public PetOverlayWindow CreatePetOverlay()=>_petOverlay??=new(this);
    private void SyncPetOverlay()
    {
        if(_disposed)return;_petOverlay?.ApplyPreferences();
        if(!Preferences.ShowPetOverlay)_petOverlay?.Hide();
        if(Preview||Overlay is null)return;
        if(Preferences.ShowPetOverlay&&!_petsAutoHidden){CreatePetOverlay().Show();_petOverlay!.Refresh();}
        else _petOverlay?.Hide();
    }
    public void SetPetOverlayVisible(bool value){Preferences.ShowPetOverlay=value;_petsAutoHidden=false;SyncPetOverlay();Overlay?.ApplyPreferences();QueueSave();}
    public void TogglePetOverlay()=>SetPetOverlayVisible(!Preferences.ShowPetOverlay);
    internal void AutoHidePetOverlay(bool hidden){_petsAutoHidden=hidden;SyncPetOverlay();if(hidden)_petPickup?.Hide();}
    public void ResetPetOverlayPosition(){Preferences.PetOverlayLeft=Preferences.PetOverlayTop=null;_petOverlay?.ApplyPreferences();QueueSave();}
    public void ToggleCombatStrip(){Preferences.CombatStrip=!Preferences.CombatStrip;ApplyPreferences();}
    public void OpenCaptureDiagnostics()=>ShowSettings("Debug");
    internal bool DeleteHistory(Guid id){if(_history is not null&&!_history.Remove(id))return false;lock(_historyGate)_capturedHistory.Remove(id);_engine.RemoveHistory(id);return true;}
    internal bool ClearSavedHistory(){if(_history is not null&&!_history.Clear())return false;lock(_historyGate){_capturedHistory.Clear();_capturedOrder.Clear();}_engine.ClearHistory();return true;}
    private void RefreshCaptureHealth(DateTimeOffset? at=null,EncounterSnapshot? snapshot=null)
    {
        bool admitted;lock(_eventLock)admitted=_events.Count>0;
        _vm.ApplyCaptureHealth(CaptureHealth.Evaluate(_npcap.IsRunning,_vm.Mode,_pipeline.Diagnostics,_pipeline.LiveDiagnostics?.IdentityRefreshPending==true,admitted,snapshot?.IsActive==true,at??DateTimeOffset.UtcNow,_captureFailure));
    }
    public void Quit(){Dispose();Application.Current.Shutdown();}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_demo.Stop();_refresh.Stop();_save.Stop();_lootReader?.Dispose();_lootCounter?.CloseForExit();_npcap.Dispose();_engine.FinishAtLastActivity(interrupted:true);_history?.Dispose();Pets.Dispose();_desktop?.Dispose();FlushFieldBosses(force:true);Save();_eventOverlay?.CloseForExit();_petOverlay?.CloseForExit();_petPickup?.CloseForExit();_settings?.Close();
    }
}
