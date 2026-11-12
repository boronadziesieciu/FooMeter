using System.Diagnostics;
using AscendAPP.Core.Settings;
using System.Text.Json;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Network;
using AscendAPP.Core.Pets;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Storage;

namespace FooMeter.Linux;

public sealed class LinuxMeter : IDisposable
{
    // All decoder access and UI snapshots use the same gate. No UI work runs on capture callbacks.
    private readonly object _gate = new();
    private readonly CombatEngine _engine = new();
    private readonly CaptureFilter _filter = new();
    private readonly HealingReconciler _healing = new();
    private readonly PlayerRatingStore _ratings = new();
    private readonly ExperimentalGlobalDecoder _decoder;
    private readonly NetworkPipeline _pipeline;
    private readonly EncounterHistoryStore? _history;
    private readonly string _bossPath;
    private readonly FieldBossTracker _bosses;
    private readonly WorldTimerCatalog _timers;
    private readonly PetProgressStore _pets;
    private BossForecastTracker _forecast;
    private DateTimeOffset? _dummyAt;
    private bool _dummyFinished;
    private DateTimeOffset? _pendingBossEnd;
    public PetPickup? Pickup => _pets.Pickup;
    private Process? _capture;
    private CancellationTokenSource? _stop;
    private Task? _reader;
    private string _status = "Capture stopped. Choose an adapter, then Start before entering your character.";
    private DateTimeOffset? _replayAt;
    private bool _persist;
    public MeterPreferences Preferences { get; private set; } = new();
    public void SavePreferences(MeterPreferences preferences)
    {
        lock (_gate) { preferences.Normalize(); Preferences = preferences;
            _engine.IdleTimeoutSeconds = preferences.IdleSeconds;
            _engine.ActiveDps = preferences.DpsCalculation == "Active";
            if (!Demo) PreferencesStore.Write(Path.Combine(ProfileDirectory, "settings.json"), preferences); }
    }
    public bool DeleteFight(Guid id) { lock (_gate) { _engine.RemoveHistory(id); return _history?.Remove(id) ?? false; } }
    public IReadOnlyList<AscendAPP.Core.Pets.PetLootRow> ResolveLoot(IEnumerable<AscendAPP.Core.Pets.LootTextLine> lines)
    { lock (_gate) return new AscendAPP.Core.Pets.LootRowMatcher(_petNames).Resolve(lines, _pets.Progress, _pets.RecentPickups(DateTimeOffset.UtcNow), DateTimeOffset.UtcNow); }
    private IReadOnlyDictionary<uint,string> _petNames = new Dictionary<uint,string>();
    public bool Demo { get; }
    public string ProfileDirectory { get; }
    public bool Running => _reader is { IsCompleted: false };
    internal Task CaptureCompletion => _reader ?? Task.CompletedTask;

