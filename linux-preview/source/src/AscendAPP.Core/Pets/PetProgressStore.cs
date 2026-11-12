using System.Text.Json;
using AscendAPP.Core.Storage;
namespace AscendAPP.Core.Pets;
public sealed record PetCharacter(string Name,int ServerId,DateTimeOffset Seen,IReadOnlyList<PetCount> Counts);
public sealed record PetPickup(long Sequence,uint Id,string Name,int Amount,int? Level,int? Points,bool Confirmed,DateTimeOffset At);
public sealed record PetArchive(int Version,IReadOnlyList<PetCharacter> Characters);
public sealed record PetProgress(uint Id,string Name,int? Level,int? Points,bool Confirmed,bool Pinned,DateTimeOffset? Recent)
{
    public bool Complete=>Level==3;
    public string Progress=>Level is null?"—":Complete?"MAX":$"{Points}/{PetProtocol.Required(Level.Value)}";
    public string Left=>Level is null?"Total unknown":Complete?"Fully collected":$"{PetProtocol.Required(Level.Value)-Points} souls to next level";
    public double Fraction=>Level is null?0:Complete?1:(double)(Points??0)/PetProtocol.Required(Level.Value);
    public string Rank=>Level is null?"Lv —":$"Lv {Level}";
}
/// <summary>Only server snapshots establish totals. Saved totals are never assumed current after a gap/restart.</summary>
public sealed class PetProgressStore : IDisposable
{
    private readonly object _gate=new(),_writeGate=new();private readonly string? _path;private readonly Dictionary<uint,string> _names;
    private readonly Dictionary<string,PetCharacter> _characters=new();private readonly Dictionary<uint,DateTimeOffset> _recent=new();
    private FileStream? _lease;private bool _writable=true,_trusted,_disposed;private string? _owner;private string? _ownerName;private int _ownerServer;private int _revision;private int _saved;
    private long _viewRevision;public long ViewRevision {get{lock(_gate)return _viewRevision;}}
    private long _pickupSequence;private readonly Queue<PetPickup> _pickups=new();
    public IReadOnlyList<PetPickup> RecentPickups(DateTimeOffset now){lock(_gate)return _pickups.Where(p=>now>=p.At&&now-p.At<=TimeSpan.FromSeconds(8)).ToArray();}
    public PetProgress Progress(uint id){lock(_gate){var has=_owner is not null&&_characters.TryGetValue(_owner,out var c);var p=has?_characters[_owner!].Counts.FirstOrDefault(x=>x.Id==id):null;return new(id,Name(id),p?.Level??(has?0:null),p?.Points??(has?0:null),_trusted,false,null);}}
    private void RecordPickup(){if(Pickup is {} p){_pickups.Enqueue(p);while(_pickups.Count>64)_pickups.Dequeue();}}
    public PetPickup? Pickup {get{lock(_gate){if(_lastGain is not {} gain)return null;var count=_owner is not null&&_characters.TryGetValue(_owner,out var c)?c.Counts.FirstOrDefault(x=>x.Id==gain.PetId):null;return new(_pickupSequence,gain.PetId,Name(gain.PetId),gain.Amount,_trusted?count?.Level:null,_trusted?count?.Points:null,_trusted,gain.At);}}}
    private PetGainObservation? _lastGain;private string _syncReason="Enter the world with capture running.";
    public string? LastError {get;private set;}
    public PetProgressStore(string? path,IReadOnlyDictionary<uint,string>? names=null)
    {
        _path=path;_names=names?.Where(x=>x.Key>0&&!string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x=>x.Key,x=>x.Value)??new();
        if(path is null)return;
        try{Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);_lease=new(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){_writable=false;LastError="Pet progress is read-only: another instance or a folder restriction.";}
        Load();
    }
    private static string Key(string name,int server)=>server+":"+name;
    private static bool ValidOwner(string name,int server)=>!string.IsNullOrWhiteSpace(name)&&name.Length<=100&&!name.Any(char.IsControl)&&server is >=1000 and <=9999;
    public void Bind(string name,int server)
    {lock(_gate){if(!ValidOwner(name,server))return;string key=Key(name,server);if(key==_owner)return;_viewRevision++;_owner=key;_ownerName=name;_ownerServer=server;_trusted=false;_syncReason="Waiting for this character's collection.";_recent.Clear();_lastGain=null;_pickups.Clear();}}
    public void Invalidate(string reason){lock(_gate){if(_trusted||_syncReason!=reason)_viewRevision++;_trusted=false;_syncReason=reason;}}
    public void Unbind(string reason){lock(_gate){_viewRevision++;_owner=null;_ownerName=null;_ownerServer=0;_trusted=false;_recent.Clear();_lastGain=null;_syncReason=reason;}}
    public bool Observe(PetCollectionObservation state)
    {
        lock(_gate){
            if(!ValidOwner(state.Character,state.ServerId)||state.Counts is null||state.Counts.Count>PetProtocol.MaxPets||state.Counts.Any(x=>!PetProtocol.Valid(x))||state.Counts.Select(x=>x.Id).Distinct().Count()!=state.Counts.Count)return false;
            Bind(state.Character,state.ServerId);_characters[_owner!]=new(state.Character,state.ServerId,state.At,state.Counts.ToArray());
            _viewRevision++;_trusted=true;_syncReason="Collection synchronized from game packets.";_revision++;
            while(_characters.Count>32)_characters.Remove(_characters.Where(x=>x.Key!=_owner).MinBy(x=>x.Value.Seen).Key);
            return true;
        }
    }
    public bool Observe(PetGainObservation gain)
    {
        lock(_gate){
            if(!ValidOwner(gain.Character,gain.ServerId)||gain.PetId is 0 or >1000000||gain.Amount is <=0 or >1000000)return false;
            Bind(gain.Character,gain.ServerId);_viewRevision++;_pickupSequence++;_lastGain=gain;_recent[gain.PetId]=gain.At;
            if(_recent.Count>64)_recent.Remove(_recent.MinBy(x=>x.Value).Key);
            if(!_trusted||!_characters.TryGetValue(_owner!,out var character)){RecordPickup();return true;}
            var counts=character.Counts.ToDictionary(x=>x.Id);var old=counts.GetValueOrDefault(gain.PetId)??new(gain.PetId,0,0);
            if(!counts.ContainsKey(gain.PetId)&&counts.Count>=PetProtocol.MaxPets){Invalidate("Collection limit reached; re-enter the world to sync.");RecordPickup();return true;}
            int level=old.Level,points=old.Points+gain.Amount;
            while(level<3&&points>=PetProtocol.Required(level)){points-=PetProtocol.Required(level);level++;}
            counts[gain.PetId]=new(gain.PetId,level,level==3?0:points);
            _characters[_owner!]=character with{Counts=counts.Values.ToArray(),Seen=gain.At};_revision++;RecordPickup();return true;
        }
    }
    public string Status {get{lock(_gate){string identity=_owner is not null?_ownerName+" · "+_ownerServer:"Character not identified";return identity+" · "+(_trusted?"Live collection":_owner is not null&&_characters.ContainsKey(_owner)?"Saved collection · needs sync":"Waiting for collection")+"\n"+_syncReason+(LastError is not null?"\n"+LastError:"");}}}
    public bool Trusted {get{lock(_gate)return _trusted;}}
    public string Toast(DateTimeOffset now)
    {
        lock(_gate){if(_lastGain is not {} g||now<g.At||now-g.At>TimeSpan.FromSeconds(6))return "";
            string suffix="total unknown";if(_trusted&&_characters.TryGetValue(_owner!,out var c)){var p=c.Counts.FirstOrDefault(x=>x.Id==g.PetId);if(p is not null)suffix=p.Level==3?"MAX":$"{p.Points}/{PetProtocol.Required(p.Level)} · {PetProtocol.Required(p.Level)-p.Points} left";}
            return $"+{g.Amount} soul{(g.Amount==1?"":"s")} · {Name(g.PetId)} · {suffix}";}
    }
    public string Name(uint id)=>_names.GetValueOrDefault(id)??"Pet #"+id;
    public IReadOnlyList<PetProgress> Rows(IReadOnlySet<uint> pins,bool onlyPinned,bool hideCompleted,string search="",DateTimeOffset? now=null)
    {
        lock(_gate){
            var counts=_owner is not null&&_characters.TryGetValue(_owner,out var c)?c.Counts.ToDictionary(x=>x.Id):new Dictionary<uint,PetCount>();
            var ids=_names.Keys.Union(counts.Keys).Union(_recent.Keys).Union(pins);
            bool hasSnapshot=_owner is not null&&_characters.ContainsKey(_owner);
            return ids.Select(id=>{var p=counts.GetValueOrDefault(id);return new PetProgress(id,Name(id),p?.Level??(hasSnapshot?0:null),p?.Points??(hasSnapshot?0:null),_trusted,pins.Contains(id),_recent.TryGetValue(id,out var at)?at:null);})
                .Where(r=>(!onlyPinned||r.Pinned)&&(!hideCompleted||!r.Complete||(r.Recent is {} recent&&(now??DateTimeOffset.UtcNow)-recent<=TimeSpan.FromSeconds(6)))&&(search.Length==0||r.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||r.Id.ToString().Contains(search)))
                .OrderByDescending(r=>r.Recent).ThenByDescending(r=>r.Pinned).ThenBy(r=>r.Complete).ThenBy(r=>r.Name).Take(1024).ToArray();
        }
    }
    private void Load()
    {
        if(_path is null||!File.Exists(_path))return;
        try{
            if(new FileInfo(_path).Length>2*1024*1024)throw new InvalidDataException("Pet archive exceeds 2 MB; original preserved.");
            var archive=JsonSerializer.Deserialize<PetArchive>(File.ReadAllText(_path));
            if(archive?.Version!=1)throw new InvalidDataException("Unsupported pet archive; original preserved.");
            if(archive.Characters is null||archive.Characters.Count>32)throw new InvalidDataException("Invalid pet archive; original preserved.");
            foreach(var c in archive.Characters){
                if(c is null||!ValidOwner(c.Name,c.ServerId)||c.Counts is null||c.Counts.Count>PetProtocol.MaxPets||c.Counts.Any(x=>!PetProtocol.Valid(x))||c.Counts.Select(x=>x.Id).Distinct().Count()!=c.Counts.Count)throw new InvalidDataException("Invalid pet collection; original preserved.");
                if(!_characters.TryAdd(Key(c.Name,c.ServerId),c))throw new InvalidDataException("Duplicate pet character; original preserved.");
            }
        }catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException or InvalidDataException){_characters.Clear();_writable=false;LastError="Pet archive not loaded: "+e.Message;}
    }
    public bool Save()
    {
        lock(_writeGate){
            PetArchive archive;int revision;
            lock(_gate){if(_path is null||_saved==_revision)return true;if(!_writable)return false;revision=_revision;archive=new(1,_characters.Values.OrderByDescending(x=>x.Seen).ToArray());}
            // Immutable snapshot: serialization and disk flushing never hold the capture-state gate.
            try{AtomicFile.WriteAllText(_path!,JsonSerializer.Serialize(archive),flushToDisk:true);lock(_gate){_saved=revision;LastError=null;}return true;}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException){lock(_gate)LastError="Could not save pet progress: "+e.Message;return false;}
        }
    }
    public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;}lock(_writeGate){Save();_lease?.Dispose();_lease=null;}}
}
