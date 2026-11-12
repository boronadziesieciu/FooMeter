using AscendAPP.Core.Pets;
using AscendAPP.Core.Settings;
static class LootCounterChecks
{
    public static void Run(Action<bool,string> check)
    {
        var now=DateTimeOffset.UtcNow;var matcher=new LootRowMatcher(new Dictionary<uint,string>{{1016,"Slink"},{1121,"Young Slink"},{1194,"Guardite"},{1197,"Watcher Guardite"}});
        check(matcher.Identify("Soul: Slink (Bound) x1")==1016,"Loot text maps an exact soul name to its pet ID");
        check(matcher.Identify("Soul: Young Slink (Bound) x1")==1121,"Longer pet names are not mistaken for their shorter suffix");
        check(matcher.Identify("Soul: Guardi")==1194,"A unique long scrolling-name prefix remains identifiable");
        check(matcher.Identify("Kina x140") is null&&matcher.Identify("Soul: G") is null&&matcher.Identify("Soul: Slinker") is null,"Coins, ambiguous short fragments and unrelated suffixes do not become pet counters");
        using var store=new PetProgressStore(null,new Dictionary<uint,string>{{1016,"Slink"},{1121,"Young Slink"}});
        store.Observe(new PetCollectionObservation("Fixture",1304,[new(1016,1,13),new(1121,1,10)],now));
        store.Observe(new PetGainObservation("Fixture",1304,1016,1,now));store.Observe(new PetGainObservation("Fixture",1304,1016,1,now.AddMilliseconds(1)));
        var gains=store.RecentPickups(now.AddSeconds(1));
        check(gains.Count==2&&gains[0].Points==14&&gains[1].Points==15,"Consecutive pickup snapshots retain separate observed totals");
        var rows=matcher.Resolve([new("Soul: Slink (Bound) x1",0,10,160,14,90),new("Soul: Slink (Bound) x1",0,50,160,14,90),new("Soul: Young Slink (Bound) x1",0,90,180,14,90)],store.Progress,gains,now.AddSeconds(1));
        check(rows.Count==3&&rows[0].Text=="14 / 25"&&rows[1].Text=="15 / 25"&&rows[2].Text=="10 / 25","Multiple visible loot rows get separate packet-derived values in the correct vertical order");
        var before=store.Progress(1016);matcher.Resolve([new("Soul: Slink (Bound) x1",0,10,160,14,90)],store.Progress,[],now);
        check(store.Progress(1016)==before,"Reading loot text never increments soul progress");
        store.Invalidate("gap");var stale=matcher.Resolve([new("Soul: Slink",0,10,160,14,90)],store.Progress,[],now);
        check(!stale.Single().Fresh,"Cached collection values are distinguished from a fresh capture");
        store.Bind("Other",1304);check(store.RecentPickups(now.AddSeconds(1)).Count==0&&store.Progress(1016).Level is null,"Character switching clears pickup associations instead of borrowing another collection");
        for(int i=0;i<80;i++)store.Observe(new PetGainObservation("Other",1304,1016,1,now));
        check(store.RecentPickups(now).Count==64,"Pickup association history remains bounded");
        var p=new MeterPreferences{LootRegionLeft=.99,LootRegionTop=.99,LootRegionWidth=double.NaN,LootRegionHeight=double.NaN};p.Normalize();
        check(p.LootRegionLeft+p.LootRegionWidth<=1&&p.LootRegionTop+p.LootRegionHeight<=1&&p.PetCounterStyle=="Beside loot rows","Normalized loot areas stay inside the game and use the requested counter style by default");
    }
}
