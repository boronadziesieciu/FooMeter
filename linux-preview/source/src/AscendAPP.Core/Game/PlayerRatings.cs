using System.Buffers.Binary;
using AscendAPP.Core.Protocol;
namespace AscendAPP.Core.Game;
public sealed record PlayerRating(uint ActorId,string Name,int? ServerId,int? GearScore,int? CombatPower,DateTimeOffset At,string Source,DateTimeOffset? CombatPowerAt=null,DateTimeOffset? GearScoreAt=null,bool ManualGearScore=false,bool ManualCombatPower=false);
/// <summary>Selected MIT reference facts: SkeeveAN/Aion-DPS-Meter 03026182c4ae2059b41804189852af6e945e365a, ReadSeenLevels/ReadSeenCombatPower.
/// A unique level anchor is required. GS and CP are independent; ambiguous tail values remain unknown.</summary>
public static class PlayerRatingReader
{
    public static PlayerRating? Read(uint id,string name,int? server,ReadOnlySpan<byte> body,int afterName,DateTimeOffset at)
    {
        if(body.Length<64||afterName<0||afterName+5>body.Length||GlobalProtocolFacts.CharacterClass(BinaryPrimitives.ReadInt32LittleEndian(body[afterName..])) is null||body[afterName+4] is not (1 or 2))return null;
        var anchors=new List<(int Level,int Score,int At)>();
        for(int p=Math.Max(afterName+5,body.Length-460);p+16<=body.Length;p++)
        {
            if(BinaryPrimitives.ReadInt32LittleEndian(body[p..])!=-3000)continue;
            int level=BinaryPrimitives.ReadInt32LittleEndian(body[(p+4)..]),zero=BinaryPrimitives.ReadInt32LittleEndian(body[(p+8)..]),score=BinaryPrimitives.ReadInt32LittleEndian(body[(p+12)..]);
            if(level is >=1 and <=100&&zero==0)anchors.Add((level,score,p));
        }
        if(anchors.Count!=1)return null;
        var values=new HashSet<int>();
        foreach(int back in new[]{9,10,8,11})
        {int p=body.Length-back;if(p<anchors[0].At+16)continue;int value=BinaryPrimitives.ReadInt32LittleEndian(body[p..]);if(value is >=20_000 and <=4_000_000)values.Add(value);}
        int? scoreValue=anchors[0].Score is >0 and <100_000?anchors[0].Score:null;
        return new(id,name,server,scoreValue,values.Count==1?values.Single():null,at,"Observed game profile (experimental)");
    }
}
/// <summary>World IDs are transient. Profile values can be rebound only to a freshly verified exact name/server.</summary>
public sealed class PlayerRatingStore
{
    private readonly object _gate=new();private readonly Dictionary<uint,PlayerRating> _values=new();
    private readonly Dictionary<uint,PlayerIdentity> _identities=new();
    private readonly Dictionary<(string Name,int Server),PlayerRating> _profiles=new();
    private static readonly TimeSpan ScoreLifetime=TimeSpan.FromHours(2),PowerLifetime=TimeSpan.FromMinutes(10);
    private static bool Fresh(DateTimeOffset observed,DateTimeOffset at,TimeSpan lifetime)=>at>=observed&&at-observed<=lifetime;
    private static bool SamePlayer(PlayerRating a,PlayerRating b)=>a.Name==b.Name&&a.ServerId==b.ServerId;
    private static PlayerRating? Current(PlayerRating rating,DateTimeOffset at)
    {
        int? score=rating.GearScore.HasValue&&Fresh(rating.GearScoreAt??rating.At,at,ScoreLifetime)?rating.GearScore:null;
        int? power=rating.CombatPower.HasValue&&Fresh(rating.CombatPowerAt??rating.At,at,PowerLifetime)?rating.CombatPower:null;
        return score.HasValue||power.HasValue?rating with{GearScore=score,CombatPower=power}:null;
    }
    private static PlayerRating Merge(PlayerRating rating,PlayerRating? prior)
    {
        if(rating.GearScore.HasValue)rating=rating with{GearScoreAt=rating.GearScoreAt??rating.At};
        if(rating.CombatPower.HasValue)rating=rating with{CombatPowerAt=rating.CombatPowerAt??rating.At};
        if(prior is null||!SamePlayer(rating,prior)||Current(prior,rating.At)is not {} fresh)return rating;
        bool retained=false;
        if(!rating.GearScore.HasValue&&fresh.GearScore.HasValue){rating=rating with{GearScore=fresh.GearScore,GearScoreAt=fresh.GearScoreAt??fresh.At};retained=true;}
        if(!rating.CombatPower.HasValue&&fresh.CombatPower.HasValue){rating=rating with{CombatPower=fresh.CombatPower,CombatPowerAt=fresh.CombatPowerAt??fresh.At};retained=true;}
        return retained?rating with{Source="Observed game profile / partial update (experimental)"}:rating;
    }
    private void SaveProfile(PlayerRating r)
    {
        if(r.ServerId is not {} server)return;
        if(_profiles.Count>=2048&&!_profiles.ContainsKey((r.Name,server)))_profiles.Remove(_profiles.MinBy(x=>x.Value.At).Key);
        var key=(r.Name,server);if(!_profiles.TryGetValue(key,out var old)||r.At>=old.At)_profiles[key]=r;
    }
    public void Observe(PlayerRating rating)
    {
        lock(_gate)
        {
            if(rating.ActorId!=0&&rating.ServerId is null&&_identities.TryGetValue(rating.ActorId,out var identity)&&identity.Name==rating.Name)rating=rating with{ServerId=identity.ServerId};
            PlayerRating? prior=null;
            if(rating.ActorId!=0&&_values.TryGetValue(rating.ActorId,out var actorRating)&&SamePlayer(actorRating,rating))prior=actorRating;
            if(rating.ServerId is {} server&&_profiles.TryGetValue((rating.Name,server),out var profile)&&(prior is null||profile.At>=prior.At))prior=profile;
            if(prior is not null&&prior.At>rating.At)return;
            rating=Merge(rating,prior);
            // Inspect may identify a roster member before a world actor ID is available. Keep that exact
            // profile without letting all such members collide in the actor dictionary at ID zero.
            if(rating.ActorId!=0)
            {
                if(_values.Count>=2048&&!_values.ContainsKey(rating.ActorId))_values.Remove(_values.MinBy(x=>x.Value.At).Key);
                _values[rating.ActorId]=rating;
            }
            SaveProfile(rating);
        }
    }
    public void Identity(PlayerIdentity p)
    {
        lock(_gate)
        {
            if(!p.IsSelf&&!p.PlayerVerified){_identities.Remove(p.Id);_values.Remove(p.Id);return;}
            if(_identities.Count>=2048&&!_identities.ContainsKey(p.Id))_identities.Remove(_identities.Keys.First());
            _identities[p.Id]=p;
            if(_values.TryGetValue(p.Id,out var rating))
            {
                if(rating.Name!=p.Name||rating.ServerId.HasValue&&p.ServerId.HasValue&&rating.ServerId!=p.ServerId)_values.Remove(p.Id);
                else if(rating.ServerId is null&&p.ServerId.HasValue)
                {
                    rating=rating with{ServerId=p.ServerId};
                    if(_profiles.TryGetValue((rating.Name,p.ServerId.Value),out var prior)&&prior.At<=rating.At)rating=Merge(rating,prior);
                    _values[p.Id]=rating;SaveProfile(rating);
                }
            }
        }
    }
    public IReadOnlyList<PlayerRating> Snapshot(DateTimeOffset at)
    {
        lock(_gate)return _profiles.Values.Concat(_values.Values).GroupBy(x=>(x.Name,x.ServerId)).Select(g=>Current(g.MaxBy(x=>x.At)!,at)).OfType<PlayerRating>().OrderByDescending(x=>x.At).Take(128).ToArray();
    }
    public void Forget(uint id){lock(_gate){_values.Remove(id);_identities.Remove(id);}}
    public void ClearWorld(){lock(_gate){_values.Clear();_identities.Clear();}}
    public void Clear(){lock(_gate){_values.Clear();_identities.Clear();_profiles.Clear();}}
    public PlayerRating? Find(uint id,string name,DateTimeOffset at)
    {
        lock(_gate)
        {
            PlayerRating? current=id!=0&&_values.TryGetValue(id,out var value)&&value.Name==name?Current(value,at):null;
            if(_identities.TryGetValue(id,out var identity)&&identity.Name==name&&identity.ServerId is {} server&&_profiles.TryGetValue((name,server),out var profile)&&Current(profile,at)is {} cached&&(current is null||cached.At>=current.At))
                return cached with{ActorId=id,Source=cached.Source+" · cached profile"};
            return current;
        }
    }
    /// <summary>Call only for a member of the current verified roster, using its home server when known.</summary>
    public PlayerRating? FindRoster(string name,int server,DateTimeOffset at)
    {
        lock(_gate)return _profiles.TryGetValue((name,server),out var profile)&&Current(profile,at)is {} cached?cached with{ActorId=0,Source=cached.Source+" · cached roster profile"}:null;
    }
}
