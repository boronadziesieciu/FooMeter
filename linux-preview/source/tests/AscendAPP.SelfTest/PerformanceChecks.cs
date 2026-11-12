using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Game;
using AscendAPP.Core.Settings;
static class PerformanceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var t=DateTimeOffset.Parse("2026-10-23T15:00:00Z");
        CombatEvent Hit(int n,uint actor=17)=>new(t.AddMilliseconds(n*100),actor,"Fixture"+actor,900,"Fixture boss",14010000+n%12,100+n,CombatKind.Damage,IsSelf:actor==17,TargetIsBoss:true);
        var engine=new CombatEngine{KeepBossOpen=true};
        for(int i=0;i<1000;i++)engine.Add(Hit(i));
        var full=engine.Snapshot(t.AddSeconds(100));var light=engine.Snapshot(t.AddSeconds(100),false);
        check(full.TotalDamage==light.TotalDamage&&full.Players.Single().Dps==light.Players.Single().Dps&&full.TopHit==light.TopHit,"Lightweight summaries retain exact damage, DPS and top-hit totals");
        check(light.Players.Single() is {SkillCount:12,Skills.Count:0,Timeline:null}&&full.Players.Single().Timeline!.Count==100,"Lightweight refresh omits full history while retaining skill counts");
        check(engine.Snapshot(t.AddSeconds(101)).Players.Single().Skills.Count==12,"Requesting a summary never erases detailed analysis");
        engine.Finish(t.AddSeconds(102));var completed=engine.Snapshot(t.AddSeconds(103),false);
        check(!completed.IsActive&&completed.Players.Single().Skills.Count==0&&engine.History.Single().Players.Single().Skills.Count==12,"Archived fights retain full details when the visible completed result is lightweight");
        engine.ActiveDps=true;
        check(engine.Snapshot(t.AddSeconds(104),false).Players.Single().Dps==engine.Snapshot(t.AddSeconds(104)).Players.Single().Dps,"Active DPS recalculates equally for completed detailed and lightweight views");

        var forecast=new BossForecastTracker(new BossTimingCatalog(new Dictionary<int,int>()));
        var events=Enumerable.Range(0,1000).Select(i=>Hit(i,(uint)(17+i%4))).ToArray();
        foreach(var e in events)forecast.Add(e,e.ActorId is 17 or 18);
        var identities=new Dictionary<uint,PlayerIdentity>{
            [17]=new(17,"Fixture17","Ranger",true,1304),[18]=new(18,"Fixture18","Ranger",false,1304),
            [19]=new(19,"Fixture19","Ranger",false,2301),[20]=new(20,"Fixture20","Ranger",false,1304)};
        foreach(var identity in identities.Values)forecast.Identity(identity);
        forecast.Party(new([new("Fixture17",1304,"Ranger"),new("Fixture18",1304,"Ranger")],t));
        bool same=true;
        foreach(var scope in new string?[]{null,"Self only","Self and party"})
        foreach(int cut in new[]{1,30,200,500,999,1000,1200})
        {
            var at=t.AddMilliseconds(cut*100);
            var eligible=events.Where(e=>e.Time<=at&&(scope=="Self only"?e.ActorId==17:e.ActorId is 17 or 18));
            double expected=cut<30?0:eligible.Sum(e=>(double)e.Amount)/(cut/10d);
            same&=Math.Abs(forecast.Snapshot(at,scope).DamagePerSecond-expected)<0.000001;
        }
        check(same,"Grouped forecast equals an independent linear calculation for historical and current scopes");
        forecast.Identity(new(20,"Fixture20","Ranger",true,1304));
        double own=events.Where(e=>e.ActorId is 17 or 20).Sum(e=>(double)e.Amount)/120;
        check(Math.Abs(forecast.Snapshot(t.AddSeconds(120),"Self only").DamagePerSecond-own)<0.000001,"Late self metadata reclassifies grouped forecast without losing explicit self flags");
        forecast.Party(new([new("Fixture20",1304,"Ranger")],t));
        double retained=events.Where(e=>e.ActorId is 17 or 18 or 20).Sum(e=>(double)e.Amount)/120;
        check(Math.Abs(forecast.Snapshot(t.AddSeconds(120),"Self and party").DamagePerSecond-retained)<0.000001,"Roster replacement preserves already-admitted damage while applying current metadata");
        forecast.ClearDamage();check(forecast.Snapshot(t.AddSeconds(121)).DamagePerSecond==0,"Clearing forecast damage also clears aggregate totals");
        forecast.ResetEncounter();check(forecast.Snapshot(t.AddSeconds(122)).TargetId is null,"Encounter reset discards forecast aggregate state");
        var capped=new BossForecastTracker(new BossTimingCatalog(new Dictionary<int,int>()));
        for(int i=0;i<=100000;i++)capped.Add(Hit(0) with{Time=t.AddMilliseconds(i),Amount=1},true);
        check(Math.Abs(capped.Snapshot(t.AddMilliseconds(100000),"Self only").DamagePerSecond-1000.02)<0.000001,"Forecast history compaction rebuilds totals exactly at the retention boundary");
        check(Math.Abs(capped.Snapshot(t.AddMilliseconds(60000),"Self only").DamagePerSecond-1000.1)<0.000001,"Historical queries remain exact after forecast history compaction");

        var filter=new CaptureFilter();filter.Identity(new(17,"Fixture17","Ranger",true,1304));filter.Party(new([new("Fixture17",1304,"Ranger")],t));
        var a=filter.Members(t);check(ReferenceEquals(a,filter.Members(t.AddMinutes(1))),"Unchanged party metadata reuses its display snapshot");
        filter.Identity(new(17,"Fixture17","Ranger",true,1304));check(ReferenceEquals(a,filter.Members(t)),"Repeated identical identity metadata does not invalidate party display cache");
        filter.Identity(new(17,"Fixture17","Cleric",true,1304));
        check(!ReferenceEquals(a,filter.Members(t))&&filter.Members(t).Single().ActorId is null,"Changed class metadata invalidates party links and cached display");
        filter.Reset();check(filter.Members(t).Count==0,"Reset cannot reuse cached party membership");
        var p=new MeterPreferences{DummyNpcIds="42;43 42"};
        var ids=p.DummyIds();check(ReferenceEquals(ids,p.DummyIds())&&ids.SetEquals(new[]{42,43}),"Repeated dummy checks reuse a deduplicated immutable set");
        p.DummyNpcIds="44";check(p.DummyIds().SetEquals(new[]{44})&&!ReferenceEquals(ids,p.DummyIds()),"Changing explicit dummy IDs refreshes the cached filter");
        var healing=new HealingReconciler();for(int i=0;i<10;i++)healing.Drain(t);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)healing.Drain(t);
        check(GC.GetAllocatedBytesForCurrentThread()-before<1024,"Empty healing refreshes avoid per-call allocation");
    }
}
