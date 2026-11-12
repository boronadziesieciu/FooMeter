using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
namespace AscendAPP.Desktop;
internal static class UiSelfChecks
{
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach(var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var x in Descendants(c))yield return x;
    }
    public static void Run(string output)
    {
        var results=new List<string>();void Check(bool condition,string title){if(!condition)throw new InvalidOperationException(title);results.Add(title);}
        var warnings=new StringWriter();var listener=new TextWriterTraceListener(warnings);PresentationTraceSources.DataBindingSource.Listeners.Add(listener);PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Warning;
        using var c=new MeterController(preview:true);var overlay=c.CreateOverlay();var settings=new SettingsWindow(c);
        var eventWindow=c.CreateEventOverlay();
        Check(eventWindow.Width==300&&eventWindow.Title.Contains("Events"),"Independent compact event window uses the shared application model");
        Check(c.Model.ScheduledEvents.Count==2&&c.Model.FieldBossTimers.Count==4&&c.Model.FieldBossTimers.All(x=>x.Remaining=="—"),"Requested schedules and four unknown boss timers initialize without invented snapshots");
        settings.SelectPage("Event tracking");var showEvents=Descendants(settings).OfType<CheckBox>().Single(x=>AutomationProperties.GetName(x)=="Show event overlay");showEvents.IsChecked=true;
        Check(c.Preferences.ShowEventOverlay,"Timer visibility switch changes the persisted overlay preference");
        Descendants(overlay).OfType<Button>().Single(x=>AutomationProperties.GetName(x)=="Toggle events and boss timers").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!c.Preferences.ShowEventOverlay,"Main meter bell toggles the independent event window");
        c.SetEventOverlayVisible(true);Descendants(eventWindow).OfType<Button>().Single(x=>AutomationProperties.GetName(x)=="Hide event overlay").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!c.Preferences.ShowEventOverlay&&overlay.DataContext==c.Model,"Timer close action hides timers while retaining the main meter");
        Descendants(eventWindow).OfType<Button>().Single(x=>AutomationProperties.GetName(x)=="Lock timer position").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(c.Preferences.EventOverlayLocked&&!c.Preferences.PositionLocked,"Timer position lock is independent from the combat overlay");
        var observedAt=DateTimeOffset.UtcNow;
        c.ObserveFieldBosses(new(1010,new[]{21,22,23,24}.Select((n,i)=>new AscendAPP.Core.Game.FieldBossSlot(n,false,observedAt.AddHours(i+1).ToUnixTimeMilliseconds())).ToArray(),observedAt,1304));c.RefreshWorldTimers(observedAt,true);
        Check(c.Model.FieldBossTimers.First().Name=="Eternal Gartua"&&c.Model.FieldBossTimers.First().Remaining=="01:00:00","Observed server map deadlines reach the actual event-window bindings");
        c.RefreshWorldTimers(observedAt.AddHours(7),true);
        Check(c.Model.FieldBossTimers.All(x=>x.Remaining=="Due · refresh"),"Expired timer rows request a refresh rather than announcing fabricated spawns");
        c.Preferences.ShowFieldBossTimers=false;c.Preferences.ShowShugoTimer=false;c.Preferences.ShowRiftTimer=false;c.ApplyPreferences();
        Check(Descendants(eventWindow).OfType<TextBlock>().Single(x=>x.Name=="EmptyMessage").Visibility==Visibility.Visible,"Disabling all timer groups produces a clear empty state");
        c.Preferences.ShowFieldBossTimers=true;c.Preferences.ShowShugoTimer=true;c.Preferences.ShowRiftTimer=true;c.ApplyPreferences();
        Check(overlay.Width==420&&overlay.Title.Contains("Compact"),"Single compact overlay is the startup surface");
        foreach(var page in new[]{"General","Combat","Display","Overlay behaviour","Audio","Event tracking","Keybinds","Account","Integrations","Changelog","Debug","Tickets"}){settings.SelectPage(page);Check(Descendants(settings).OfType<TextBlock>().Any(),page+" settings construct");}
        settings.SelectPage("Combat");var scope=Descendants(settings).OfType<ComboBox>().Single(x=>x.Items.Cast<object>().Contains("Self only"));
        Check(scope.Items.Cast<object>().SequenceEqual(new[]{"Self only","Self and party"}),"Combat settings expose only self or confirmed-party capture");
        scope.SelectedItem="Self only";Check(c.Preferences.CaptureScope=="Self only","Capture scope selector updates the actual capture preference");
        Check(!Descendants(settings).OfType<ComboBox>().Any(x=>x.Items.Cast<object>().Contains("All targets")),"No settings control can enable ordinary-target capture");
        c.Preferences.CaptureScope="Self and party";c.ApplyPreferences();
        c.Model.IdentityRefreshPending=true;
        Check(Descendants(overlay).OfType<TextBlock>().Single(x=>x.Name=="IdleTitle").Text=="Refreshing character after teleport…","Compact idle surface explains pending teleport character refresh");
        c.Model.IdentityRefreshPending=false;c.Model.SelfIdentified=true;
        Check(Descendants(overlay).OfType<TextBlock>().Single(x=>x.Name=="IdleTitle").Text=="Waiting for boss or dummy combat","Recovered identity returns the overlay to eligible-combat waiting state");
        c.Showcase();
        Check(c.Model.Rows.Count==9&&c.Model.Mode.Contains("DEMO")&&!c.Model.IdentityRefreshPending,"Synthetic replay remains complete and labelled regardless of capture-only filters");
        settings.SelectPage("Display");var hide=Descendants(settings).OfType<CheckBox>().Single(x=>AutomationProperties.GetName(x)=="Hide other player names");hide.IsChecked=true;
        Check(c.Preferences.HideNames&&c.Model.Rows.Where(x=>!x.IsSelf).All(x=>x.DisplayName==x.ClassName),"Hide-name switch changes visible rows immediately");
        var total=Descendants(settings).OfType<CheckBox>().Single(x=>AutomationProperties.GetName(x)=="Hide total damage");total.IsChecked=true;Check(c.Preferences.HideTotalDamage,"Damage-column switch updates saved preference");
        c.Preferences.UiScale=0.8;c.ApplyPreferences();Check(Math.Abs(overlay.Width-336)<0.001,"Scale preference resizes the actual compact window");
        c.Model.ShowHealing=true;Check(c.Model.RateTitle=="eHPS · HP"&&c.Model.OverlayRows.First().RawHealing==c.Model.Rows.Max(x=>x.RawHealing),"Healing tab reorders bars by actual healing");
        var legacyEngine=new AscendAPP.Core.Combat.CombatEngine();
        legacyEngine.Add(new(DateTimeOffset.UtcNow,17,"LegacyHealer",18,"Ally",17240000,200,AscendAPP.Core.Combat.CombatKind.Heal));
        var legacyModel=new AscendAPP.Desktop.ViewModels.UiModel();legacyModel.Apply(legacyEngine.Snapshot(DateTimeOffset.UtcNow));
        Check(legacyModel.HealingInfo.Contains("original healing totals"),"Legacy healing replay is visibly labelled as potentially including overheal");
        using(var details=new CloseWindow(new SkillBreakdownWindow(c.AnalysisSnapshot(),healing:true,hideNames:true)))Check(details.Window.Title.Contains("Analysis"),"Healing detail window binds without a separate dashboard");
        settings.SelectPage("Event tracking");Descendants(settings).OfType<Button>().Single(x=>AutomationProperties.GetName(x)=="New reminder").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(c.Preferences.Reminders.Count==1,"New-reminder action creates a user-defined schedule");
        c.Model.ApplyForecast(new(900,100,null,null,null,"Your dummy DPS","Training dummy","No boss countdown",true));
        Check(c.Model.ForecastIsDummy&&Descendants(overlay).OfType<Grid>().Single(x=>x.Name=="ForecastPanel").Visibility==Visibility.Collapsed,"Training dummy forecast hides the boss countdown row");
        c.ResetEncounter();Check(!c.Model.HasRows,"Reset action clears the visible meter");c.ClearMeter();Check(c.Model.Latest?.Players.Count==0,"Clear-meter action leaves no synthetic combat rows");
        var at=DateTimeOffset.UtcNow;c.Preferences.CaptureScope="Self only";c.ApplyPreferences();
        var own=new AscendAPP.Core.Combat.CombatEvent(at,17,"FixtureSelf",900,"NPC #2400032",14010000,100,AscendAPP.Core.Combat.CombatKind.Damage,IsSelf:true,TargetNpcCode:2400032);
        c.OnCombat(own);c.OnCombat(own with{ActorId=18,ActorName="Nearby",IsSelf=false,Amount=90000});c.OnCombat(own with{TargetNpcCode=2400033,TargetId=901,TargetName="Ordinary target",Amount=90000});c.RefreshScreen();
        Check(c.Model.Rows.Count==1&&c.Model.Rows.Single().ActorId==17&&c.Model.Rows.Single().RawDamage==100,"Production controller excludes nearby actors and ordinary targets before overlay binding");
        Check(c.Model.EncounterName=="Training Scarecrow"&&c.Model.ForecastIsDummy,"Production controller resolves packaged dummy metadata through the compact view model");
        c.ClearMeter();c.Preferences.CaptureScope="Self and party";c.ApplyPreferences();
        var partyAt=DateTimeOffset.UtcNow.AddSeconds(-4);
        c.ObserveIdentity(new(17,"FixtureSelf","Ranger",true,1304));
        c.ObserveIdentity(new(18,"FixtureAlly","Cleric",false));
        c.ObserveParty(new([new("FixtureSelf",1304,"Ranger",50),new("FixtureAlly",1303,"Cleric",45),new("UnboundAlly",1303,"Templar",50)],partyAt));c.RefreshScreen();
        Check(c.Model.Rows.Count==3&&c.Model.Latest!.Players.Count==0&&c.Model.Rows.All(r=>r.DisplayRate=="—"),"Party roster appears before combat with unknown rates and no synthetic combat records");
        Check(c.Model.Rows.Single(r=>r.Name=="UnboundAlly").ActorId==0&&c.Model.Rows.Single(r=>r.Name=="FixtureAlly").Info.Contains("server 1303"),"Roster-only members expose observed class/level/server without fabricated combat IDs");
        var waitingRow=c.Model.Rows.Single(r=>r.Name=="UnboundAlly");c.RefreshScreen();
        Check(ReferenceEquals(waitingRow,c.Model.Rows.Single(r=>r.Name=="UnboundAlly")),"Roster members waiting for identity retain their row between refreshes");
        c.Model.ShowHealing=false;c.OnCombat(own with{Time=partyAt.AddSeconds(1),ActorName="FixtureSelf"});
        c.OnCombat(own with{Time=partyAt.AddSeconds(2),ActorId=18,ActorName="FixtureAlly",ActorClass="Cleric",IsSelf=false,Amount=200});c.RefreshScreen();
        Check(c.Model.Rows.Single(r=>r.ActorId==18).RawDamage==200&&c.Model.Rows.Count==3,"Real party damage replaces the waiting roster row without duplicating members");
        var allyRow=c.Model.Rows.Single(r=>r.ActorId==18);
        c.OnCombat(own with{Time=partyAt.AddSeconds(2.05),Amount=1000});c.RefreshScreen();
        Check(ReferenceEquals(allyRow,c.Model.Rows.Single(r=>r.ActorId==18))&&c.Model.OverlayRows.First().ActorId==17,"Live ranking moves existing rows while preserving each member's identity");
        c.ObserveHealth(new(18,900,1000,partyAt.AddSeconds(2.1)));
        c.OnCombat(own with{Time=partyAt.AddSeconds(2.5),Kind=AscendAPP.Core.Combat.CombatKind.Heal,Amount=300,TargetId=18,TargetName="FixtureAlly",TargetNpcCode=null});
        c.ObserveHealth(new(18,1000,1000,partyAt.AddSeconds(2.6)));c.RefreshScreen();c.Model.ShowHealing=true;
        Check(c.Model.Latest!.TotalHealing==100&&c.Model.Rows.Single(r=>r.ActorId==17).RawHealing==100,"Production controller reconciles a 300 raw heal to 100 observed HP restored");
        Check(c.Model.HealingInfo.Contains("200 excluded")&&Descendants(overlay).OfType<TextBlock>().Single(x=>x.Name=="HealingInfo").Visibility==Visibility.Visible,"Healing overlay explains excluded amounts next to HP-based eHPS");
        c.ObserveParty(new([new("FixtureSelf",1304,"Ranger",50)],DateTimeOffset.UtcNow));c.ResetEncounter();
        Check(c.Model.Rows.Count==1&&c.Model.Rows.Single().Name=="FixtureSelf"&&c.Model.Latest!.Players.Count==0,"Leaving party removes departed waiting rows while reset retains the actual self roster");
        c.Preferences.CaptureScope="Self only";c.ApplyPreferences();
        Check(c.Model.Rows.Count==0,"Self-only preference hides party roster rows");
        var t=DateTimeOffset.UtcNow;c.OnCombat(own with{Time=t});
        c.RefreshScreenAt(t.AddMilliseconds(10));
        var stableRow=c.Model.Rows.Single();var stableOverlay=c.Model.OverlayRows;
        int changes=0;c.Model.OverlayRows.CollectionChanged+=(_,_)=>changes++;
        for(int pass=0;pass<10;pass++)c.RefreshScreenAt(t.AddMilliseconds(20+pass*10));
        Check(ReferenceEquals(stableRow,c.Model.Rows.Single())&&ReferenceEquals(stableOverlay,c.Model.OverlayRows)&&changes==0,"Unchanged refreshes reuse player objects and the bound collection without item resets");
        Check(c.Model.Latest!.Players.Single().Skills.Count==0&&c.AnalysisSnapshot().Players.Single().Skills.Count>0,"Compact refresh avoids detail allocation while analysis still receives full skills");
        Check(c.RefreshInterval.TotalMilliseconds==c.Preferences.UpdateMs,"Active combat keeps the configured refresh rate");
        c.ObserveHealth(new(17,900,1000,t.AddMilliseconds(100)));
        c.OnCombat(own with{Time=t.AddMilliseconds(150),Kind=AscendAPP.Core.Combat.CombatKind.Heal,Amount=300,TargetId=17,TargetName="FixtureSelf",TargetNpcCode=null});
        c.RefreshScreenAt(t.AddMilliseconds(250));c.RefreshScreenAt(t.AddMilliseconds(450));
        Check(c.Model.Latest!.TotalHealing==0,"Rapid refreshes keep the HP reconciliation window open");
        c.ObserveHealth(new(17,1000,1000,t.AddMilliseconds(500)));c.RefreshScreenAt(t.AddMilliseconds(700));
        Check(c.Model.Latest!.TotalHealing==100,"Pending healing survives several refreshes and scores once after its HP update");
        c.RefreshScreenAt(t.AddMilliseconds(950));Check(c.Model.Latest!.TotalHealing==100,"Repeated refreshes do not duplicate reconciled healing");
        c.ClearMeter();Check(c.RefreshInterval.TotalMilliseconds>=1000,"Idle meter backs off to one-second refreshes");
        settings.SelectPage("Display");var smooth=Descendants(settings).OfType<CheckBox>().Single(x=>AutomationProperties.GetName(x)=="Smooth number transitions");
        smooth.IsChecked=false;Check(!c.Preferences.SmoothNumbers,"Display setting disables smooth number transitions");
        var number=new AscendAPP.Desktop.Controls.SmoothText{Text="123",TransitionsEnabled=true};
        number.Text="456";Check(number.Text=="456"&&!number.TransitionRunning,"Unloaded number controls show the exact value without starting animation");
        number.StartTransition();Check(number.TransitionRunning,"WPF number transition creates an animation clock");
        number.TransitionsEnabled=false;Check(!number.TransitionRunning&&number.Text=="456","Disabling transitions removes the clock and preserves the exact number");
        var animationHost=new Window{Content=number,Width=100,Height=30,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,AllowsTransparency=true,Opacity=0};
        animationHost.Show();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        number.StartTransition();var frame=new System.Windows.Threading.DispatcherFrame();
        var endAnimation=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};
        endAnimation.Tick+=(_,_)=>{endAnimation.Stop();frame.Continue=false;};endAnimation.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
        Check(!number.TransitionRunning&&number.Text=="456","A real WPF number transition completes and removes its animation clock");
        animationHost.Close();
        Check(!AscendAPP.Desktop.Controls.SmoothText.ShouldAnimate(true,true,true,false)&&
            !AscendAPP.Desktop.Controls.SmoothText.ShouldAnimate(true,true,false,true),"Reduced motion and hidden overlays suppress number animations");

        settings.SelectPage("General");
        Check(Descendants(settings).OfType<TextBlock>().Any(x=>x.Text.Contains(c.UserDataDirectory))&&
            Descendants(settings).OfType<Button>().Any(x=>AutomationProperties.GetName(x)=="Import older settings"),"General settings exposes the shared data folder and legacy import action");
        string profileFixtureBase=Path.Combine(AppContext.BaseDirectory,"profile-fixtures");
        string profileTest=Path.Combine(profileFixtureBase,"AscendAPP-ui-profile-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileTest);
        try
        {
            string oldFolder=Path.Combine(profileTest,"old"),newFolder=Path.Combine(profileTest,"new"),sharedFolder=Path.Combine(profileTest,"shared");
            Directory.CreateDirectory(oldFolder);Directory.CreateDirectory(newFolder);
            AscendAPP.Core.Settings.PreferencesStore.Write(Path.Combine(oldFolder,"settings.json"),new(){CapturePolicyVersion=1,CaptureScope="Self only",UiScale=1.25,SmoothNumbers=false,WorldTimerServerId=1304,ShowEventOverlay=true,AutoCapture=false});
            var savedAt=DateTimeOffset.UtcNow;
            File.WriteAllText(Path.Combine(oldFolder,"characters.json"),JsonSerializer.Serialize(new[]{new MeterController.CharacterTickets("Fixture character",1304,new[]{new AscendAPP.Core.Protocol.TicketBalance(60000001,75,10,savedAt)},savedAt)}));
            File.WriteAllText(Path.Combine(oldFolder,"fieldbosses.json"),JsonSerializer.Serialize(new[]{new AscendAPP.Core.Game.FieldBossObservation(1304,1010,2101074,false,savedAt.AddHours(1).ToUnixTimeMilliseconds(),savedAt),new AscendAPP.Core.Game.FieldBossObservation(2301,1010,2101074,false,savedAt.AddHours(8).ToUnixTimeMilliseconds(),savedAt.AddMinutes(1))}));
            var profile=AscendAPP.Core.Storage.UserDataStore.Open(sharedFolder,newFolder,()=>new[]{oldFolder});
            using(var upgraded=new MeterController(preview:true,profile:profile))
            {
                Check(upgraded.UserDataDirectory==sharedFolder&&upgraded.Preferences.UiScale==1.25&&!upgraded.Preferences.SmoothNumbers&&
                    upgraded.Preferences.CaptureScope=="Self only"&&upgraded.Preferences.ShowEventOverlay,"A new desktop build loads the migrated profile into its actual preferences");
                Check(upgraded.Model.Preferences==upgraded.Preferences,"The overlay shares the migrated preference instance");
                Check(upgraded.Model.Tickets.Any(t=>t.Character.StartsWith("Fixture character")&&t.Energy=="85")&&
                    upgraded.Model.FieldBossTimers.Any(t=>t.Name=="Eternal Gartua"&&t.Remaining!="—"&&t.Tooltip.Contains("Server 1304")),"Migrated ticket balances and server boss snapshots reach the actual desktop bindings");
                upgraded.Preferences.HideNames=true;AscendAPP.Core.Settings.PreferencesStore.Write(profile.SettingsPath,upgraded.Preferences);
            }
            using(var next=new MeterController(preview:true,profile:AscendAPP.Core.Storage.UserDataStore.Open(sharedFolder,newFolder)))
            {
                Check(next.Preferences.HideNames&&next.Preferences.UiScale==1.25&&!next.Preferences.AutoCapture,"A subsequent desktop instance reads the changes saved by the previous build");
                next.ImportSettingsFile(Path.Combine(oldFolder,"settings.json"));
                Check(!next.Preferences.HideNames&&next.Model.Preferences==next.Preferences&&Directory.GetFiles(sharedFolder,"settings.json.backup-*").Length==1,"Legacy import updates the active view model and keeps a backup");
            }
        }
        finally
        {
            string resolved=Path.GetFullPath(profileTest);
            if(!resolved.StartsWith(Path.GetFullPath(profileFixtureBase)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("AscendAPP-ui-profile-"))throw new InvalidOperationException("Unexpected fixture cleanup path");
            Directory.Delete(resolved,true);
        }
        DungeonUiChecks.Run(Check);
        TimerContinuityUiChecks.Run(Check);
        AutomaticScoreUiChecks.Run(Check);
        CompactMeterUiChecks.Run(Check);
        MeterEventsUiChecks.Run(Check);
        HistoryStripUiChecks.Run(Check);
        PetUiChecks.Run(Check);
        AppearanceUiChecks.Run(Check);
        LootCounterUiChecks.Run(Check);
        OverlayInteractionUiChecks.Run(Check);
        settings.Close();overlay.Close();PresentationTraceSources.DataBindingSource.Flush();PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        Check(string.IsNullOrWhiteSpace(warnings.ToString()),"No WPF binding warnings during settings interactions");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(new{Passed=results.Count,Checks=results},new JsonSerializerOptions{WriteIndented=true}));
    }
    private sealed class CloseWindow(Window window):IDisposable{public Window Window=>window;public void Dispose()=>window.Close();}
}
