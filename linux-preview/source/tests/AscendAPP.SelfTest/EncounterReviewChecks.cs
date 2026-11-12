using AscendAPP.Core.Combat;
static class EncounterReviewChecks
{
    internal static EncounterSnapshot Fight(DateTimeOffset t,string target="Fixture Boss",int? code=123,long damage=1000,double duration=10,string self="Hero")
        =>new(Guid.NewGuid(),t,duration,false,damage,0,999,[new(17,self,damage,0,999,0,1,10,.2,[],"Ranger",true),new(18,"Private Ally",0,0,0,0,0,1,0,[],"Cleric")],Boss:new(900,code,target,null,null));
    public static void Run(Action<bool,string> check)
    {
        var t=DateTimeOffset.UtcNow;var a=Fight(t);var b=Fight(t.AddMinutes(1),damage:2400,duration:12);
        check(EncounterReview.Search([a,b],"boss").Count==2&&EncounterReview.Search([a,b]," CLERIC ").Count==2,"History search matches targets/classes without case or surrounding-space sensitivity");
        check(EncounterReview.Search([a],"Private Ally",true).Count==0&&EncounterReview.Search([a],"Hero",true).Count==1,"Hidden player names cannot leak through history search while your own name remains searchable");
        check(EncounterReview.Search([a with{IsActive=true}],"").Count==0,"An active fight cannot enter the completed-history comparison list");
        var compare=EncounterReview.Compare(b,a);check(compare.Comparable&&Math.Abs(compare.PartyDpsChange!.Value-1)<1e-9&&Math.Abs(compare.DurationChange!.Value-.2)<1e-9&&compare.SelfDpsChange==1,"History comparison uses recorded totals and full duration, independently of cached active-DPS values");
        check(!EncounterReview.Compare(a,a).Comparable&&!EncounterReview.Compare(b,Fight(t,code:456)).Comparable,"The same encounter and different known NPCs cannot produce a misleading comparison");
        check(!EncounterReview.SameTarget(a,Fight(t,code:null))&&EncounterReview.SameTarget(Fight(t,code:null),Fight(t,code:null)),"A missing NPC ID cannot override a known ID; name-only matching is used only when both IDs are unavailable");
        check(!EncounterReview.Compare(b,a with{DurationSeconds=double.NaN}).Comparable&&!EncounterReview.Compare(b,a with{IsActive=true}).Comparable,"Invalid or still-active baseline data cannot generate percentage results");
        var zero=EncounterReview.Compare(b,Fight(t,damage:0));check(zero.Comparable&&zero.PartyDpsChange is null&&zero.SelfDpsChange is null,"A zero baseline remains unknown rather than an infinite percent improvement");
        check(EncounterReview.Compare(b,Fight(t,self:"Another character")).SelfDpsChange is null,"Different own characters never share a personal-DPS comparison");
    }
}
