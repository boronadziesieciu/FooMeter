using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Settings;

static class StrictCaptureChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-23T12:00:00Z");
    static readonly HashSet<int> NoCustom=[];
    static CombatEvent Hit(uint actor=17,int npc=2400032,bool self=true,bool boss=false,bool dummy=true)=>
        new(T,actor,actor==17?"Hero":"Ally",900,"NPC #"+npc,14010000,100,CombatKind.Damage,
            IsSelf:self,TargetIsBoss:boss,TargetNpcCode:npc,TargetIsTrainingDummy:dummy);
    // Synthetic wire fixtures: regression tests, not verification against a live capture.
    static byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
    static byte[] U(uint n){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,n);return b;}
    static byte[] J(params byte[][] parts)=>parts.SelectMany(p=>p).ToArray();
    static byte[] N(string n){var b=Encoding.UTF8.GetBytes(n);return J([(byte)b.Length],b);}
    static AionFrame F(ushort op,byte[] p)=>new(op,p,false);
    static AionFrame Character()=>F(0x3336,J(V(17),[0,0],N("Hero"),[0x18,5],U(13),[1],U(50)));
    static AionFrame Spawn(uint id,uint npc)=>F(0x4136,J(V(id),[0,0,0],U(npc),new byte[21],V(50000),V(100000),[100,0,0,0,100,0,0,0]));
    static AionFrame Damage(uint actor=17,uint target=900)=>F(0x0438,J(V(target),[4,0],V(actor),U(14010000),[0,0],[1,0,0,0],V(20000),V(100)));
    static AionFrame Appearance(uint actor,string name,byte serverLow=0x17)=>F(0x048d,J(V(900),U(14010000),V(actor),[serverLow,5],N(name)));
    static byte[] Wire(AionFrame f)=>J(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload);
    static byte[] Packet(byte[] payload,uint seq)
    {
        var b=new byte[54+payload.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);
        b[14]=0x45;BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+payload.Length));b[23]=6;
        b[26]=10;b[29]=1;b[30]=10;b[33]=2;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),50001);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),seq);b[46]=0x50;b[47]=0x18;payload.CopyTo(b,54);return b;
    }
    public static void Run(Action<bool,string> check)
    {
        var prefs=new MeterPreferences{PartyOnly=false,Targeting="All targets",HideNames=true,UiScale=1.2};prefs.Normalize();
        check(prefs.CaptureScope=="Self and party"&&prefs.PartyOnly&&prefs.Targeting=="Bosses and dummies"&&prefs.HideNames&&prefs.UiScale==1.2,"v0.8 broad defaults migrate to strict capture without resetting appearance");
        prefs.CaptureScope="Self only";prefs.Normalize();check(prefs.CaptureScope=="Self only","An explicit self-only choice survives normalization");
        prefs.CaptureScope="Everyone";prefs.Normalize();check(prefs.CaptureScope=="Self and party","Invalid scope cannot enable nearby-player capture");

        var catalog=TargetCatalog.Read(Path.Combine(AppContext.BaseDirectory,"data","targets.json"));
        check(catalog.IsTrainingDummy(2400032)&&catalog.NameOf(2400032)=="Training Scarecrow"&&!catalog.IsBoss(2400032),"Bundled screenshot NPC #2400032 resolves to Training Scarecrow, not boss");
        check(catalog.IsBoss(2100003)&&catalog.NameOf(2100003)=="Neikel of the East","Boss identity and English name do not require a berserk duration");
        check(!catalog.IsBoss(2400033)&&!catalog.IsTrainingDummy(2400033)&&catalog.NameOf(2400033)is null,"Adjacent numeric IDs are not guessed to be dummies");
        check(!TargetCatalog.Read("missing-target-catalogue.json").IsTrainingDummy(2400032),"Missing catalogue fails closed rather than admitting unknown targets");
        string file=Path.Combine(AppContext.BaseDirectory,"catalog-"+Guid.NewGuid()+".json");
        try{File.WriteAllText(file,"""{"bosses":{"42":"Partial"},"dummies":null}""");check(!TargetCatalog.Read(file).IsBoss(42),"Malformed catalogue cannot partially publish boss identities");}
        finally{File.Delete(file);}
        var filter=new CaptureFilter();var heal=Hit()with{Kind=CombatKind.Heal,TargetId=17,TargetName="Hero",TargetNpcCode=null,TargetIsTrainingDummy=false};
        check(!filter.Accept(heal,"Self and party",NoCustom),"Healing in town cannot start a boss or dummy encounter");
        check(!filter.Accept(Hit(18,self:false),"Self and party",NoCustom),"Nearby named damage cannot become self or party damage");
        filter.Identity(new(17,"Hero","Ranger",true,1304));filter.Identity(new(18,"Ally","Ranger",false,1303));filter.Identity(new(19,"Ally","Ranger",false,1304));
        filter.Party(new([new("Ally",1303)],T));
        check(filter.Accept(Hit(self:false),"Self only",NoCustom)&&!filter.Accept(Hit(18,self:false),"Self only",NoCustom),"Self-only uses known identity and excludes confirmed party members");
        check(filter.Accept(Hit(18,self:false),"Self and party",NoCustom)&&!filter.Accept(Hit(19,self:false),"Self and party",NoCustom),"Party mode accepts exact name/server matches and rejects same-name outsiders");
        check(filter.Accept(Hit(18,self:false)with{Time=T.AddSeconds(31)},"Self and party",NoCustom)&&filter.Accept(Hit()with{Time=T.AddSeconds(31)},"Self and party",NoCustom),"Roster remains authoritative beyond 30 seconds until replaced or reset");
        check(!filter.Accept(Hit(npc:12,dummy:false)with{TargetName="Training Dummy"},"Self only",NoCustom),"A dummy-looking runtime name cannot bypass target filtering");
        check(!filter.Accept(Hit(npc:12,dummy:false),"Self only",NoCustom)&&filter.Accept(Hit(npc:12,dummy:false),"Self only",new HashSet<int>{12}),"Ordinary targets require an explicit custom dummy ID to count");
        check(filter.Accept(heal with{Time=T.AddSeconds(1)},"Self only",NoCustom),"Healing by eligible players is retained during an admitted dummy encounter");
        filter.EndEncounter();check(!filter.Accept(heal with{Time=T.AddSeconds(2)},"Self only",NoCustom),"Ending or resetting the encounter closes healing admission");
        filter.Accept(Hit(),"Self only",NoCustom);
        check(!filter.Accept(heal with{Time=T.AddSeconds(21)},"Self only",NoCustom),"Dummy healing admission expires after the damage idle timeout");
        filter.Identity(new(20,"Relog","Ranger",true,1304));
        check(!filter.Accept(Hit(self:false),"Self only",NoCustom),"A replacement self identity invalidates the old self entity");
        filter.Reset();check(!filter.Accept(Hit(18,self:false),"Self and party",NoCustom)&&filter.RejectedActors==1,"Connection reset removes membership and resets rejection counters");

        var decoder=new ExperimentalGlobalDecoder(new(new Dictionary<int,int>()),catalog);decoder.Decode("game",Character(),T);
        decoder.Decode("game",Spawn(900,2400032),T);var dummyEvent=decoder.Decode("game",Damage(),T).Single();
        check(dummyEvent is {TargetName:"Training Scarecrow",TargetIsTrainingDummy:true,TargetIsBoss:false,IsSelf:true},"Spawn and damage decoding carry the real dummy ID/name through the live event model");
        decoder.Decode("game",Spawn(901,2100003),T);var bossEvent=decoder.Decode("game",Damage(target:901),T).Single();
        check(bossEvent is {TargetName:"Neikel of the East",TargetIsBoss:true,TargetIsTrainingDummy:false},"A boss without a timing entry remains eligible in the decoder");

        var engine=new CombatEngine();var admitted=new List<CombatEvent>();filter=new();
        decoder=new ExperimentalGlobalDecoder(new(new Dictionary<int,int>()),catalog);
        decoder.IdentityUpdated+=filter.Identity;decoder.PartyUpdated+=filter.Party;decoder.ConnectionChanged+=filter.Reset;
        var pipeline=new NetworkPipeline(decoder,e=>{if(filter.Accept(e,"Self and party",NoCustom)){admitted.Add(e);engine.Add(e);}});
        byte[] data=J(Wire(Character()),Wire(Spawn(900,2400032)),Wire(Spawn(901,2400033)),
            Wire(Appearance(18,"Ally")),Wire(Appearance(19,"Stranger")),
            Wire(F(0x0297,J([0x17,5],N("Ally"),U(13),U(50)))),
            Wire(Damage()),Wire(Damage(18)),Wire(Damage(19)),Wire(Damage(target:901)));
        pipeline.Ingest(1,Packet(data[..7],1000),"adapter",T);
        pipeline.Ingest(1,Packet(data[7..],1007),"adapter",T);
        pipeline.Ingest(1,Packet(data[7..],1007),"adapter",T);
        var result=engine.Snapshot(T.AddSeconds(5));
        check(result.TotalDamage==200&&result.Players.Select(p=>p.ActorId).Order().SequenceEqual(new uint[]{17,18})&&result.TargetName=="Training Scarecrow","Ethernet/TCP/split frames/decoder/filter/engine retain only self and party dummy damage");
        check(pipeline.Diagnostics.Errors==0&&filter.RejectedActors==1&&filter.RejectedTargets==1&&admitted.Count==2,"End-to-end counters explain excluded nearby actors and ordinary targets without duplicate damage");

        var forecasts=new BossForecastTracker(new(new Dictionary<int,int>{{2400032,300}}));
        forecasts.Engage(new(900,true,true,T));forecasts.Add(dummyEvent);forecasts.Add(dummyEvent with{Time=T.AddSeconds(5)});
        var projection=forecasts.Snapshot(T.AddSeconds(5),"Self and party");
        check(projection is {KillSeconds:null,BerserkSeconds:null,MarginSeconds:null}&&projection.Scope.Contains("dummy")&&projection.Status.Contains("Training dummy"),"Dummy targets show measured DPS without fake boss defeat or berserk predictions");
        forecasts.Add(dummyEvent with{ActorId=19,IsSelf=false,Amount=900000,Time=T.AddSeconds(6)});
        check(forecasts.Snapshot(T.AddSeconds(10),"Self and party").DamagePerSecond==20,"Unknown party cannot contaminate strict forecast DPS with nearby actors");
        forecasts.Identity(new(19,"Ally","Ranger",false,1303));forecasts.Party(new([new("Ally",1303)],T));
        check(forecasts.Snapshot(T.AddSeconds(10),"Self only").DamagePerSecond==20,"Self-only forecast excludes even a confirmed ally");
        var retained=new BossForecastTracker(new(new Dictionary<int,int>()));retained.Add(dummyEvent,true);retained.Add(dummyEvent with{ActorId=19,IsSelf=false,Amount=200,Time=T.AddSeconds(5)},true);
        var historyRate=retained.Snapshot(T.AddSeconds(31),"Self and party");
        check(Math.Abs(historyRate.DamagePerSecond-300d/31)<0.000001&&historyRate.Scope.Contains("party"),"Previously admitted party damage stays in the encounter after roster freshness expires");
    }
}
