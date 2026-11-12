using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;

static class TeleportIdentityChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-23T14:00:00Z");
    // Synthetic layouts from the pinned MIT protocol source. No live teleport capture is implied.
    static byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
    static byte[] U(uint n){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,n);return b;}
    static byte[] J(params byte[][] p)=>p.SelectMany(x=>x).ToArray();
    static byte[] N(string n){var b=Encoding.UTF8.GetBytes(n);return J([(byte)b.Length],b);}
    static AionFrame F(ushort op,byte[] p)=>new(op,p,false);
    static AionFrame Character(uint id=17,string name="FixtureHero",byte server=0x18)=>F(0x3336,J(V(id),[0,0],N(name),[server,5],U(13),[1],U(50)));
    static AionFrame Zone(uint counter=1,uint id=1010)=>F(0x2136,J(U(counter),U(id)));
    static AionFrame Spawn(uint npc=2400032)=>F(0x4136,J(V(900),[0,0,0],U(npc),new byte[21],V(50000),V(100000),[100,0,0,0,100,0,0,0]));
    static AionFrame Damage(uint actor=17)=>F(0x0438,J(V(900),[4,0],V(actor),U(14010000),[0,0],[1,0,0,0],V(20000),V(100)));
    static AionFrame Appearance(uint actor,string name)=>F(0x048d,J(V(900),U(14010000),V(actor),[0x17,5],N(name)));
    static byte[] Wire(AionFrame f)=>J(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload);
    static byte[] Packet(byte[] payload,uint sequence,ushort port)
    {
        var b=new byte[54+payload.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);
        b[14]=0x45;BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+payload.Length));b[23]=6;
        b[26]=10;b[29]=1;b[30]=10;b[33]=2;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),port);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),sequence);b[46]=0x50;b[47]=0x18;payload.CopyTo(b,54);return b;
    }
    static TargetCatalog Catalog()=>new(new Dictionary<int,string>(),new Dictionary<int,string>{{2400032,"Training Scarecrow"}});
    public static void Run(Action<bool,string> check)
    {
        var d=new ExperimentalGlobalDecoder(targets:Catalog());int changes=0;var identities=new List<PlayerIdentity>();
        d.ConnectionChanged+=()=>changes++;d.IdentityUpdated+=identities.Add;
        d.Decode("world",Zone(),T);
        check(d.HasGameStream&&d.IdentityRefreshPending&&d.SelfName is null&&d.ZoneId==1010&&d.ZoneRefreshes==1,"Starting capture in-game can recognize a teleport without login or three clock messages");
        d.Decode("world",F(0x0036,BitConverter.GetBytes(T.ToUnixTimeMilliseconds())),T);
        d.Decode("world",Appearance(17,"NearbyPlayer"),T);
        check(d.SelfName is null&&d.IdentityRefreshPending,"Clocks and nearby appearance cannot become self during a teleport refresh");
        d.Decode("world",Character(),T);
        check(d.SelfName=="FixtureHero"&&!d.IdentityRefreshPending&&d.CharacterRecords==1&&identities.Last()is{Id:17,IsSelf:true},"Fresh own-character record completes a mid-session teleport lookup");
        d.Decode("world",Zone(2),T.AddSeconds(1));
        check(d.SelfName is null&&d.IdentityRefreshPending&&d.ZoneRefreshes==2&&changes==2,"A second teleport in the same zone also invalidates the old self combat ID");
        d.Decode("world",Character(21),T.AddSeconds(2));
        check(d.Decode("world",Damage(21),T.AddSeconds(2)).Single().IsSelf&&!d.Decode("world",Damage(17),T.AddSeconds(2)).Single().IsSelf,"New combat ID becomes self while the previous ID stays unknown");
        d.Decode("world",Zone(2),T.AddSeconds(3));
        check(d.SelfName=="FixtureHero"&&!d.IdentityRefreshPending&&d.ZoneRefreshes==2,"Duplicate counter/zone notice does not erase a recovered character");
        d.Decode("world",Zone(3),T.AddSeconds(4));d.Decode("world",Character(21),T.AddSeconds(5));
        check(identities.Last()is{Id:21,IsSelf:true}&&d.CharacterRecords==3,"Even unchanged combat IDs are republished after each fresh teleport record");
        long rejected=d.Rejected;
        d.Decode("world",F(0x2136,new byte[7]),T.AddSeconds(6));d.Decode("world",Zone(4,uint.MaxValue),T.AddSeconds(6));
        check(d.SelfName=="FixtureHero"&&d.ZoneRefreshes==3&&d.Rejected==rejected+2,"Truncated or invalid zone records cannot erase a valid character");
        d.Decode("world",Character(22,"AltCharacter"),T.AddSeconds(7));
        check(d.SelfName=="AltCharacter"&&d.Decode("world",Damage(21),T.AddSeconds(7)).Single().IsSelf==false,"A real character switch cannot keep the previous self entity");
        d.Reset();check(d.ZoneId is null&&d.ZoneRefreshes==0&&d.CharacterRecords==0&&!d.IdentityRefreshPending,"Capture reset clears teleport diagnostics and pending identity state");

        var energy=new ExperimentalGlobalDecoder();
        energy.Decode("world",F(0x0b61,J([1,4],U(OdyleEnergyReader.TicketId),V(75))),T);
        energy.Decode("world",Character(),T);
        energy.Decode("world",Zone(),T.AddSeconds(1));
        check(energy.Energy is null&&energy.Tickets.Count==0,"Ticket snapshots are hidden while the post-teleport owner is unknown");
        energy.Decode("world",Character(21),T.AddSeconds(2));
        check(energy.Energy?.Available==75,"Same verified name/server keeps its observed ticket balance after ID rebinding");
        energy.Decode("world",Zone(2),T.AddSeconds(3));energy.Decode("world",Character(22,"AltCharacter"),T.AddSeconds(4));
        check(energy.Energy is null&&energy.Tickets.Count==0,"Different character cannot inherit the previous character's post-teleport tickets");

        var filter=new CaptureFilter();var engine=new CombatEngine();var events=new List<CombatEvent>();
        d=new ExperimentalGlobalDecoder(targets:Catalog());
        d.IdentityUpdated+=filter.Identity;d.PartyUpdated+=filter.Party;
        d.ConnectionChanged+=()=>{filter.Reset();engine.Reset(T);};
        var pipeline=new NetworkPipeline(d,e=>{if(filter.Accept(e,"Self and party",new HashSet<int>())){events.Add(e);engine.Add(e);}});
        var sequences=new Dictionary<(ushort Port,string Surface),uint>();
        void Send(ushort port,double second,byte[] data,string surface="adapter")
        {
            var key=(port,surface);uint seq=sequences.GetValueOrDefault(key,1000u);
            pipeline.Ingest(1,Packet(data,seq,port),surface,T.AddSeconds(second));sequences[key]=seq+(uint)data.Length;
        }
        Send(50001,0,J(Wire(Character()),Wire(Spawn()),Wire(Damage())));
        check(events.Count==1&&pipeline.Diagnostics.SelfKnown,"Pre-teleport own damage reaches strict live ingestion");
        Send(50002,1,Wire(Zone(1,600001)));
        check(pipeline.LiveDiagnostics is{ZoneId:600001,IdentityRefreshPending:true,SelfName:null}&&!pipeline.Diagnostics.SelfKnown&&engine.History.Count==1,"New world TCP connection immediately starts refresh while the old socket is still alive");
        Send(50002,2,J(Wire(Spawn()),Wire(Appearance(17,"NearbyPlayer")),Wire(Damage(17))));
        check(events.Count==1&&engine.Snapshot(T.AddSeconds(2)).Players.Count==0,"Old ID reuse and nearby damage remain excluded before character re-identification");
        byte[] own=J(Wire(Character(21)),Wire(Damage(21)));
        var key=(Port:(ushort)50002,Surface:"adapter");uint seq=sequences[key];
        pipeline.Ingest(1,Packet(own[..4],seq,50002),"adapter",T.AddSeconds(3));
        check(pipeline.LiveDiagnostics?.IdentityRefreshPending==true,"A partial character frame cannot prematurely complete teleport identity");
        pipeline.Ingest(1,Packet(own[4..],seq+4,50002),"adapter",T.AddSeconds(3));
        pipeline.Ingest(1,Packet(own[4..],seq+4,50002),"adapter",T.AddSeconds(3));sequences[key]=seq+(uint)own.Length;
        check(events.Count==2&&events.Last()is{ActorId:21,IsSelf:true}&&pipeline.LiveDiagnostics is{IdentityRefreshPending:false,CharacterRecords:2},"Split TCP character record rebinds self and subsequent damage exactly once");
        var reverse=Packet(Wire(Zone(99,9999)),1000,13328);BinaryPrimitives.WriteUInt16BigEndian(reverse.AsSpan(34),50002);
        pipeline.Ingest(1,reverse,"adapter",T.AddSeconds(3.5));
        check(pipeline.LiveDiagnostics is{ZoneId:600001,IdentityRefreshPending:false,ZoneRefreshes:1},"Opposite-direction zone-shaped traffic cannot take over a live server stream");
        Send(50002,4,J(Wire(Character(99,"DuplicateSurface")),Wire(Damage(99))),"other-adapter");
        check(events.Count==2&&pipeline.LiveDiagnostics?.SelfName=="FixtureHero","Another capture adapter cannot steal the new world identity or duplicate damage");
        Send(50002,5,Wire(Zone(2,600001)));
        Send(50002,6,J(Wire(Character(31)),Wire(Damage(31))));
        check(events.Count==2&&pipeline.LiveDiagnostics is{ZoneRefreshes:2,IdentityRefreshPending:false},"Same-zone teleport refreshes self but does not reuse the previous world's target classification");
        Send(50002,7,J(Wire(Spawn()),Wire(Damage(31))));
        check(events.Last()is{ActorId:31,TargetName:"Training Scarecrow",TargetIsTrainingDummy:true}&&events.Count==3,"Fresh post-teleport target spawn restores eligible self DPS");
        Send(50001,50,J(Wire(Character(17,"OldWorld")),Wire(Damage(17))));
        check(events.Count==3&&pipeline.LiveDiagnostics?.SelfName=="FixtureHero","Late retired-world login traffic cannot reclaim self even after the activity timeout");
        Send(50002,51,Wire(F(0x0297,J([0x17,5],N("PartyMate"),U(13),U(50)))));
        Send(50002,51,J(Wire(Appearance(32,"PartyMate")),Wire(Damage(32))));
        check(events.Count==4&&events.Last().ActorId==32,"A newly observed roster admits confirmed party damage in the destination world");
        Send(50002,52,Wire(Zone(3,1010)));
        Send(50002,53,J(Wire(Character(41)),Wire(Spawn()),Wire(Appearance(32,"PartyMate")),Wire(Damage(32)),Wire(Damage(41))));
        check(events.Count==5&&events.Last().ActorId==41,"Teleport clears previous party membership while retaining fresh own DPS");
        check(pipeline.Diagnostics.Errors==0&&pipeline.LiveDiagnostics is{ZoneId:1010,ZoneRefreshes:3,CharacterRecords:4},"Teleport diagnostic counters reflect accepted zone and own-record transitions without packet bodies");
    }
}
