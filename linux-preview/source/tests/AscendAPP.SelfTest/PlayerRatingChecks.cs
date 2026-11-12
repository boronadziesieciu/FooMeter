using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
internal static class PlayerRatingChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-24T12:00:00Z");
    static byte[] J(params byte[][] p)=>p.SelectMany(x=>x).ToArray();
    static byte[] N(string name){var b=Encoding.UTF8.GetBytes(name);return J([(byte)b.Length],b);}
    static byte[] U(int value)=>BitConverter.GetBytes(value);
    static AionFrame Character()=>new(0x3336,J([17,0,0],N("FixtureHero"),[0x18,5],U(13),[1],U(50)),false);
    static byte[] Profile(string name="FixtureHero",uint id=17)
    {
        var prefix=J(WireFixture.V(id),N(name));var b=new byte[600];prefix.CopyTo(b,0);int after=prefix.Length;
        U(13).CopyTo(b,after);b[after+4]=1;U(-3000).CopyTo(b,210);U(50).CopyTo(b,214);U(2871).CopyTo(b,222);U(138016).CopyTo(b,591);return b;
    }
    public static void Run(Action<bool,string> check)
    {
        var bytes=Profile();int after=1+1+Encoding.UTF8.GetByteCount("FixtureHero");var rating=PlayerRatingReader.Read(17,"FixtureHero",1304,bytes,after,T);
        check(rating is{GearScore:2871,CombatPower:138016},"Documented profile anchor and unique CP tail decode independently of damage");
        check(PlayerRatingReader.Read(17,"FixtureHero",1304,bytes[..50],after,T)is null,"Short nickname frame cannot fabricate player ratings");
        var ambiguous=bytes.ToArray();U(-3000).CopyTo(ambiguous,260);U(51).CopyTo(ambiguous,264);U(3000).CopyTo(ambiguous,272);
        check(PlayerRatingReader.Read(17,"FixtureHero",1304,ambiguous,after,T)is null,"Multiple plausible gear anchors are rejected instead of choosing an arbitrary value");
        ambiguous=bytes.ToArray();U(65537).CopyTo(ambiguous,591);ambiguous[590]=1;
        check(PlayerRatingReader.Read(17,"FixtureHero",1304,ambiguous,after,T)is{GearScore:2871,CombatPower:null},"Ambiguous shifted CP tails remain unknown while the independent GS stays valid");
        var invalid=bytes.ToArray();invalid[after+4]=0;
        check(PlayerRatingReader.Read(17,"FixtureHero",1304,invalid,after,T)is null,"Unrecognized faction does not activate player-rating heuristics");
        invalid=bytes.ToArray();U(999).CopyTo(invalid,after);check(PlayerRatingReader.Read(17,"FixtureHero",1304,invalid,after,T)is null,"Unrecognized class prevents a false profile match");
        var store=new PlayerRatingStore();store.Observe(rating!);
        check(store.Find(17,"FixtureHero",T)is{CombatPower:138016}&&store.Find(17,"Other",T)is null,"Rating cache requires actor ID and exact current name");
        store.Observe(rating! with{At=T.AddSeconds(-1),GearScore=1});check(store.Find(17,"FixtureHero",T)!.GearScore==2871,"Delayed profiles cannot replace a newer score");
        check(store.Find(17,"FixtureHero",T.AddHours(3))is null,"Unrefreshed scores expire without being extended by UI refreshes");
        store.Forget(17);check(store.Find(17,"FixtureHero",T)is null,"Entity removal invalidates cached scores");
        var d=new ExperimentalGlobalDecoder();var ratings=new List<PlayerRating>();var identities=new List<AscendAPP.Core.Protocol.PlayerIdentity>();d.RatingUpdated+=ratings.Add;d.IdentityUpdated+=identities.Add;
        d.Decode("fixture-world",Character(),T);d.Decode("fixture-world",new(0x4536,bytes,false),T);
        check(ratings.Last()is{ActorId:17,GearScore:2871,CombatPower:138016}&&identities.Last().ClassName=="Ranger","Profile decoder emits observed ratings and class metadata for the existing actor");
        var inspect=new byte[130];N("FixtureHero").CopyTo(inspect,3);int p=4+Encoding.UTF8.GetByteCount("FixtureHero");U(13).CopyTo(inspect,p);U(50).CopyTo(inspect,p+6);U(3001).CopyTo(inspect,p+14);inspect[p+20]=0x18;inspect[p+21]=5;N("FixtureLegion").CopyTo(inspect,p+22);
        d.Decode("fixture-world",new(0x5036,inspect,false),T.AddSeconds(1));
        check(ratings.Last()is{ActorId:17,GearScore:3001,CombatPower:null}&&ratings.Count==2,"In-game inspect score joins only the verified name, server and class");
        inspect[p+20]=0x17;d.Decode("fixture-world",new(0x5036,inspect,false),T.AddSeconds(2));check(ratings.Count==2,"Same inspect name on a different server cannot overwrite an own score");
        inspect[p+20]=0x18;U(5).CopyTo(inspect,p);d.Decode("fixture-world",new(0x5036,inspect,false),T.AddSeconds(2));check(ratings.Count==2,"Inspect class mismatch remains unbound");
        store.Observe(rating!);store.Observe(ratings[1]);check(store.Find(17,"FixtureHero",T.AddSeconds(1))is{GearScore:3001,CombatPower:138016},"Fresh GS-only inspection preserves a recently observed CP without estimating it");
        store.Observe(ratings[1] with{At=T.AddMinutes(9)});store.Observe(ratings[1] with{At=T.AddMinutes(11)});check(store.Find(17,"FixtureHero",T.AddMinutes(11))is{CombatPower:null},"Repeated GS-only inspections cannot indefinitely refresh an old CP observation");
        store.Clear();check(store.Find(17,"FixtureHero",T)is null,"World connection reset clears rating joins");
    }
}
