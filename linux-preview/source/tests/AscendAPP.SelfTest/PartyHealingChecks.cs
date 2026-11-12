using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Storage;

static class PartyHealingChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-23T12:00:00Z");
    static byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
    static byte[] U(uint n){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,n);return b;}
    static byte[] J(params byte[][] parts)=>parts.SelectMany(p=>p).ToArray();
    static byte[] N(string s){var b=Encoding.UTF8.GetBytes(s);return J([(byte)b.Length],b);}
    static AionFrame F(ushort op,params byte[][] b)=>new(op,J(b),false);
    static AionFrame Own()=>F(0x3336,V(17),[0,0],N("Hero"),[0x18,5],U(13),[1],U(50));
    static AionFrame Roster()=>F(0x0297,[0x18,5],N("Hero"),U(13),U(50),[0x17,5],N("Azaëde"),U(32),U(45));
    static CombatEvent Hit(double seconds=1,uint actor=18)=>new(T.AddSeconds(seconds),actor,"Azaëde",900,"Training dummy",17240000,100,CombatKind.Damage,TargetIsTrainingDummy:true);
    static CombatEvent Heal(double seconds=1,long amount=200,uint actor=17)=>new(T.AddSeconds(seconds),actor,"Hero",18,"Azaëde",17240000,amount,CombatKind.Heal);
    static TargetHealth Hp(double seconds,long hp,long? max=1000)=>new(18,hp,max,T.AddSeconds(seconds));

    public static void Run(Action<bool,string> check)
    {
        var d=new ExperimentalGlobalDecoder();var filter=new CaptureFilter();PartyRoster? party=null;
        d.IdentityUpdated+=filter.Identity;d.EntityRemoved+=filter.Forget;d.PartyUpdated+=p=>{party=p;filter.Party(p);};d.ConnectionChanged+=filter.Reset;
        d.Decode("game",Own(),T);d.Decode("game",Roster(),T);
        check(party?.Members.Single(m=>m.Name=="Azaëde") is {ClassName:"Cleric",Level:45,ServerId:1303},"Roster decodes UTF-8 byte lengths, roster class and level from pinned MIT layout");
        check(filter.Members(T).Count==2&&filter.Members(T).Single(m=>m.Name=="Azaëde").ActorId is null,"Roster exposes a member before any nickname or damage, without inventing a combat ID");
        d.Decode("game",F(0x4536,V(18),[0,0],N("Azaëde")),T.AddMilliseconds(10));
        check(filter.Accept(Hit(),"Self and party",new HashSet<int>()),"Unique nickname joins the server-bearing party roster even when nickname has no server");
        check(filter.Members(T.AddSeconds(1)).Single(m=>m.Name=="Azaëde") is {ActorId:18,ClassName:"Cleric",Binding:"Roster + unique nickname"},"Nickname binding carries real roster class and explains provenance");
        check(filter.Accept(Hit(120),"Self and party",new HashSet<int>()),"Quiet unchanged parties remain visible and eligible after two minutes");
        d.Decode("game",F(0x0197,[0x18,5],N("Stranger"),U(32),U(50)),T.AddSeconds(121));
        check(party?.Members.Count==2&&d.PartyLists==1,"Other-parties list 0197 never grants local party membership");
        d.Decode("game",F(0x0297,[0xff,0xff,1]),T.AddSeconds(122));
        check(d.RejectedPartyLists==1&&d.PartyLists==1&&party?.ObservedAt==T,"Malformed roster cannot replace or refresh the last valid snapshot");
        var wrongClass=d.Decode("game",F(0x0438,V(900),[4,0],V(18),U(14010000),[0,0],[1,0,0,0],V(20000),V(100)),T.AddSeconds(122.1)).Single();
        check(!filter.Accept(wrongClass with{TargetIsTrainingDummy=true},"Self and party",new HashSet<int>()),"Actual skill class can contradict a provisional roster nickname rather than counting a namesake");
        filter.Identity(new(18,"Azaëde","Cleric",false));
        filter.Identity(new(19,"Azaëde","Cleric",false,2301));
        check(!filter.Accept(Hit(123),"Self and party",new HashSet<int>()),"Same-name server collision blocks unresolved nickname membership");
        filter.Identity(new(18,"Azaëde","Cleric",false,1303));
        check(filter.Accept(Hit(124),"Self and party",new HashSet<int>())&&!filter.Accept(Hit(124,19),"Self and party",new HashSet<int>()),"Explicit server disambiguates party member from namesake outsider");
        d.Decode("game",F(0x0297,[0x18,5],N("Hero"),U(13),U(50)),T.AddSeconds(125));
        check(!filter.Accept(Hit(126),"Self and party",new HashSet<int>())&&filter.Members(T.AddSeconds(126)).Count==1,"Self-only roster immediately removes departed members");
        d.Decode("game",Roster(),T.AddSeconds(127));d.InvalidateContinuity("game",T.AddSeconds(128));
        check(filter.Members(T.AddSeconds(129)).Count==0&&!filter.Accept(Hit(129),"Self and party",new HashSet<int>()),"Lost TCP continuity invalidates party membership rather than retaining a missed leave");
        filter.Identity(new(18,"Azaëde","Cleric",false,2303));filter.Party(new([new("Azaëde",2306,"Cleric",45,2303)],T));
        check(filter.Accept(Hit(),"Self and party",new HashSet<int>()),"Roster home server and wire server alias both identify the same member");

        d.Decode("game",Roster(),T.AddSeconds(130));
        d.Decode("game",F(0x4236,V(18)),T.AddSeconds(131));
        check(filter.Members(T.AddSeconds(132)).Single(m=>m.Name=="Azaëde").ActorId is null,"Despawn removes the old combat identity while retaining the roster member");
        d.Decode("game",F(0x4236,V(19)),T.AddSeconds(132));
        d.Decode("game",F(0x4536,V(20),[0,0],N("Azaëde")),T.AddSeconds(133));
        check(filter.Accept(Hit(134,20),"Self and party",new HashSet<int>()),"Reappearing party member can bind to a new nickname ID without the despawned ID blocking it");

        var h=new HealingReconciler();h.Observe(Hp(0,1000));h.Add(Heal(),true);
        check(h.Drain(T.AddSeconds(1.4)).Count==0,"Healing reconciliation waits for the HP update window");
        var full=h.Drain(T.AddSeconds(1.6)).Single();
        check(full is {EffectiveHealing:0}&&full.ScoredAmount==0&&full.Amount==200,"Full-health heal adds zero score and preserves its raw wire amount");
        h.Reset();h.Observe(Hp(0,900));h.Add(Heal(amount:300),true);h.Observe(Hp(1.1,1000));
        var partial=h.Drain(T.AddSeconds(2)).Single();
        check(partial.ScoredAmount==100&&partial.Amount==300,"Partial overheal scores only the observed 100 HP restored");
        h.Add(Heal(2,100),true);var subsequent=h.Drain(T.AddSeconds(3)).Single();
        check(subsequent.ScoredAmount==0,"The next heal sees the restored full HP rather than reusing the old deficit");
        h.Reset();h.Add(Heal(),true);var unknown=h.Drain(T.AddSeconds(2)).Single();
        check(unknown.EffectiveHealing is null&&unknown.ScoredAmount==0&&unknown.HealingAssessment!.StartsWith("Unverified"),"Unknown HP remains unverified and never falls back to raw potency");
        h.Reset();h.Observe(Hp(-5,700));h.Add(Heal(),true);h.Observe(Hp(1.1,900));
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"Stale pre-heal HP cannot establish effective healing");
        h.Reset();h.Observe(Hp(1,1000));h.Add(Heal(),true);
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"A same-time or post-heal full-HP update alone cannot erase a useful heal");
        h.Reset();h.Observe(Hp(0,600));h.Add(Heal(),true);
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"Missing post-heal HP does not assume the heal restored its full amount");
        h.Reset();h.Observe(Hp(0,600));h.Add(Heal(),true);h.Add(Heal(1.1,200,19),false);h.Observe(Hp(1.2,1000));
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"Overlapping outsider healing prevents misattributing the whole HP increase to self");
        h.Reset();h.Observe(Hp(0,700));h.Damage(18,T.AddSeconds(1.05));h.Add(Heal(),true);h.Observe(Hp(1.1,800));
        check(h.Drain(T.AddSeconds(2)).Single().HealingAssessment=="Unverified: intervening damage","Concurrent damage makes net HP change unverified rather than a fabricated effective-heal value");
        h.Reset();h.Observe(Hp(0,700));h.Add(Heal(),true);h.Observe(Hp(1.1,900,1200));
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"A changed HP maximum makes healing attribution ambiguous");
        h.Reset();h.Observe(Hp(0,700));h.Add(Heal(),true);h.Observe(Hp(1.1,1000));
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"An HP gain greater than the wire heal is not assigned to that healer");
        h.Reset();h.Observe(Hp(0,700));h.Observe(Hp(1.1,800));h.Observe(Hp(1.1,900));h.Add(Heal(),true);
        check(h.Drain(T.AddSeconds(2)).Single().HealingAssessment=="Unverified: HP ordering ambiguous","Conflicting HP values in one capture timestamp cannot be attributed in an invented order");
        h.Reset();h.Observe(Hp(0,800));h.Add(Heal() with{Tags=HitTags.Dot},true);h.Observe(Hp(1.1,900));
        var hot=h.Drain(T.AddSeconds(2)).Single();
        check(hot.ScoredAmount==100&&hot.Tags==HitTags.Dot,"Heal-over-time ticks use the same effective-healing rule as direct heals");
        check(h.Diagnostics is {Raw:200,Scored:100,Excluded:100,Unverified:0,Pending:0},"Diagnostics separate raw, scored, excluded, unknown and pending healing");
        h.Add(Heal(3),true);h.ClearPending();
        check(h.Drain(T.AddSeconds(4)).Count==0,"Encounter reset cannot leak pending heals into the next pull");
        h.Reset();check(h.Diagnostics==new HealingDiagnostics(0,0,0,0,0),"World reset discards healing provenance and counters");

        var engine=new CombatEngine();engine.Add(Hit(0,17) with{ActorName="Hero",IsSelf=true});
        engine.Add(Hit(1.2,17) with{ActorName="Hero",IsSelf=true});
        engine.Add(partial);engine.Add(unknown with{Time=T.AddSeconds(1.3)});engine.Add(full with{Time=T.AddSeconds(1.4)});
        var snapshot=engine.Snapshot(T.AddSeconds(2));var p=snapshot.Players.Single();
        check(p.Healing==100&&snapshot.TotalHealing==100&&p.RawHealing==700&&p.ExcludedHealing==400&&p.UnverifiedHealing==200,"Engine, player and encounter totals share effective healing with separate excluded and unknown amounts");
        check(p.Skills.Single().MaxHeal==100&&p.Skills.Single().HealingHits==1,"Zero/unknown heals do not inflate skill heal counts or maximum heal");
        engine.Finish(T.AddSeconds(2));engine.Add(partial with{Time=T.AddSeconds(3)});
        check(engine.Snapshot(T.AddSeconds(4)).TotalHealing==100&&!engine.Snapshot(T.AddSeconds(4)).IsActive,"A late heal cannot resurrect an ended encounter");
        engine.Reset(T);engine.Add(Hit(0,17));engine.Add(Hit(1,17));engine.Add(full with{Time=T.AddSeconds(10)});engine.Add(unknown with{Time=T.AddSeconds(11)});
        check(!engine.Snapshot(T.AddSeconds(22)).IsActive,"Full-health and unknown heals do not prolong dummy fights");
        string file=Path.Combine(Path.GetTempPath(),"ascend-healing-"+Guid.NewGuid()+".jsonl");
        try{JsonlCombatSession.Write(file,[partial,full,unknown]);var replay=JsonlCombatSession.Read(file);check(replay.Sum(e=>e.ScoredAmount)==100&&replay.Sum(e=>e.Amount)==700,"JSONL retains raw values and healing assessment for deterministic replay");}finally{File.Delete(file);}
        h.Observe(Hp(0,1000));h.Forget(18);h.Add(Heal(),true);
        check(h.Drain(T.AddSeconds(2)).Single().EffectiveHealing is null,"Reused/despawned entity IDs cannot inherit full-health healing provenance");
        var legacy=new CombatEngine();legacy.Add(Heal());
        check(legacy.Snapshot(T.AddSeconds(2)).Players.Single() is {Healing:200,RawHealing:200,LegacyHealing:200},"Legacy replay healing remains explicit raw data instead of being relabelled HP-confirmed");
        d=new();d.Decode("game",Own(),T);
        int damagedTargets=0;d.TargetDamaged+=(id,at)=>{if(id==18)damagedTargets++;};
        d.Decode("game",F(0x0438,V(18),[4,0],V(900),U(1604660),[0,0],[1,0,0,0],V(20000),V(100)),T.AddSeconds(1));
        check(damagedTargets==1,"Incoming monster damage invalidates healing correlation even when the monster is not ranked");
        
        check(d.Decode("game",F(0x0538,V(17),[9],V(17),V(1),U(1724000011),V(334),U(17240000)),T.AddSeconds(1)).Count==0,"HoT initial 0x09 announcement is not counted as an actual heal tick");
        check(d.Decode("game",F(0x0538,V(900),[10],V(17),V(1),U(1401000011),V(250),U(14010000)),T.AddSeconds(2)).Single() is {Kind:CombatKind.Damage,Amount:250},"Actual damage-over-time remains damage regardless of healer health");
    }
}
