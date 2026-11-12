using System.Text;
using System.Text.Json;
using AscendAPP.Core.Combat;
namespace AscendAPP.Core.Storage;
/// <summary>Bounded local snapshots. Capture callbacks enqueue; serialization and disk writes run on one worker.</summary>
public sealed class EncounterHistoryStore : IDisposable
{
    public const int MaxFights=80,MaxBytes=16*1024*1024,MaxFightBytes=2*1024*1024;
    private sealed record Document(int Version,EncounterSnapshot[] Fights);
    private static readonly JsonSerializerOptions Json=new(){MaxDepth=32};
    private readonly object _gate=new();private readonly string _path;private readonly Dictionary<Guid,EncounterSnapshot> _fights=[];
    private FileStream? _lease;private bool _disposed;private Task _writer=Task.CompletedTask;private bool _writing,_readOnly;private long _version,_saved;private string _status="History ready",_error="";
    public string Status{get{lock(_gate)return _status;}}public string LastError{get{lock(_gate)return _error;}}
    public EncounterHistoryStore(string path){_path=Path.GetFullPath(path);try{Directory.CreateDirectory(Path.GetDirectoryName(_path)!);_lease=new FileStream(_path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){_readOnly=true;_error=_status="History is open elsewhere or not writable; this instance is read-only.";}Load();if(_lease is null)_error=_status="History is open elsewhere or not writable; this instance is read-only.";}
    public IReadOnlyList<EncounterSnapshot> Snapshot(){lock(_gate)return _fights.Values.OrderByDescending(f=>f.StartedAt).ToArray();}
    private static bool Label(string? s,bool optional=false)=>s is null?optional:s.Length<=100&&!s.Any(char.IsControl);
    public static bool Valid(EncounterSnapshot? f)
    {
        if(f is null||f.Id==Guid.Empty||f.IsActive||f.StartedAt<DateTimeOffset.UnixEpoch||f.StartedAt.Year>2100||!double.IsFinite(f.DurationSeconds)||f.DurationSeconds is <1 or >86401||f.TotalDamage<0||f.TotalHealing<0||f.TopHit<0||!Label(f.TopHitBy)||!Label(f.TargetName,true)||f.Players is null||f.Players.Count is <1 or >96)return false;
        bool Health(BossSnapshot? b)=>b is null||Label(b.Name)&&b.CurrentHp is not <0&&b.MaxHp is not <=0&&(!b.CurrentHp.HasValue||!b.MaxHp.HasValue||b.CurrentHp<=b.MaxHp);
        if(!Health(f.Boss)||!Health(f.TargetHealth))return false;
        try
        {
            long damage=0,healing=0;var ids=new HashSet<uint>();
            foreach(var p in f.Players)
            {
                if(p is null||p.ActorId==0||!ids.Add(p.ActorId)||string.IsNullOrWhiteSpace(p.Name)||!Label(p.Name)||!Label(p.ClassName,true)||p.Damage<0||p.Healing<0||p.Hits<0||p.HealingHits<0||!double.IsFinite(p.ActiveSeconds)||p.ActiveSeconds<0||p.ActiveSeconds>f.DurationSeconds+1||!double.IsFinite(p.CritRate)||p.CritRate is <0 or >1||p.Skills is null||p.Skills.Count>512||p.Timeline?.Count>86401||p.RawHealing<0||p.ExcludedHealing<0||p.UnverifiedHealing<0||p.LegacyHealing<0)return false;
                damage=checked(damage+p.Damage);healing=checked(healing+p.Healing);
                if(p.Skills.Any(s=>s is null||s.SkillId<0||s.Damage<0||s.Healing<0||s.Hits<0||s.CriticalHits<0||s.CriticalHits>s.Hits||s.HealingHits<0||s.HealingCriticalHits<0||s.HealingCriticalHits>s.HealingHits||s.MaxHit<0||s.MaxHeal<0||!double.IsFinite(s.AverageHit)||s.AverageHit<0||!Label(s.Name,true)))return false;
                if(p.Timeline?.Any(d=>d is null||d.Second is <0 or >86400||d.Damage<0)==true)return false;
            }
            return damage==f.TotalDamage&&healing==f.TotalHealing;
        }catch(OverflowException){return false;}
    }
    private static EncounterSnapshot Canonical(EncounterSnapshot f)=>f with{PartyDps=f.TotalDamage/f.DurationSeconds,Players=f.Players.Select(p=>p with{Dps=p.Damage/f.DurationSeconds,Share=f.TotalDamage>0?(double)p.Damage/f.TotalDamage:0,Burst5s=double.IsFinite(p.Burst5s)&&p.Burst5s>=0?p.Burst5s:0}).ToArray()};
    private void Load()
    {
        if(!File.Exists(_path))return;
        try
        {
            if(new FileInfo(_path).Length>MaxBytes){_readOnly=true;_error=_status="History exceeds 16 MB; original file preserved. Export or clear it before recording more.";return;}
            using var doc=JsonDocument.Parse(File.ReadAllBytes(_path),new(){MaxDepth=32});
            if(!doc.RootElement.TryGetProperty("Version",out var version)||!version.TryGetInt32(out int v)||v!=1){_readOnly=true;_error=_status="Unsupported history version; original file preserved.";return;}
            if(!doc.RootElement.TryGetProperty("Fights",out var rows)||rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()>MaxFights)throw new JsonException("Invalid archive shape");
            int rejected=0;
            foreach(var row in rows.EnumerateArray())
            {
                try{var f=row.Deserialize<EncounterSnapshot>(Json);if(!Valid(f)){rejected++;continue;}_fights[f!.Id]=Canonical(f);}
                catch(Exception e)when(e is JsonException or InvalidOperationException or ArgumentException){rejected++;}
            }
            if(rejected>0&&_lease is not null){File.Copy(_path,_path+".recovery",true);_status=$"Loaded {_fights.Count} encounters; {rejected} invalid records skipped. Original preserved as history.json.recovery.";}
            else _status=$"Loaded {_fights.Count} saved encounters";
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        {
            _fights.Clear();if(_lease is null){_readOnly=true;return;}try{File.Copy(_path,_path+".recovery",true);_status=_error="History unreadable; recovery copy preserved. New encounters can still be saved.";}
            catch(Exception copy)when(copy is IOException or UnauthorizedAccessException){_readOnly=true;_status=_error="History could not be read or backed up; original file preserved.";}
        }
    }
    public void Enqueue(EncounterSnapshot fight)
    {
        if(fight.Id==Guid.Empty||fight.IsActive||fight.Players.Count==0)return;
        lock(_gate){if(_readOnly||_disposed)return;_fights[fight.Id]=fight;while(_fights.Count>MaxFights)_fights.Remove(_fights.Values.MinBy(f=>f.StartedAt)!.Id);_version++;_status="Saving fight history…";Start();}
    }
    public bool Remove(Guid id){lock(_gate){if(_readOnly||_disposed)return false;if(_fights.Remove(id)){_version++;Start();}return true;}}
    public bool Clear(){lock(_gate){if(_lease is null||_disposed)return false;_readOnly=false;_fights.Clear();_version++;Start();return true;}}
    private void Start(){if(_writing||_readOnly)return;_writing=true;_writer=Task.Run(WriteLoop);}
    private async Task WriteLoop()
    {
        await Task.Delay(120).ConfigureAwait(false);
        while(true)
        {
            EncounterSnapshot[] all;long version;
            lock(_gate){if(_saved==_version){_writing=false;return;}version=_version;all=_fights.Values.OrderByDescending(f=>f.StartedAt).ToArray();}
            try
            {
                var selected=new List<EncounterSnapshot>();int size=64,skipped=0;
                foreach(var f in all){if(!Valid(f)){skipped++;continue;}var normalized=Canonical(f);int bytes=JsonSerializer.SerializeToUtf8Bytes(normalized,Json).Length;if(bytes>MaxFightBytes||size+bytes+1>MaxBytes){skipped++;continue;}selected.Add(normalized);size+=bytes+1;}
                string text=JsonSerializer.Serialize(new Document(1,selected.ToArray()),Json);if(Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new InvalidDataException("History exceeds byte limit");
                AtomicFile.WriteAllText(_path,text,flushToDisk:true);
                lock(_gate){_saved=version;_error="";_status=$"Saved {selected.Count} encounters locally"+(skipped>0?$"; {skipped} records exceeded validation/size limits":"");}
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
            {lock(_gate){_error=_status="History save failed; recording remains in memory. "+e.Message;_writing=false;}return;}
        }
    }
    public async Task<bool> FlushAsync()
    {
        Task pending;lock(_gate){if(_readOnly)return false;if(!_writing&&_saved!=_version)Start();pending=_writer;}
        await pending.ConfigureAwait(false);lock(_gate)return _saved==_version;
    }
    public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;}FlushAsync().GetAwaiter().GetResult();_lease?.Dispose();}
    public static void Export(string path,EncounterSnapshot fight){if(!Valid(fight))throw new InvalidDataException("Encounter is not a valid completed snapshot.");AtomicFile.WriteAllText(path,JsonSerializer.Serialize(new Document(1,[Canonical(fight)]),new JsonSerializerOptions(Json){WriteIndented=true}),flushToDisk:true);}
}