    public LinuxMeter(bool demo = false, string? profile = null, bool seedDemo = true)
    {
        Demo = demo;
        string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg && Path.IsPathRooted(xdg)
            ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        ProfileDirectory = profile ?? Path.Combine(dataHome, "FooMeter");
        if (!demo) Directory.CreateDirectory(ProfileDirectory);
        Preferences = demo ? new() : PreferencesStore.Read(Path.Combine(ProfileDirectory, "settings.json"));
        if(!Preferences.RememberClickThrough)Preferences.ClickThrough=false;
        string Data(string name) => Path.Combine(AppContext.BaseDirectory, "data", name);
        _decoder = new(BossTimingCatalog.Read(Data("boss_berserk.json")), TargetCatalog.Read(Data("targets.json")));
        _forecast = new(BossTimingCatalog.Read(Data("boss_berserk.json")));
        _timers = WorldTimerCatalog.Read(Data("field_bosses.json"), Data("recurring_events.json"));
        _bosses = new(_timers);
        _bossPath = Path.Combine(ProfileDirectory, "fieldbosses.json");
        if (!demo)
        {
            _history = new(Path.Combine(ProfileDirectory, "history.json"));
            _engine.RestoreHistory(_history.Snapshot());
            _bosses.Load(_bossPath);
        }
        var names = JsonSerializer.Deserialize<Dictionary<uint, string>>(File.ReadAllText(Data("pet_names.json")))!;
        _petNames = names;
        _pets = new(demo ? null : Path.Combine(ProfileDirectory, "pets.json"), names);
        _engine.EncounterCompleted += fight => { if (_persist && !Demo) _history?.Enqueue(fight); };
        _decoder.IdentityUpdated += id =>
        {
            _filter.Identity(id); _ratings.Identity(id); _forecast.Identity(id);
            _engine.UpdateIdentity(id.Id, id.Name, id.ClassName, id.IsSelf);
            if (id.IsSelf && id.ServerId is { } server) _pets.Bind(id.Name, server);
        };
        _decoder.PartyUpdated += roster => { _filter.Party(roster); _forecast.Party(roster); };
        _decoder.EngagementUpdated += _forecast.Engage;
        _decoder.RatingUpdated += _ratings.Observe;
        _decoder.TargetDamaged += _healing.Damage;
        _decoder.HealthUpdated += hp =>
        {
            _healing.Observe(hp); _forecast.Health(hp.Id,hp.Current,hp.Maximum,hp.ObservedAt??DateTimeOffset.UtcNow); _engine.UpdateHealth(hp.Id, hp.Current, hp.Maximum);
            if (Preferences.BossResetDetection && hp.Current == 0 && _engine.Snapshot(DateTimeOffset.UtcNow, false).Boss?.ActorId == hp.Id)
                _pendingBossEnd=DateTimeOffset.UtcNow.AddMilliseconds(500);
        };
        _decoder.EntityRemoved += id => { _filter.Forget(id); _healing.Forget(id); _ratings.Forget(id); _forecast.Forget(id); };
        _decoder.CaptureContinuityLost += _healing.Reset;
        _decoder.PetCollectionUpdated += state => _pets.Observe(state);
        _decoder.PetSoulsReceived += gain => _pets.Observe(gain);
        _decoder.PetSyncLost += _pets.Invalidate;
        _decoder.FieldBossesUpdated += list => _bosses.Update(list);
        _decoder.ConnectionChanged += () =>
        {
            FinishEncounter(); _filter.Reset(); _healing.Reset(); _ratings.ClearWorld(); _forecast.Reset();_dummyAt=null;_dummyFinished=false;
            _pets.Unbind("Connection changed; enter the world to synchronize.");
        };
        _pipeline = new(_decoder, OnCombat);
        _pipeline.CaptureReset += () => _pets.Unbind("Capture changed; enter the world to synchronize.");
        _pipeline.Initialize();
        SavePreferences(Preferences);
        // End on inactivity as well as observed boss death; no infinite stale encounter.
        _engine.KeepBossOpen = false;
        if (demo && seedDemo)
        {
            var now = DateTimeOffset.UtcNow;
            _engine.Add(new(now.AddSeconds(-2), 1, "Demo player", 900, "Demo boss", 42, 12000,
                CombatKind.Damage, HitTags.Critical, SkillName: "Demo strike", ActorClass:"Sorcerer", IsSelf: true, TargetIsBoss: true));
            _engine.Add(new(now, 2, "Demo party member", 900, "Demo boss", 43, 8000,
                CombatKind.Damage, SkillName:"Demo skill", ActorClass:"Cleric", TargetIsBoss: true));
            var sample = _engine.Snapshot(now);
            _engine.RestoreHistory(new[]{sample with{Id=Guid.NewGuid(),StartedAt=now.AddMinutes(-20),IsActive=false},sample with{Id=Guid.NewGuid(),StartedAt=now.AddMinutes(-10),IsActive=false}});
            _status = "DEMO — synthetic combat; capture disabled; no profile files written.";
        }
    }

    private void OnCombat(CombatEvent e)
    {
        bool accepted = _filter.Accept(e, Preferences.CaptureScope, Preferences.DummyIds(), Preferences.IdleSeconds, false);
        if (e.Kind == CombatKind.Heal) { _healing.Add(e, accepted); return; }
        _forecast.Add(e,accepted);
        if (!accepted || _dummyFinished) return;
        if(e.TargetIsTrainingDummy&&Preferences.DummySeconds>0) {
            _dummyAt??=e.Time;
            if(e.Time-_dummyAt>=TimeSpan.FromSeconds(Preferences.DummySeconds)) { _engine.Finish(_dummyAt.Value.AddSeconds(Preferences.DummySeconds)); _filter.EndEncounter(); _healing.ClearPending(); _dummyFinished=true;return; }
        }
        _engine.Add(e);
    }

    public void Reset() { lock (_gate) {FinishEncounter();_dummyAt=null;_dummyFinished=false;_forecast.ResetEncounter();} }
    private void FinishEncounter()
    {
        _pendingBossEnd=null;_engine.FinishAtLastActivity(); _engine.Reset(DateTimeOffset.UtcNow);
        _filter.EndEncounter(); _healing.ClearPending();
    }

