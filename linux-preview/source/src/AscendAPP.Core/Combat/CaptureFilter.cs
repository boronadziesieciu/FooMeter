using AscendAPP.Core.Protocol;
namespace AscendAPP.Core.Combat;
/// <summary>Live admission: self or verified party only; damage to explicit bosses/dummies only. Healing needs an admitted encounter.</summary>
public sealed class CaptureFilter
{
    private readonly object _gate=new();
    private readonly Dictionary<uint,PlayerIdentity> _players=new();
    private IReadOnlyList<PartyDisplayMember>? _memberCache;
    private PartyRoster? _party;private DateTimeOffset? _lastDamage;private bool _holdBoss;
    public long RejectedActors {get;private set;}public long RejectedTargets {get;private set;}
    public void Reset(){lock(_gate){_players.Clear();_memberCache=null;_party=null;_lastDamage=null;_holdBoss=false;RejectedActors=RejectedTargets=0;}}
    public void EndEncounter(){lock(_gate){_lastDamage=null;_holdBoss=false;}}
    public void Identity(PlayerIdentity p){lock(_gate){if(_players.TryGetValue(p.Id,out var previous)&&previous==p)return;_memberCache=null;if(_players.Count>=16000&&!_players.ContainsKey(p.Id)){_players.Clear();_party=null;}if(p.IsSelf)foreach(var id in _players.Where(x=>x.Value.IsSelf&&x.Key!=p.Id).Select(x=>x.Key).ToArray())_players.Remove(id);_players[p.Id]=p;}}
    public void Forget(uint id){lock(_gate){if(_players.Remove(id))_memberCache=null;}}
    public void Party(PartyRoster p){lock(_gate){_party=p;_memberCache=null;}}
    public IReadOnlyList<PartyDisplayMember> Members(DateTimeOffset now)
    {
        lock(_gate)
        {
            if(_party is not {} roster||now<roster.ObservedAt)return [];
            if(_memberCache is not null)return _memberCache;
            return _memberCache=roster.Members.Select(m=>{
                var ids=_players.Values.Where(p=>PartyMembership.Find(p,roster,_players.Values)==m).ToArray();
                var p=ids.Length==1?ids[0]:null;
                return new PartyDisplayMember(m.Name,m.HomeServerId??m.ServerId,m.ClassName??p?.ClassName,m.Level,p?.Id,p?.IsSelf==true,
                    p is null?"Waiting for character ID":p.ServerId is null?"Roster + unique nickname":"Roster + name/server");
            }).ToArray();
        }
    }
    public DateTimeOffset? RosterObservedAt {get{lock(_gate)return _party?.ObservedAt;}}
    public bool Accept(CombatEvent e,string scope,IReadOnlySet<int> customDummies,int idleSeconds=20,bool keepBossOpen=true)
    {
        lock(_gate)
        {
            bool known=_players.TryGetValue(e.ActorId,out var actor);
            bool own=e.IsSelf||known&&actor!.IsSelf;
            bool member=scope=="Self and party"&&known&&_party is {} party&&e.Time>=party.ObservedAt&&PartyMembership.Find(actor!,party,_players.Values) is not null;
            if(!own&&!member){RejectedActors++;return false;}
            if(e.Kind==CombatKind.Damage)
            {
                bool dummy=e.TargetIsTrainingDummy||e.TargetNpcCode is {} npc&&customDummies.Contains(npc);
                if(!e.TargetIsBoss&&!dummy){RejectedTargets++;return false;}
                _lastDamage=e.Time;_holdBoss=keepBossOpen&&e.TargetIsBoss&&!dummy;return true;
            }
            if(_lastDamage is {} last&&e.Time>=last&&(_holdBoss||e.Time-last<=TimeSpan.FromSeconds(Math.Clamp(idleSeconds,5,600))))return true;
            RejectedTargets++;return false;
        }
    }
}
