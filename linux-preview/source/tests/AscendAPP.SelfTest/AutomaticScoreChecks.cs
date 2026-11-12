using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Combat;
internal static class AutomaticScoreChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-26T13:00:00Z");
    static byte[] Join(params byte[][] parts)=>parts.SelectMany(p=>p).ToArray();
    static byte[] Name(string s){var b=Encoding.UTF8.GetBytes(s);return Join([(byte)b.Length],b);}
    static byte[] U(int n)=>BitConverter.GetBytes(n);
    static byte[] S(int n)=>BitConverter.GetBytes((ushort)n);
    static byte[] Q(ulong n)=>BitConverter.GetBytes(n);
    static byte[] Power(int cp){var b=Join(Q((ulong)cp),Q((ulong)(cp+123)),[5],new byte[15]);for(int i=0;i<5;i++){b[17+i*3]=(byte)(i+1);b[18+i*3]=(byte)(40+i);}return b;}
    internal static byte[] Own(string name="AutoHero",int server=1318,int gs=3141,int cp=161803,uint id=17)=>Join(WireFixture.V(id),[0,0],Name(name),S(server),U(9),[1],U(45),U(gs),U(gs),new byte[23],Power(cp));
    static byte[] Member(string name,int server,int gs,int cp,int slot=1,int world=0,int extra=0)=>Join([12,(byte)slot],Q(((ulong)server<<48)|1234UL),Name(name),U(9),U(45),U(gs),new byte[extra],S(world==0?server:world),S(world==0?server:world),[4],Q((ulong)cp),[0,0,1]);
    internal static byte[] Roster(params byte[][] members)=>Join(new byte[24],Join(members));
    public static void Run(Action<bool,string> check)
    {
        var p=new PlayerIdentity(17,"AutoHero","Templar",true,1318);var b=Own();int after=3+1+Encoding.UTF8.GetByteCount(p.Name);
        var r=AutomaticRatingReader.Own(b,after,p,T,out var detail);
        check(r is{GearScore:3141,CombatPower:161803,ManualGearScore:false,ManualCombatPower:false}&&detail.Contains("CP observed"),"Own record supplies independent automatic GS and full-precision CP");
        var wrong=b.ToArray();U(3142).CopyTo(wrong,after+15);
        check(AutomaticRatingReader.Own(wrong,after,p,T,out _)is{GearScore:null,CombatPower:161803},"Inconsistent duplicate own GS is rejected without losing a valid CP");
        wrong=b.ToArray();wrong[^3]=8;check(AutomaticRatingReader.Own(wrong,after,p,T,out _)is{GearScore:3141,CombatPower:null},"Invalid keyed own CP table remains unknown");
        var ambiguous=Join(b,new byte[9],Power(180001));check(AutomaticRatingReader.Own(ambiguous,after,p,T,out var ambiguousDetail)is{GearScore:3141,CombatPower:null}&&ambiguousDetail.Contains("ambiguous"),"Two plausible own CP locations cannot pick an arbitrary number");
        check(AutomaticRatingReader.Own(b,after,p with{ServerId=2318},T,out _)is null&&AutomaticRatingReader.Own(b,after,p with{ClassName="Ranger"},T,out _)is null&&AutomaticRatingReader.Own(b,after,p with{IsSelf=false},T,out _)is null,"Own scores require the verified class, home server and self identity");
        check(AutomaticRatingReader.Own(b[..(after+11)],after,p,T,out _)is null,"Identity-only own records do not fabricate scores");
        bool truncated=true;for(int size=0;size<b.Length;size++){var value=AutomaticRatingReader.Own(b[..size],after,p,T,out _);if(value?.CombatPower is not null)truncated=false;}check(truncated,"Every truncated own-tail boundary is safe and cannot produce CP");
        wrong=b.ToArray();Q(0).CopyTo(wrong,wrong.Length-32);check(AutomaticRatingReader.Own(wrong,after,p,T,out _)is{CombatPower:null},"Zero own CP does not become a known score");
        wrong=b.ToArray();Q(ulong.MaxValue).CopyTo(wrong,wrong.Length-32);check(AutomaticRatingReader.Own(wrong,after,p,T,out _)is{CombatPower:null},"Oversized own CP is rejected before integer conversion");
        string unicode="Åuto名";var ub=Own(unicode);check(AutomaticRatingReader.Own(ub,4+Encoding.UTF8.GetByteCount(unicode),p with{Name=unicode},T,out _)is{CombatPower:161803},"Multibyte own names do not shift the numeric field offsets");
        foreach(int extra in new[]{0,1,2})
        {
            var member=Member("PartyFixture",2318,2911,147123,2,2302,extra);int end=11+Encoding.UTF8.GetByteCount("PartyFixture");
            check(AutomaticRatingReader.Party(member,10,end,"PartyFixture",2318,"Templar",T)is{ActorId:0,ServerId:2318,GearScore:2911,CombatPower:147123},$"Party score variant +{extra} bytes uses the account home server while visiting another world");
        }
        var mb=Member("PartyFixture",1318,2911,147123);int mend=11+Encoding.UTF8.GetByteCount("PartyFixture");
        wrong=mb.ToArray();wrong[0]=0;check(AutomaticRatingReader.Party(wrong,10,mend,"PartyFixture",1318,"Templar",T)is null,"An empty party slot cannot create a score observation");
        wrong=mb.ToArray();wrong[mend+16]=0x10;check(AutomaticRatingReader.Party(wrong,10,mend,"PartyFixture",1318,"Templar",T)is{GearScore:2911,CombatPower:null},"Shifted server bytes with an invalid tag cannot inflate party CP");
        check(AutomaticRatingReader.Party(mb[..(mend+17)],10,mend,"PartyFixture",1318,"Templar",T)is{GearScore:2911,CombatPower:null},"A partial party tail retains only the independently available GS");
        check(AutomaticRatingReader.Party(mb,10,mend,"PartyFixture",2318,"Templar",T)is null,"Party score association rejects an account/home-server mismatch");
        var decoder=new ExperimentalGlobalDecoder();var store=new PlayerRatingStore();var ratings=new List<PlayerRating>();PartyRoster? roster=null;
        decoder.RatingUpdated+=r=>{ratings.Add(r);store.Observe(r);};decoder.IdentityUpdated+=store.Identity;decoder.PartyUpdated+=r=>roster=r;
        decoder.Decode("world",new(0x3336,b,false),T);
        check(store.Find(17,"AutoHero",T)is{GearScore:3141,CombatPower:161803}&&decoder.OwnGearScores==1&&decoder.OwnCombatPowers==1,"Framed own login updates the real rating store and diagnostic counters");
        var party=Roster(Member("AutoHero",1318,3150,162500),Member("PartyFixture",2318,2911,147123,2,2302,1));
        store.Observe(new(0,"AutoHero",1318,3142,161900,T,"Automatic same-packet roster"));
        check(store.Find(17,"AutoHero",T)is{GearScore:3142,CombatPower:161900}&&store.Snapshot(T).Any(x=>x.Name=="AutoHero"&&x.GearScore==3142),"Later score observations in the same TCP timestamp supersede an earlier actor-bound value");
        store.Observe(new(17,"AutoHero",1318,3143,null,T,"Same-packet inspect"));
        check(store.Find(17,"AutoHero",T)is{GearScore:3143,CombatPower:161900},"A same-timestamp GS-only update retains the latest profile CP rather than an older actor value");
        decoder.Decode("world",new(0x0297,party,false),T.AddSeconds(1));
        check(roster?.Members.Count==2&&store.Find(17,"AutoHero",T.AddSeconds(1))is{GearScore:3150,CombatPower:162500}&&decoder.PartyScoreRecords==2,"Party roster refreshes self GS/CP automatically after the initial login");
        check(store.FindRoster("PartyFixture",2318,T.AddSeconds(1))is{ActorId:0,CombatPower:147123}&&store.FindRoster("PartyFixture",2302,T.AddSeconds(1))is null,"Roster-only scores bind to the home server without fabricating a combat ID");
        store.Identity(new(27,"PartyFixture","Templar",false,2318));check(store.Find(27,"PartyFixture",T.AddSeconds(2))is{CombatPower:147123},"A later verified party entity inherits exact cached roster scores");
        store.ClearWorld();store.Identity(p with{Id=71});check(store.Find(71,"AutoHero",T.AddSeconds(3))is{CombatPower:162500},"Automatic own scores survive a teleport with a newly verified world ID");
        store.Identity(p with{Id=71,ServerId=2318});check(store.Find(71,"AutoHero",T.AddSeconds(3))is null,"Automatic scores never cross same-name characters on different servers");
        int count=ratings.Count;decoder.Decode("other-world",new(0x0297,party,false),T.AddSeconds(2));check(ratings.Count==count,"An unrelated unselected connection cannot publish party scores");
        decoder.Decode("world",new(0x3336,Own(gs:3160,cp:164000),false),T.AddSeconds(3));check(decoder.OwnGearScores==2&&store.Find(17,"AutoHero",T.AddSeconds(3))is{GearScore:3160,CombatPower:164000},"A repeated own record automatically refreshes changed equipment scores");
        decoder.Reset();check(decoder.OwnGearScores==0&&decoder.PartyScoreRecords==0,"Capture reset clears automatic source diagnostics");
    }
}
