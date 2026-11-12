using AscendAPP.Core.Settings;
static class AppearanceChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(AppearanceColors.TryNormalize(" 81b9ff ",out var hex)&&hex=="#81B9FF","Appearance accepts and canonicalizes RGB colors");
        check(AppearanceColors.TryNormalize("#abc",out hex)&&hex=="#AABBCC","Short RGB colors expand consistently");
        foreach(var input in new[]{"red","transparent","#12345678","#GGGGGG","",null})
            check(!AppearanceColors.TryNormalize(input,out _),"Invalid or transparent appearance color is rejected: "+(input??"null"));
        var p=new MeterPreferences{AccentColor=null!,PanelColor="#12345678",LogoColor="bad color",PetPickupLeft=double.NaN,PetPickupTop=double.PositiveInfinity};
        p.Normalize();
        check(p.AccentColor==AppearanceColors.DefaultAccent&&p.PanelColor==AppearanceColors.DefaultPanel&&p.LogoColor==AppearanceColors.DefaultLogo&&!p.PetPickupLeft.HasValue&&!p.PetPickupTop.HasValue,"Old or invalid appearance preferences recover to safe defaults");
        p.AccentColor="#81b9ff";p.PanelColor="#101214";p.LogoColor="#ebc574";p.LogoFollowsAccent=false;p.ShowPetPickups=false;p.PetPickupLeft=245;p.Normalize();
        string json=System.Text.Json.JsonSerializer.Serialize(p);
        check(PreferencesStore.TryParse(json,out var restored)&&restored.AccentColor=="#81B9FF"&&restored.PanelColor=="#101214"&&restored.LogoColor=="#EBC574"&&!restored.LogoFollowsAccent&&!restored.ShowPetPickups&&restored.PetPickupLeft==245,"Independent app/logo colors and pickup preferences survive settings serialization");
        using var pet=new AscendAPP.Core.Pets.PetProgressStore(null);
        var now=DateTimeOffset.UtcNow;pet.Observe(new AscendAPP.Core.Pets.PetGainObservation("Fixture",1304,1006,2,now));
        check(pet.Pickup is{Amount:2,Confirmed:false,Points:null},"Unsynchronized pickup counters expose observed gains without made-up totals");
        check(pet.Status.Contains("Fixture · 1304")&&!pet.Status.Contains("not identified"),"Pet status distinguishes a known character from a missing collection baseline");
        pet.Observe(new AscendAPP.Core.Pets.PetCollectionObservation("Fixture",1304,[new(1006,1,13)],now));
        pet.Observe(new AscendAPP.Core.Pets.PetGainObservation("Fixture",1304,1006,1,now));
        check(pet.Pickup is{Confirmed:true,Points:14,Level:1},"Synchronized pickup counters expose the authoritative running count");
    }
}
