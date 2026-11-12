using System.Text;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
internal static class RatingReviewChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-24T18:00:00Z");
    static byte[] V(uint n)=>WireFixture.V(n);
    static byte[] J(params byte[][] p)=>p.SelectMany(x=>x).ToArray();
    static byte[] N(string n){var b=Encoding.UTF8.GetBytes(n);return J([(byte)b.Length],b);}
    static byte[] U(int n)=>BitConverter.GetBytes(n);
    static byte[] S(int n)=>BitConverter.GetBytes((ushort)n);
    static AionFrame Own()=>new(0x3336,J(V(17),[0,0],N("RatingHero"),S(1304),U(13),[1],U(50)),false);
    static byte[] Profile(int score=2871,int power=138016)
    {
        var b=new byte[600];var prefix=J(V(17),N("RatingHero"));prefix.CopyTo(b,0);U(13).CopyTo(b,prefix.Length);b[prefix.Length+4]=1;
        U(-3000).CopyTo(b,210);U(50).CopyTo(b,214);U(score).CopyTo(b,222);U(power).CopyTo(b,591);return b;
    }
    static AionFrame Inspect(string name,int server=1303,int code=13,int score=3001)
    {
        var b=new byte[130];N(name).CopyTo(b,3);int after=4+Encoding.UTF8.GetByteCount(name);
        U(code).CopyTo(b,after);U(50).CopyTo(b,after+6);U(score).CopyTo(b,after+14);
        S(server).CopyTo(b,after+20);N("FixtureLegion").CopyTo(b,after+22);return new(0x5036,b,false);
    }
    public static void Run(Action<bool,string> check)
    {
        int after=2+Encoding.UTF8.GetByteCount("RatingHero");
        var missingScore=PlayerRatingReader.Read(17,"RatingHero",1304,Profile(0),after,T);
        check(missingScore is{GearScore:null,CombatPower:138016},"Unavailable GS no longer suppresses a separately decoded CP");
        check(PlayerRatingReader.Read(17,"RatingHero",1304,Profile(-1),after,T)is{GearScore:null,CombatPower:138016},"Out-of-range GS remains unknown while a validated profile can supply CP");
        var ambiguous=Profile();U(-3000).CopyTo(ambiguous,260);U(51).CopyTo(ambiguous,264);
        check(PlayerRatingReader.Read(17,"RatingHero",1304,ambiguous,after,T)is null,"A second level anchor with unknown GS still makes the full profile ambiguous");
        var d=new ExperimentalGlobalDecoder();var observed=new List<PlayerRating>();var identities=new List<PlayerIdentity>();d.RatingUpdated+=observed.Add;d.IdentityUpdated+=identities.Add;
        d.Decode("rating-review",Own(),T);
        d.Decode("rating-review",new(0x4536,Profile(0),false),T);
        check(observed is[{GearScore:null,CombatPower:138016}]&&d.ProfilePowers==1&&d.ProfileScores==0&&identities.Last().PlayerVerified,"CP-only validated profiles reach ingestion and accurate diagnostic counters");
        d.Decode("rating-review",new(0x0297,J(S(1304),N("RatingHero"),U(13),U(50),S(1303),N("RatingAlly"),U(13),U(50)),false),T);
        int before=observed.Count;d.Decode("rating-review",Inspect("RatingAlly"),T.AddSeconds(1));
        check(observed.Count==before+1&&observed.Last()is{ActorId:0,Name:"RatingAlly",ServerId:1303,GearScore:3001,CombatPower:null},"Inspect GS can join an exact confirmed roster member while its actor ID is unavailable");
        check(!identities.Any(i=>i.Name=="RatingAlly"),"Roster-only inspect scores cannot invent world identities or combat actor IDs");
        before=observed.Count;d.Decode("rating-review",Inspect("RatingAlly",1304),T.AddSeconds(2));d.Decode("rating-review",Inspect("RatingAlly",1303,5),T.AddSeconds(2));d.Decode("rating-review",Inspect("Stranger"),T.AddSeconds(2));
        check(observed.Count==before,"Wrong-server, wrong-class and non-roster inspect data cannot claim a roster score");
        var guildless=Inspect("RatingHero",1304);Array.Clear(guildless.Payload,4+Encoding.UTF8.GetByteCount("RatingHero")+18,40);
        d.Decode("rating-review",guildless,T.AddSeconds(2));
        check(observed.Count==before,"Guildless inspect lacking a verified server stays unbound; own identity is never guessed from its name");

        var store=new PlayerRatingStore();var original=new PlayerRating(17,"RatingHero",1304,2871,138016,T,"Observed game profile");
        store.Identity(new(17,"RatingHero","Ranger",true,1304,1));store.Observe(original);
        store.Observe(original with{At=T.AddMinutes(1),GearScore=null,CombatPower=150000});
        var mixed=store.Find(17,"RatingHero",T.AddMinutes(1));
        check(mixed is{GearScore:2871,CombatPower:150000}&&mixed.GearScoreAt==T&&mixed.CombatPowerAt==T.AddMinutes(1),"Partial profile merges GS and CP without replacing missing data or resetting its observation time");
        store.Observe(original with{At=T.AddMinutes(2),GearScore=3001,CombatPower=null,Source="Observed inspect score"});
        mixed=store.Find(17,"RatingHero",T.AddMinutes(2));
        check(mixed is{GearScore:3001,CombatPower:150000}&&mixed.GearScoreAt==T.AddMinutes(2)&&mixed.CombatPowerAt==T.AddMinutes(1),"Inspect updates GS while preserving original CP age");
        check(store.Find(17,"RatingHero",T.AddMinutes(12))is{GearScore:3001,CombatPower:null},"Expired CP is hidden at lookup even without a later profile packet");
        check(store.FindRoster("RatingHero",1304,T.AddMinutes(12))is{GearScore:3001,CombatPower:null,ActorId:0},"Roster lookup applies the same per-field freshness as actor lookup");
        store.Observe(original with{At=T.AddMinutes(3),GearScore=9});check(store.Find(17,"RatingHero",T.AddMinutes(3))is{GearScore:9},"A later valid observation still replaces a known score");
        store.Observe(original with{At=T.AddMinutes(2),GearScore=1});check(store.Find(17,"RatingHero",T.AddMinutes(3))is{GearScore:9},"Older partial packets cannot roll newer observed ratings back");
        store.Clear();store.Observe(original);store.Observe(original with{At=T.AddHours(1),GearScore=null,CombatPower=150000});
        check(store.Find(17,"RatingHero",T.AddHours(1)) is{GearScore:2871,GearScoreAt:var age}&&age==T,"A fresh CP update does not refresh the age of retained GS");
        store.Observe(original with{At=T.AddHours(2).AddMinutes(1),GearScore=null,CombatPower=180000});
        check(store.Find(17,"RatingHero",T.AddHours(2).AddMinutes(1))is{GearScore:null,CombatPower:180000},"Repeated CP-only updates cannot keep an expired GS alive");
        store.Clear();store.Observe(new(0,"RosterOne",1303,3000,null,T,"Observed inspect score"));store.Observe(new(0,"RosterTwo",1304,4000,null,T,"Observed inspect score"));
        check(store.FindRoster("RosterOne",1303,T)is{GearScore:3000}&&store.FindRoster("RosterTwo",1304,T)is{GearScore:4000},"Roster-only profiles remain independent instead of colliding at actor ID zero");
        check(store.Find(0,"RosterOne",T)is null&&store.FindRoster("RosterOne",1304,T)is null&&store.FindRoster("rosterone",1303,T)is null,"Roster cache requires exact name and server and cannot masquerade as a combat actor");
        store.ClearWorld();check(store.FindRoster("RosterOne",1303,T.AddSeconds(2))is{} cached&&cached.At==T&&cached.Source.Contains("cached roster"),"World refresh retains observed roster metadata with original age and explicit cached source");
        store.Identity(new(50,"RosterOne","Ranger",false,1303,1));
        check(store.Find(50,"RosterOne",T.AddSeconds(2))is{ActorId:50,GearScore:3000},"A subsequently verified exact world identity can bind a roster-only inspected profile");
        check(store.FindRoster("RosterOne",1303,T.AddHours(3))is null&&store.FindRoster("RosterOne",1303,T.AddSeconds(-1))is null,"Roster cache rejects expired or future observations");
        store.Clear();check(store.FindRoster("RosterOne",1303,T)is null,"Explicit clear removes the roster profile cache too");
        store.Observe(original);store.ClearWorld();store.Identity(new(27,"RatingHero","Ranger",false,1304,1));store.Observe(original with{ActorId=27,At=T.AddMinutes(1),GearScore=null,CombatPower=160000});
        check(store.Find(27,"RatingHero",T.AddMinutes(1))is{GearScore:2871,CombatPower:160000,GearScoreAt:var originalAge}&&originalAge==T,"Partial first profile after teleport can retain independently observed values by exact verified identity");
        store.Observe(new(0,"RatingHero",1304,3200,null,T.AddMinutes(2),"Observed inspect score"));
        check(store.Find(27,"RatingHero",T.AddMinutes(2))is{ActorId:27,GearScore:3200,CombatPower:160000},"A newer exact cached inspect supersedes an older actor-bound profile and preserves recent CP");
        store.Observe(new(0,"RatingHero",1303,9999,null,T.AddMinutes(3),"Observed inspect score"));
        check(store.Find(27,"RatingHero",T.AddMinutes(3))is{GearScore:3200},"Newer same-name observations on another server cannot override a bound actor");
    }
}
