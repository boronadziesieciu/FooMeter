using AscendAPP.Core.Protocol;
namespace AscendAPP.Core.Combat;
public sealed record PartyDisplayMember(string Name,int ServerId,string? ClassName,int? Level,uint? ActorId,bool IsSelf,string Binding);
/// <summary>Unique roster/name/server join; no per-hit arrays or class-only inference.</summary>
public static class PartyMembership
{
    public static PartyMember? Find(PlayerIdentity actor,PartyRoster roster,IEnumerable<PlayerIdentity> identities)
    {
        PartyMember? found=null;
        for(int i=0;i<roster.Members.Count;i++)
        {
            var member=roster.Members[i];
            if(member.Name!=actor.Name||actor.ClassName is not null&&member.ClassName is not null&&actor.ClassName!=member.ClassName)continue;
            if(actor.ServerId is {} server&&member.ServerId!=server&&member.HomeServerId!=server)continue;
            if(found is not null)return null;
            found=member;
        }
        if(found is null||actor.ServerId.HasValue)return found;
        int matches=0;
        foreach(var identity in identities)if(identity.Name==actor.Name&&++matches>1)return null;
        return matches==1?found:null;
    }
}
