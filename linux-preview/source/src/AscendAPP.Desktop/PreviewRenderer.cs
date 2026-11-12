using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
using AscendAPP.Desktop.ViewModels;

namespace AscendAPP.Desktop;
internal static class PreviewRenderer
{
    // Explicit developer-only render path: no game, native capture or visible window.
    public static void Render(string output, bool unknown, string? settingsPage=null,bool healing=false,bool minimal=false,bool refreshing=false,bool events=false,bool party=false,bool history=false,bool strip=false,bool pets=false,bool pickup=false,string? accent=null,string? background=null,string? logo=null)
    {
        if(events)unknown=true;
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var messages = new StringWriter();
        var listener = new TextWriterTraceListener(messages);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var now = DateTimeOffset.UtcNow;
        var vm = new UiModel { Mode = unknown ? "LIVE • EXPERIMENTAL" : "DEMO",HeaderState=unknown?"Idle":"Demo",SelfName=unknown?"Detecting character":"Kael" };
        if(refreshing){vm.IdentityRefreshPending=true;vm.SelfName="Refreshing character…";}
        var engine = new CombatEngine();
        var forecast = new BossForecastTracker(new BossTimingCatalog(new Dictionary<int,int>{{2300000,300}}));
        if (!unknown)
        {
            var start = now.AddSeconds(-60);
            string[] names=["Kael","Sylvaen","Nyxara","Borgrim","Celesta"];
            string[] classes=["Ranger","Sorcerer","Assassin","Templar","Cleric"];
            int[] rates=[18400,15200,12300,11400,2700];
            forecast.Engage(new(900,true,false,start));
            for (int step=0;step<10;step++)
                for (int i=0;i<names.Length;i++)
                {
                    var e = new CombatEvent(start.AddSeconds(step*6),(uint)(17+i),names[i],900,"Sample dungeon boss",14010000+i,rates[i]*6,CombatKind.Damage,
                        step%3==0 ? HitTags.Critical : HitTags.None,ActorClass:classes[i],IsSelf:i==0,TargetIsBoss:true,TargetNpcCode:2300000);
                    engine.Add(e); forecast.Add(e);
                }
            engine.Add(new CombatEvent(now.AddSeconds(-1),21,"Celesta",17,"Kael",17240000,320000,CombatKind.Heal,ActorClass:"Cleric",EffectiveHealing:180000,HealingAssessment:"Observed HP gain (estimate)"));
            engine.UpdateHealth(900,4240000,8000000);
            forecast.Health(900,4240000,8000000,now);
            vm.ApplyEnergy(new OdyleEnergy(75,520,now),demo:true);
        }
        if(party){vm.Mode="DEMO";vm.HeaderState="Demo";vm.SelfName="Kael · demo";}
        if(party)vm.SetParty([new("Kael",1304,"Ranger",50,17,true,"Roster + name/server"),new("Sylvaen",1303,"Sorcerer",50,18,false,"Roster + name/server"),new("Morwen",1303,"Chanter",50,null,false,"Waiting for character ID")],"Self and party");
        if(minimal){vm.Preferences.HideTotalDamage=true;vm.Preferences.HideNames=true;vm.Preferences.UiScale=0.8;}
        vm.Apply(engine.Snapshot(now));if(!unknown)vm.ApplyRatings(row=>new(row.ActorId,row.Name,1304,2750+(int)row.ActorId,138016+(int)row.ActorId*1200,now,"DEMO · synthetic ratings"),now); vm.ApplyForecast(forecast.Snapshot(now));
        if(!unknown)vm.SelfClass="Ranger";
        if(!unknown)vm.ApplyOwnRating(new(17,"Kael",1304,2767,158416,now,"DEMO · synthetic profile"),now);
        if (unknown) vm.ApplyEnergy(null);
        if (!unknown && (vm.OdyleAvailable!="595" || !vm.TimeToKill.StartsWith("≈ ") || !vm.BerserkIn.StartsWith("≈ ")))
            throw new InvalidOperationException("Preview data binding model failed.");
        if(strip&&!unknown)vm.EncounterName="DEMO · "+vm.EncounterName;
        vm.ShowHealing=healing;vm.Preferences.CombatStrip=strip;vm.ApplyCaptureHealth(CaptureHealth.Evaluate(false,vm.Mode,new(0,0,0,0,0,0,false,0,0,0,0),false,false,false,now));
        using var controller=new MeterController(preview:true);
        if(pets){controller.Pets.Observe(new AscendAPP.Core.Pets.PetCollectionObservation("DEMO character",1304,[new(1006,1,13),new(1097,2,42),new(1003,3,0)],now));controller.Pets.Observe(new AscendAPP.Core.Pets.PetGainObservation("DEMO character",1304,1006,1,now));controller.Preferences.PinnedPets=[1006,1097];controller.Preferences.PetOnlyPinned=true;}
        if(accent is not null||background is not null||logo is not null){var p=controller.Preferences;p.AccentColor=accent??p.AccentColor;p.PanelColor=background??p.PanelColor;p.LogoColor=logo??p.LogoColor;p.LogoFollowsAccent=logo is null;controller.ApplyPreferences();}
        if(pickup)controller.CreatePetPickup().PreviewCounter(renderOnly:true);
        bool fixedHeight=settingsPage is not null||history||pickup;
        var review=engine.Snapshot(now) with{IsActive=false};
        Window window=pickup?controller.CreatePetPickup():pets?controller.CreatePetOverlay():history?new FightHistoryWindow([review,review with{Id=Guid.NewGuid(),StartedAt=now.AddMinutes(-10),DurationSeconds=72}]):events?controller.CreateEventOverlay():settingsPage is null?new OverlayWindow(vm):new SettingsWindow(controller);
        if(window is FightHistoryWindow historyWindow)((System.Windows.Controls.TextBlock)historyWindow.FindName("HistoryTitle")).Text="Encounter history · DEMO";
        if(window is SettingsWindow settings)settings.SelectPage(settingsPage!);
        var element=(FrameworkElement)window.Content;
        element.Measure(new Size(window.Width,!fixedHeight?double.PositiveInfinity:window.Height));
        double height=!fixedHeight?Math.Ceiling(element.DesiredSize.Height):window.Height;
        element.Arrange(new Rect(0,0,window.Width,height));
        element.UpdateLayout();
        for(int pass=0;pass<4;pass++){System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);element.InvalidateMeasure();element.Measure(new Size(window.Width,!fixedHeight?double.PositiveInfinity:window.Height));height=!fixedHeight?Math.Ceiling(element.DesiredSize.Height):window.Height;element.Arrange(new Rect(0,0,window.Width,height));element.UpdateLayout();}
        if(!fixedHeight&&!unknown&&!strip&&!pets&&height<(minimal?175:220))throw new InvalidOperationException("Compact rows were clipped in preview: "+height);
        var bitmap = new RenderTargetBitmap((int)window.Width,(int)height,96,96,PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        using (var file=File.Create(output)) encoder.Save(file);
        PresentationTraceSources.DataBindingSource.Flush();
        PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        if (!string.IsNullOrWhiteSpace(messages.ToString()))
        {
            File.WriteAllText(output+".bindings.txt",messages.ToString());
            throw new InvalidOperationException("WPF binding warnings were recorded.");
        }
        if(window is PetPickupWindow pickupWindow)pickupWindow.CloseForExit();else if(window is PetOverlayWindow petWindow)petWindow.CloseForExit();else if(window is EventOverlayWindow eventWindow)eventWindow.CloseForExit();else window.Close();
    }
}
