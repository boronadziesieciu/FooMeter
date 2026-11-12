using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
static class DungeonContinuityChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-24T12:00:00Z");
    static byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
    static byte[] U(uint n)=>BitConverter.GetBytes(n);
    static byte[] J(params byte[][] p)=>p.SelectMany(x=>x).ToArray();
    static byte[] N(string n){var b=Encoding.UTF8.GetBytes(n);return J([(byte)b.Length],b);}
    static AionFrame F(ushort op,params byte[][] p)=>new(op,J(p),false);
    static AionFrame Own(uint id=17,string name="DungeonHero")=>F(0x3336,V(id),[0,0],N(name),[0x18,5],U(13),[1],U(50));
    static AionFrame Zone(uint counter,int zone=600001)=>F(0x2136,U(counter),U((uint)zone));
    static AionFrame Party(bool ally=true)=>F(0x0297,[0x18,5],N("DungeonHero"),U(13),U(50),ally?J([0x17,5],N("DungeonAlly"),U(13),U(50)):[]);
    static AionFrame Player(uint id,string name="DungeonAlly",ushort server=1303)=>F(0x048d,V(900),U(14010000),V(id),BitConverter.GetBytes(server),N(name));
    static AionFrame Spawn(uint id=900)=>F(0x4136,V(id),[0,0,0],U(2400032),new byte[21],V(50000),V(100000),[100,0,0,0,100,0,0,0]);
    static AionFrame Hit(uint id)=>F(0x0438,V(900),[4,0],V(id),U(14010000),[0,0],[1,0,0,0],V(20000),V(100));
    static byte[] Wire(AionFrame f)=>J(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload);
    static byte[] Packet(byte[] data,uint seq,ushort port,byte flags=0x18)
    {
        var b=new byte[54+data.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);b[14]=0x45;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+data.Length));b[23]=6;b[26]=10;b[29]=1;b[30]=10;b[33]=2;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),port);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),seq);b[46]=0x50;b[47]=flags;data.CopyTo(b,54);return b;
    }
    public static void Run(Action<bool,string> check)
    {
        check(WorldZoneFacts.IsInstance(600001)==true&&WorldZoneFacts.IsInstance(1010)==false&&WorldZoneFacts.IsInstance(840001)==true&&WorldZoneFacts.IsInstance(777777)is null,"Dungeon classification uses explicit zone-kind facts and preserves unknown zones");
        var d=new ExperimentalGlobalDecoder(targets:new(new Dictionary<int,string>(),new Dictionary<int,string>{{2400032,"Training Scarecrow"}}));
        var filter=new CaptureFilter();var engine=new CombatEngine();var admitted=new List<CombatEvent>();
        d.IdentityUpdated+=filter.Identity;d.EntityRemoved+=filter.Forget;d.PartyUpdated+=filter.Party;
        d.ConnectionChanged+=()=>{filter.Reset();engine.Reset(T);};
        var pipeline=new NetworkPipeline(d,e=>{if(filter.Accept(e,"Self and party",new HashSet<int>())){admitted.Add(e);engine.Add(e);}});
        var sequences=new Dictionary<(ushort,string),uint>();
        void Send(ushort port,double second,params AionFrame[] frames)=>SendSurface(port,second,"adapter",frames);
        void SendSurface(ushort port,double second,string surface,params AionFrame[] frames)
        {
            var key=(port,surface);uint seq=sequences.GetValueOrDefault(key,1000u);byte[] data=J(frames.Select(Wire).ToArray());
            pipeline.Ingest(1,Packet(data,seq,port),surface,T.AddSeconds(second));sequences[key]=seq+(uint)data.Length;
        }
        Send(50001,0,Zone(1,1010),Own(),Party(),Player(18),Spawn(),Hit(17),Hit(18));
        check(admitted.Count==2&&filter.Members(T).Count==2,"Confirmed own and party damage establish the pre-dungeon scope");
        Send(50001,1,Zone(2));
        check(filter.Members(T.AddSeconds(1)).Count==2&&filter.Members(T.AddSeconds(1)).All(m=>m.ActorId is null),"Entering a dungeon retains roster names while dropping old-world actor IDs");
        Send(50001,2,Own(21),Player(22),Spawn(),Hit(21),Hit(22));
        check(admitted.Count==4&&filter.Members(T.AddSeconds(2)).Count(m=>m.ActorId.HasValue)==2,"Fresh dungeon actors bind to the carried roster without a new party list");
        int archived=engine.History.Count;double before=engine.Snapshot(T.AddSeconds(2)).TotalDamage;
        Send(50001,3,Zone(3),Hit(21),Hit(22));
        Send(50001,4,Zone(4),Hit(21),Hit(22));
        check(d.SelfName=="DungeonHero"&&!d.IdentityRefreshPending&&d.InternalMoves==2&&admitted.Count==8,"Internal dungeon teleports keep self and party DPS without resending character, roster or spawn");
        check(engine.History.Count==archived&&engine.Snapshot(T.AddSeconds(4)).TotalDamage==before+400,"Internal teleports preserve the active encounter instead of archiving/resetting it");
        Send(50001,5,Player(99,"NearbyStranger"),Hit(99));
        check(admitted.Count==8,"Keeping the party through doors does not admit nearby non-members");
        Send(50001,6,Zone(4),Hit(22));
        check(d.InternalMoves==2&&admitted.Count==9,"Duplicate zone notices neither reset party tracking nor inflate transition counters");
        Send(50002,7,Zone(1),Spawn(),Hit(21),Hit(22));
        check(filter.Members(T.AddSeconds(7)).Count==2&&filter.Members(T.AddSeconds(7)).All(m=>m.ActorId is null)&&admitted.Count==9,"A new world TCP socket carries party membership while rejecting reused old actor IDs");
        Send(50002,8,Player(31,"DungeonHero",2301),F(0x4536,V(32),[0,0],N("DungeonHero")));
        check(d.SelfName is null&&d.IdentityRefreshPending,"Wrong-server appearance and a nickname alone cannot restore own identity");
        Send(50002,9,Player(31,"DungeonHero",1304),Player(33),Hit(31),Hit(33));
        check(d.SelfName=="DungeonHero"&&d.ProfileRebindings==1&&admitted.Count==11&&admitted[^2].IsSelf,"Fresh exact verified name/server restores self and party scoring after a socket transfer");
        Send(50001,40,Own(17,"RetiredWorld"),Hit(17));
        check(admitted.Count==11&&d.SelfName=="DungeonHero","Delayed data from the retired dungeon socket cannot reclaim identity");
        uint finSeq=sequences[((ushort)50002,"adapter")];pipeline.Ingest(1,Packet([],finSeq,50002,0x11),"adapter",T.AddSeconds(41));
        check(filter.Members(T.AddSeconds(41)).Count==2&&filter.Members(T.AddSeconds(41)).All(m=>m.ActorId is null),"A closing world socket leaves waiting party rows through the loading screen");
        Send(50003,42,Zone(1),Own(41),Player(42),Spawn(),Hit(41),Hit(42));
        check(admitted.Count==13&&filter.Members(T.AddSeconds(42)).Count==2,"FIN before the next socket's zone frame still restores the same dungeon party");
        Send(50003,43,Zone(2,600002));
        check(filter.Members(T.AddSeconds(43)).Count==2&&d.IdentityRefreshPending,"An instance-to-instance stage change preserves the confirmed roster while refreshing combat IDs");
        Send(50003,44,Own(51),Player(52),Spawn(),Hit(51),Hit(52));
        check(admitted.Count==15,"Party DPS resumes after a dungeon stage changes zone ID without a new roster");
        Send(50003,45,Zone(3,1010),Own(61),Player(62),Spawn(),Hit(61),Hit(62));
        check(filter.Members(T.AddSeconds(45)).Count==0&&admitted.Count==16&&admitted[^1].IsSelf,"Leaving the instance clears carried party scope and requires a fresh roster for others");
        Send(50003,46,Party(),Hit(62));
        check(admitted.Count==17,"A real current party list after exit can authorize those players again");
        Send(50003,47,Zone(4),Own(71),Player(72),Spawn(),Hit(72));
        Send(50003,48,Party(false),Zone(5),Hit(72),Hit(71));
        check(admitted.Count==19&&admitted[^1].IsSelf&&filter.Members(T.AddSeconds(48)).Count==1,"A solo replacement roster removes former party members even across another internal teleport");
        Send(50003,49,Party(),Own(81,"OtherCharacter"),Spawn(),Player(82),Hit(82),Hit(81));
        check(filter.Members(T.AddSeconds(49)).Count==0&&admitted.Count==20&&admitted[^1].ActorName=="OtherCharacter","A different own character clears the previous dungeon party");
        Send(50003,50,Party());d.InvalidateContinuity("not-the-active-stream",T.AddSeconds(50));
        check(filter.Members(T.AddSeconds(50)).Count==2,"An unrelated stream gap cannot invalidate the active party");
        d.InvalidateContinuity("adapter|10.0.0.1:13328>10.0.0.2:50003",T.AddSeconds(50));
        Send(50003,50.1,Zone(6));
        check(filter.Members(T.AddSeconds(50.1)).Count==0,"A capture gap revokes roster authority and an internal move cannot resurrect it");
        Send(50003,51,Spawn(81),Hit(81));
        check(d.SelfName is null&&admitted.Count==20,"A recycled own actor ID announced as an NPC cannot keep scoring as self");
        check(pipeline.Diagnostics.Errors==0&&pipeline.LiveDiagnostics is{InternalMoves:>=3,RetainedParties:>0,ProfileRebindings:1},"Pipeline diagnostics expose dungeon continuations, party carries and verified rebindings without raw packets");
        var closed=new ExperimentalGlobalDecoder();var staleFilter=new CaptureFilter();closed.PartyUpdated+=staleFilter.Party;closed.ConnectionChanged+=staleFilter.Reset;
        closed.Decode("old",Zone(1),T,"adapter","old-connection",13328);closed.Decode("old",Own(),T,"adapter","old-connection",13328);closed.Decode("old",Party(),T,"adapter","old-connection",13328);closed.ForgetStream("old");
        closed.Decode("late",Zone(1),T.AddMinutes(3),"adapter","new-connection",13328);
        check(staleFilter.Members(T.AddMinutes(3)).Count==0,"A disconnected transfer anchor expires rather than restoring an indefinitely stale party");
        var changed=new ExperimentalGlobalDecoder();var changedFilter=new CaptureFilter();changed.IdentityUpdated+=changedFilter.Identity;changed.PartyUpdated+=changedFilter.Party;changed.ConnectionChanged+=changedFilter.Reset;
        changed.Decode("old",Zone(1),T,"adapter","first",13328);changed.Decode("old",Own(),T,"adapter","first",13328);changed.Decode("old",Party(),T,"adapter","first",13328);changed.ForgetStream("old");
        changed.Decode("new",Own(81,"OtherCharacter"),T.AddSeconds(1),"adapter","second",13328);
        check(changedFilter.Members(T.AddSeconds(1)).Count==0,"A different character on the first post-FIN frame cannot inherit the waiting party");
        changed.Decode("new",Party(),T.AddSeconds(2),"adapter","second",13328);changed.ForgetStream("new");changed.ExpirePendingWorld(T.AddMinutes(3));
        check(changedFilter.Members(T.AddMinutes(3)).Count==0,"Disconnected waiting rows expire even if no destination world ever arrives");
        var fresh=new ExperimentalGlobalDecoder(targets:new(new Dictionary<int,string>(),new Dictionary<int,string>{{2400032,"Training Scarecrow"}}));fresh.Decode("fresh",Zone(1),T,"adapter","fresh",13328);fresh.Decode("fresh",Player(17,"DungeonHero",1304),T,"adapter","fresh",13328);
        check(fresh.SelfName is null,"An appearance without prior verified own-character provenance cannot create self identity");
        var removedFilter=new CaptureFilter();fresh.IdentityUpdated+=removedFilter.Identity;fresh.PartyUpdated+=removedFilter.Party;fresh.EntityRemoved+=removedFilter.Forget;fresh.ConnectionChanged+=removedFilter.Reset;
        foreach(var frame in new[]{Own(),Party(),Player(18),Spawn()})fresh.Decode("fresh",frame,T,"adapter","fresh",13328);
        fresh.Decode("fresh",F(0x4236,V(17)),T.AddSeconds(1),"adapter","fresh",13328);
        check(fresh.SelfName is null&&!fresh.Decode("fresh",Hit(17),T.AddSeconds(1),"adapter","fresh",13328).Single().IsSelf,"A despawned own ID loses self authority before it can be recycled");
        fresh.Decode("fresh",Zone(2),T.AddSeconds(2),"adapter","fresh",13328);
        var allyHit=fresh.Decode("fresh",Hit(18),T.AddSeconds(2),"adapter","fresh",13328).Single();
        check(removedFilter.Accept(allyHit,"Self and party",new HashSet<int>())&&fresh.IdentityRefreshPending,"Another internal move keeps confirmed party actors even while self is waiting for reappearance");
        fresh.Decode("fresh",Player(27,"DungeonHero",1304),T.AddSeconds(3),"adapter","fresh",13328);
        check(fresh.SelfName=="DungeonHero"&&fresh.Decode("fresh",Hit(27),T.AddSeconds(3),"adapter","fresh",13328).Single().IsSelf,"Fresh exact profile appearance safely restores self after an internal despawn");
    }
}