    public static async Task<string[]> Devices()
    {
        using var process = NewDumpcap("-D");
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await stdout; var errors = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException(errors.Trim());
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static Process NewDumpcap(params string[] arguments)
    {
        var info = new ProcessStartInfo("dumpcap") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return new() { StartInfo = info };
    }

    public async Task Start(string device)
    {
        await Stop();
        if (Demo) throw new InvalidOperationException("Restart without --demo to capture.");
        lock (_gate)
        {
            _replayAt = null; _persist = true; _pipeline.StartSession();
            _status = "Starting dumpcap…";
        }
        _stop = new();
        // The kernel filter excludes unrelated TCP. Protocol and actor/target checks remain in the shared core.
        Preferences.Adapter = device; SavePreferences(Preferences);
        int port = int.TryParse(Preferences.Port, out var configured) && configured is > 0 and <= 65535 ? configured : 13328;
        _capture = NewDumpcap("-i", device, "-p", "-F", "pcap", "-s", "262144", "-B", "16", "-f", $"tcp port {port}", "-w", "-");
        try { _capture.Start(); }
        catch { _capture.Dispose(); _capture = null; throw; }
        var capture = _capture;
        _reader = ReadCapture(capture, _stop.Token);
    }

    private async Task ReadCapture(Process capture, CancellationToken token)
    {
        // Drain stderr even while stdout is active; retain only a bounded diagnostic tail.
        var stderr = ReadDiagnosticTail(capture.StandardError);
        string? failure = null;
        try
        {
            lock (_gate) _status = "Listening on game TCP port 13328; enter your character to synchronize.";
            await PcapStreamReader.ReadAsync(capture.StandardOutput.BaseStream, (link, bytes, at) =>
            { lock (_gate) _pipeline.Ingest(link, bytes, "linux", at); }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception e) { failure = e.Message; }
        finally
        {
            if (!capture.HasExited) capture.Kill(entireProcessTree: true);
            await capture.WaitForExitAsync();
            string diagnostics = await stderr;
            lock (_gate)
            {
                _status = token.IsCancellationRequested ? "Capture stopped." :
                    $"Capture ended (exit {capture.ExitCode}). {failure} {diagnostics}";
                FinishEncounter();
            }
        }
    }

    private static async Task<string> ReadDiagnosticTail(StreamReader reader)
    {
        string tail = ""; var buffer = new char[1024]; int n;
        while ((n = await reader.ReadAsync(buffer)) > 0)
        { tail += new string(buffer, 0, n); if (tail.Length > 4096) tail = tail[^4096..]; }
        return tail.Trim();
    }

    public async Task Stop()
    {
        _stop?.Cancel();
        if (_reader is not null) await _reader;
        _reader = null; _capture?.Dispose(); _capture = null;
        _stop?.Dispose(); _stop = null;
    }

    public async Task Replay(string path)
    {
        await Stop();
        lock (_gate) { FinishEncounter(); _persist = false; _pipeline.StartSession(); }
        await using var file = File.OpenRead(path);
        await PcapStreamReader.ReadAsync(file, (link, bytes, at) =>
        { lock (_gate) { _replayAt = at; _pipeline.Ingest(link, bytes, "replay", at); } });
        lock (_gate) _status = "Offline replay complete; no replay fights saved to live history.";
    }

    public MeterView View()
    {
        lock (_gate)
        {
            var now = _replayAt ?? DateTimeOffset.UtcNow;
            foreach (var heal in _healing.Drain(now)) _engine.Add(heal);
            if(_pendingBossEnd is {} end && now>=end){_pendingBossEnd=null;_engine.FinishAtLastActivity();_filter.EndEncounter();_healing.ClearPending();}
            var snapshot = _engine.Snapshot(now);
            return new(snapshot, _pipeline.Diagnostics, _status, _pipeline.LiveDiagnostics?.IdentityState ?? "",
                _timers.Events.Select(e => EventTimetable.Evaluate(e, now, e.Id.Contains("rift", StringComparison.OrdinalIgnoreCase) ? Preferences.RiftShiftMinutes : 0)).ToArray(),
                _bosses.Snapshot(Preferences.WorldTimerServerId ?? _decoder.SelfServerId), _pets.Rows(new HashSet<uint>(), false, false), _pets.Status,
                _history?.Snapshot() ?? _engine.History, _history?.Status ?? "Demo history is temporary",
                snapshot.Players.ToDictionary(p => p.ActorId, p => FindRating(p, now)), _forecast.Snapshot(now,Preferences.CaptureScope));
        }
    }

    private PlayerRating? FindRating(PlayerSnapshot player, DateTimeOffset now)
    {
        var observed=_ratings.Find(player.ActorId,player.Name,now);
        if(player.IsSelf && _decoder.SelfServerId is {} server)
            foreach(var entry in Preferences.OwnScoreEntries)observed=entry.Fill(observed,player.ActorId,player.Name,server);
        return observed;
    }
    public void Dispose()
    {
        Stop().GetAwaiter().GetResult();
        lock (_gate)
        {
            FinishEncounter();
            if (!Demo) _bosses.Save(_bossPath);
            _pets.Dispose(); _history?.Dispose();
        }
    }
}

public sealed record MeterView(EncounterSnapshot Fight, CaptureDiagnostics Diagnostics, string Status, string Identity,
    IReadOnlyList<EventTimerStatus> Events, IReadOnlyList<FieldBossTimerStatus> Bosses,
    IReadOnlyList<PetProgress> Pets, string PetStatus, IReadOnlyList<EncounterSnapshot> History, string HistoryStatus,
    IReadOnlyDictionary<uint, PlayerRating?> Ratings, BossForecast Forecast);
