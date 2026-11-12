using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;

static class EnergyForecastChecks
{
    static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-23T12:00:00Z");
    static byte[] V(uint n) { var b=new List<byte>(); do { byte x=(byte)(n&127); n>>=7; b.Add((byte)(x|(n>0?128:0))); } while(n>0); return b.ToArray(); }
    static byte[] U32(uint n) { var b=new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b,n); return b; }
    static byte[] I64(long n) { var b=new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b,n); return b; }
    static byte[] J(params byte[][] parts)=>parts.SelectMany(x=>x).ToArray();
    static byte[] N(string n) { var b=Encoding.UTF8.GetBytes(n); return J([(byte)b.Length],b); }
    static byte[] Entry(byte mask,uint id,uint regular=0,uint bonus=0)=>J([mask],U32(id),(mask&1)!=0?new byte[8]:[],(mask&2)!=0?new byte[8]:[],(mask&4)!=0?V(regular):[],(mask&8)!=0?V(bonus):[]);
    static AionFrame F(ushort op,byte[] b)=>new(op,b,false);
    static AionFrame Character(uint id=17,string name="Hero")=>F(0x3336,J(V(id),[0,0],N(name),[0x18,5],U32(13),[1],U32(50)));
    static CombatEvent Hit(double second,uint actor=17,uint target=99,long amount=10000,int npc=2300000,long? hp=null,DateTimeOffset? observed=null)=>new(T.AddSeconds(second),actor,actor==17?"Hero":"Ally",target,"Boss",14010000,amount,CombatKind.Damage,IsSelf:actor==17,TargetNpcCode:npc,TargetCurrentHp:hp,TargetMaxHp:hp.HasValue?100000:null,TargetHpObservedAt:observed);
    static BossTimingCatalog Catalog()=>new(new Dictionary<int,int>{{2300000,300}});
    public static void Run(Action<bool,string> check)
    {
        byte[] list=J([2],Entry(1,3),Entry(12,OdyleEnergyReader.TicketId,75,520));
        check(OdyleEnergyReader.TryReadList(list,T,out var energy)&&energy is {Regular:75,Bonus:520,Available:595},"Odyle list decodes regular and bonus separately");
        check(OdyleEnergyReader.TryReadList(J([1],Entry(0,OdyleEnergyReader.TicketId)),T,out energy)&&energy?.Available==0,"Explicit zero energy is zero rather than unknown");
        check(OdyleEnergyReader.TryReadList(J([1],Entry(15,OdyleEnergyReader.TicketId,540,10)),T,out energy)&&energy?.Available==550,"Optional ticket time fields preserve alignment");
        check(!OdyleEnergyReader.TryReadList(list[..^1],T,out _),"Truncated ticket snapshot rejected");
        check(!OdyleEnergyReader.TryReadList(J(list,[0]),T,out _),"Ticket list requires exact consumption");
        check(!OdyleEnergyReader.TryReadList(J([1],Entry(16,OdyleEnergyReader.TicketId)),T,out _),"Unknown ticket mask rejected");
        check(!OdyleEnergyReader.TryReadList(J([2],Entry(4,OdyleEnergyReader.TicketId,1),Entry(4,OdyleEnergyReader.TicketId,2)),T,out _),"Duplicate energy entries rejected");
        byte[] update=J([1],Entry(12,OdyleEnergyReader.TicketId,70,530),[1],U32(10));
        check(OdyleEnergyReader.TryReadUpdate(update,T,out energy)&&energy?.Available==600,"Gain update is a snapshot, not a second additive gain");
        check(!OdyleEnergyReader.TryReadUpdate(update[..^1],T,out _),"Truncated energy gain rejected");
        check(!OdyleEnergyReader.TryReadUpdate(J(update,[0]),T,out _),"Energy update trailer rejected");
        var d=new ExperimentalGlobalDecoder();
        d.Decode("game",F(0x0b61,list),T);
        check(d.Energy is null,"Early energy cannot bind before character identity");
        d.Decode("game",Character(),T.AddSeconds(3));
        check(d.Energy?.Available==595&&d.SelfName=="Hero","Pre-login energy reaches the matching character");
        d.Decode("game",F(0x0c61,update),T.AddSeconds(4));
        check(d.Energy?.Available==600,"Live energy change reaches the active stream");
        d.Decode("unrelated",F(0x0c61,J([0],Entry(4,OdyleEnergyReader.TicketId,999),[2])),T.AddSeconds(5));
        check(d.Energy?.Available==600,"Other capture surface cannot overwrite character energy");
        d.Decode("game",F(0x0b61,J([1],Entry(4,OdyleEnergyReader.TicketId,42))),T.AddSeconds(6));
        check(d.Energy is null,"Character transition hides pending energy");
        d.Decode("game",Character(18,"OtherHero"),T.AddSeconds(7));
        check(d.Energy?.Available==42&&d.SelfName=="OtherHero","Character switch cannot retain prior energy balance");
        d.Reset(); check(d.Energy is null,"Capture reset clears energy");
        var network = new NetworkPipeline(new ExperimentalGlobalDecoder(), _=>{});
        byte[] Wire(AionFrame frame)=>J(V((uint)frame.Payload.Length+6),[(byte)(frame.Opcode>>8),(byte)frame.Opcode],frame.Payload);
        byte[] Packet(byte[] data,uint sequence)
        {
            var packet=new byte[54+data.Length];
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(12),0x0800);
            packet[14]=0x45; BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(16),(ushort)(40+data.Length));
            packet[23]=6; packet[26]=10; packet[29]=1; packet[30]=10; packet[33]=2;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(34),13328); BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(36),50001);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(38),sequence); packet[46]=0x50; packet[47]=0x18;
            data.CopyTo(packet,54); return packet;
        }
        byte[] earlyTicket=Wire(F(0x0b61,list));
        network.Ingest(1,Packet(earlyTicket,1000),"adapter",T);
        check(network.Frames==1 && network.LiveDiagnostics?.Energy is null,"Ticket-only TCP frame is retained without qualifying an unrelated game stream");
        byte[] characterWire=Wire(Character());
        network.Ingest(1,Packet(characterWire[..3],(uint)(1000+earlyTicket.Length)),"adapter",T.AddSeconds(3));
        network.Ingest(1,Packet(characterWire[3..],(uint)(1003+earlyTicket.Length)),"adapter",T.AddSeconds(3));
        check(network.LiveDiagnostics?.Energy?.Available==595 && network.Diagnostics.Errors==0,"Ethernet through split TCP framing to diagnostics retains pre-login energy");
        BossEngagement? engagement=null; PartyRoster? party=null;
        d.EngagementUpdated+=e=>engagement=e; d.PartyUpdated+=p=>party=p;
        d.Decode("game",Character(),T);
        d.Decode("game",F(0x218d,J(V(99),[1,1],I64(T.ToUnixTimeMilliseconds()))),T.AddSeconds(1));
        check(engagement is {ExactStart:true,InCombat:true}&&engagement.At==T,"Exact combat start uses the validated server timestamp");
        d.Decode("game",F(0x218d,J(V(99),[0,1])),T);
        check(engagement is {ExactStart:false,InCombat:true},"Plain combat notice remains approximate");
        d.Decode("game",F(0x218d,J(V(99),[0,0])),T);
        check(engagement is {InCombat:false},"Disengage parsed separately from timer expiration");
        d.Decode("game",F(0x0297,J([0x18,5],N("Hero"),U32(13),U32(50),[0x17,5],N("Ally"),U32(25),U32(45))),T);
        check(party?.Members is {Count:2} && party.Members.Any(p=>p.Name=="Ally"&&p.ServerId==1303),"Party roster retains server and character identity");

        var f=new BossForecastTracker(Catalog());
        f.Engage(new(99,true,true,T));
        f.Add(Hit(0)); f.Add(Hit(5));
        f.Health(99,80000,100000,T.AddSeconds(10));
        var snap=f.Snapshot(T.AddSeconds(10));
        check(snap.DamagePerSecond==2000&&snap.KillSeconds==40,"Time to defeat is current target HP divided by target-specific damage rate");
        check(snap.BerserkSeconds==290&&snap.MarginSeconds==250,"Berserk countdown and time margin use the observed pull clock");
        f.Add(Hit(6,target:100,amount:900000,npc:123));
        check(f.Snapshot(T.AddSeconds(10)).KillSeconds==40,"Damage to adds cannot inflate boss defeat estimate");
        f.Add(Hit(7) with {Kind=CombatKind.Heal});
        check(f.Snapshot(T.AddSeconds(10)).KillSeconds==40,"Healing never enters boss damage rate");
        f.Add(Hit(8,actor:22,amount:10000)); f.Add(Hit(9,actor:23,amount:90000));
        f.Identity(new(22,"Ally","Sorcerer",false,1304)); f.Identity(new(23,"Ally","Sorcerer",false,2301));
        f.Party(new([new("Hero",1304),new("Ally",1304)],T.AddSeconds(9)));
        snap=f.Snapshot(T.AddSeconds(10));
        check(snap.DamagePerSecond==3000&&snap.Scope.StartsWith("Observed party"),"Party boss damage excludes a same-name outsider on another server");
        check(f.Snapshot(T.AddSeconds(50)).Scope.StartsWith("Observed party"),"Roster snapshot remains authoritative until the next membership or world change");
        check(f.Snapshot(T.AddSeconds(19)).KillSeconds is null,"Stale health disables defeat projection");
        f.ClearDamage(); check(f.Snapshot(T.AddSeconds(20)).KillSeconds is null&&f.Snapshot(T.AddSeconds(20)).BerserkSeconds==280,"Damage reset preserves known berserk clock without inventing new DPS");
        f.Add(Hit(21,hp:80000,observed:T)); f.Add(Hit(25,hp:80000,observed:T));
        check(f.Snapshot(T.AddSeconds(26)).KillSeconds is null,"Repeated cached HP in combat records cannot refresh health freshness");
        f.Health(99,0,100000,T.AddSeconds(27));
        check(f.Snapshot(T.AddSeconds(27)) is {KillSeconds:0,BerserkSeconds:null,Status:"Target defeated"},"Dead boss ends projections and countdown");
        f.Health(99,100000,100000,T.AddSeconds(30));
        check(f.Snapshot(T.AddSeconds(30)).BerserkSeconds is null,"Full health reset does not reuse previous pull clock");
        var unknown=new BossForecastTracker(Catalog()); unknown.Add(Hit(0,npc:123)); unknown.Add(Hit(5,npc:123)); unknown.Health(99,50000,100000,T.AddSeconds(5)); unknown.Engage(new(99,true,true,T));
        check(unknown.Snapshot(T.AddSeconds(5)).BerserkSeconds is null,"Unlisted boss never gets a fabricated 300-second timer");
        var late=new BossForecastTracker(Catalog());late.Add(Hit(0));late.Add(Hit(5));late.Health(99,50000,100000,T.AddSeconds(5));
        check(late.Snapshot(T.AddSeconds(5)).BerserkSeconds is null,"Starting mid-pull cannot infer start time from first damage alone");
        late.Engage(new(99,true,false,T)); check(late.Snapshot(T.AddSeconds(5)).BerserkBasis.StartsWith("Approximate"),"Approximate combat notice basis remains explicit");
        var early=new BossForecastTracker(Catalog()); early.Add(Hit(0,hp:100000,observed:T));
        check(early.Snapshot(T.AddSeconds(1)).KillSeconds is null,"One hit does not produce an unstable defeat estimate");
        var cleared=new BossForecastTracker(Catalog()); cleared.Engage(new(99,true,true,T));
        cleared.Add(Hit(0)); cleared.Add(Hit(5)); cleared.Health(99,50000,100000,T.AddSeconds(5));
        cleared.ClearDamage(); cleared.Health(99,100000,100000,T.AddSeconds(6));
        check(cleared.Snapshot(T.AddSeconds(6)).BerserkSeconds is null,"A real full-health boss reset clears the pull clock after manual damage reset");
        var lateSelf=new BossForecastTracker(Catalog()); lateSelf.Add(Hit(0,actor:44)); lateSelf.Add(Hit(5,actor:44));
        lateSelf.Health(99,50000,100000,T.AddSeconds(10)); lateSelf.Identity(new(44,"Hero","Ranger",true,1304));
        lateSelf.Party(new([new("Ally",1304)],T.AddSeconds(10)));
        check(lateSelf.Snapshot(T.AddSeconds(10)).DamagePerSecond==2000,"Late self identity includes earlier own damage even when roster omits self");
        var rng=new Random(7);
        for(int i=0;i<10000;i++){var bad=new byte[rng.Next(0,60)];rng.NextBytes(bad);OdyleEnergyReader.TryReadList(bad,T,out _);OdyleEnergyReader.TryReadUpdate(bad,T,out _);}
        check(true,"Ten thousand malformed ticket cases complete without exception");
    }
}
