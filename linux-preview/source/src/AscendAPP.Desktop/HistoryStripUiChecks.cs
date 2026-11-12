using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Storage;
using AscendAPP.Core.Settings;
namespace AscendAPP.Desktop;
internal static class HistoryStripUiChecks
{
    public static void Run(Action<bool,string> check)
    {
        using(var c=new MeterController(preview:true))
        {
            var meter=c.CreateOverlay();var events=c.CreateEventOverlay();
            try
            {
                c.Showcase();var latest=c.Model.Latest!;c.ToggleCombatStrip();
                double Layout(){var content=(FrameworkElement)meter.Content;content.Measure(new Size(meter.Width,double.PositiveInfinity));content.Arrange(new Rect(0,0,meter.Width,content.DesiredSize.Height));content.UpdateLayout();return content.DesiredSize.Height;}
                double height=Layout();var strip=(Grid)meter.FindName("StripPanel");
                check(c.Preferences.CombatStrip&&strip.Visibility==Visibility.Visible&&height<=48&&((FrameworkElement)meter.FindName("Header")).Visibility==Visibility.Collapsed,"Combat strip replaces the full layout with one short row");
                check(c.Model.Latest!.Id==latest.Id&&c.Model.Latest.TotalDamage==latest.TotalDamage&&c.Model.Rows.Count==9,"Switching to strip view does not reset combat totals or player rows");
                check(c.Model.StripSelfRate==c.Model.Rows.Single(r=>r.IsSelf).DisplayRate&&c.Model.StripPartyRate==c.Model.PartyDps,"Strip personal and party values bind to the existing measured rates");
                var elements=strip.Children.OfType<FrameworkElement>().ToArray();var bounds=elements.Select(e=>e.TransformToAncestor(strip).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight))).ToArray();
                check(bounds.All(b=>b.Left>=-1&&b.Right<=strip.ActualWidth+1)&&bounds.Zip(bounds.Skip(1),(a,b)=>a.Right<=b.Left+1).All(ok=>ok),"Strip content and controls fit one row without overlapping columns");
                c.Preferences.UiScale=.8;c.ApplyPreferences();check(Layout()<=48,"The strip stays on one row at the smallest saved scale");
                c.Preferences.UiScale=1.5;c.ApplyPreferences();check(Layout()<=72&&meter.Width<=SystemParameters.WorkArea.Width+1,"Large strip scale fits the work area without moving controls off screen");
                c.Preferences.PositionLocked=true;c.ApplyPreferences();meter.Interaction.SampleForTest(new(true,false));check(meter.Interaction.TemporaryInteractive&&!meter.Interaction.EffectiveClickThrough,"Alt temporarily enables interaction with a locked combat strip");meter.Interaction.SampleForTest(new(false,false));check(meter.Interaction.EffectiveClickThrough,"Alt release restores strip click-through protection");
                c.Preferences.PositionLocked=false;c.Preferences.UiScale=1;c.ApplyPreferences();((Button)strip.Children.OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Expand combat meter")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(!c.Preferences.CombatStrip&&meter.Width==420&&Layout()>200,"Expand restores the familiar compact meter without a separate window");
                check(c.Model.CaptureState=="Demo"&&((Button)meter.FindName("CaptureIndicator")).ToolTip!.ToString()!.Contains("Synthetic"),"The visible capture badge labels showcase values as synthetic demo data");
                var counters=new AscendAPP.Core.Protocol.CaptureDiagnostics(10,5,500,0,0,0,false,0,0,0,0,DateTimeOffset.UtcNow);c.Model.ApplyCaptureHealth(AscendAPP.Core.Protocol.CaptureHealth.Evaluate(true,"LIVE",counters,false,false,false,DateTimeOffset.UtcNow));
                var badge=(Button)meter.FindName("CaptureIndicator");var label=((StackPanel)badge.Content).Children.OfType<TextBlock>().Single();check(label.Text=="No frames"&&badge.ToolTip!.ToString()!.Contains("recognized frames"),"The real capture badge shows an unrecognized-framing state and its explanation");
                var engine=new CombatEngine();engine.Add(new(DateTimeOffset.UtcNow,18,"Other",900,"Training dummy",42,100,CombatKind.Damage));c.Model.Apply(engine.Snapshot(DateTimeOffset.UtcNow));check(c.Model.StripSelfRate=="—","An unidentified self never borrows another player's rate in strip view");
                c.Model.ShowHealing=true;check(c.Model.StripSelfTitle=="You HPS","Healing mode has an accurate personal-rate tooltip in strip view");
                check(events.Width==300,"Strip toggling does not change the independent event overlay");
            }
            finally{meter.Close();}
        }
        string fixtureBase=System.IO.Path.Combine(AppContext.BaseDirectory,"profile-fixtures"),root=System.IO.Path.Combine(fixtureBase,"AscendAPP-history-ui-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(root);string shared=System.IO.Path.Combine(root,"shared"),app=System.IO.Path.Combine(root,"app");System.IO.Directory.CreateDirectory(shared);System.IO.Directory.CreateDirectory(app);
        try
        {
            PreferencesStore.Write(System.IO.Path.Combine(shared,"settings.json"),new(){AutoCapture=false,CapturePolicyVersion=1,CaptureScope="Self only",CombatStrip=true});
            var t=DateTimeOffset.UtcNow;CombatEvent Hit()=>new(t,17,"FixtureSelf",900,"Training dummy",42,100,CombatKind.Damage,ActorClass:"Ranger",IsSelf:true,TargetNpcCode:2400032);
            using(var first=new MeterController(preview:true,profile:UserDataStore.Open(shared,app))){first.OnCombat(Hit());first.ResetEncounter();check(first.FlushHistory().GetAwaiter().GetResult()&&first.HistoryItems.Count==1,"Actual controller completion saves a captured encounter outside the UI thread");}
            Guid id;
            using(var next=new MeterController(preview:true,profile:UserDataStore.Open(shared,app))){id=next.HistoryItems.Single().Id;check(next.Preferences.CombatStrip&&next.Model.Latest!.Players.Count==0&&next.HistoryItems.Single().Players.Single().Skills.Single().Damage==100,"A new controller restores saved history and strip preference without replaying old DPS as live combat");next.ClearMeter();check(next.HistoryItems.Count==1,"Clear current meter preserves the saved fight archive");next.Showcase();next.ResetEncounter();check(next.HistoryItems.Count==1,"Synthetic session history is kept out of the saved-fight browser as well as the archive file");}
            using(var afterDemo=new MeterController(preview:true,profile:UserDataStore.Open(shared,app))){check(afterDemo.HistoryItems.Count==1,"Showcase and demo encounters never pollute persistent fight history");afterDemo.OnCombat(Hit() with{Time=t.AddMinutes(1)});}
            using(var afterExit=new MeterController(preview:true,profile:UserDataStore.Open(shared,app)))
            {
                check(afterExit.HistoryItems.Count==2&&afterExit.HistoryItems.First().Interrupted,"App shutdown saves an in-progress encounter as partial");
                var history=new FightHistoryWindow(afterExit.HistoryItems,onDelete:afterExit.DeleteHistory,onClear:afterExit.ClearSavedHistory,confirm:_=>true);
                try
                {
                    string export=System.IO.Path.Combine(root,"selected.json");history.ExportSelected(export);check(System.IO.File.Exists(export),"The history selection exports its complete saved snapshot");
                    ((Button)history.FindName("DeleteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(afterExit.HistoryItems.Count==1,"Delete selected updates both actual controller history and the dialog");
                    ((Button)history.FindName("ClearHistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(afterExit.HistoryItems.Count==0&&((ListBox)history.FindName("Fights")).Items.Count==0,"Confirmed clear removes saved encounters while leaving the meter usable");
                }
                finally{history.Close();}
                afterExit.FlushHistory().GetAwaiter().GetResult();
            }
            using(var cleared=new MeterController(preview:true,profile:UserDataStore.Open(shared,app)))check(cleared.HistoryItems.Count==0,"Deleting history through the native controls survives another app restart");
        }
        finally{string full=System.IO.Path.GetFullPath(root);if(!full.StartsWith(System.IO.Path.GetFullPath(fixtureBase)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected fixture path");System.IO.Directory.Delete(full,true);}
    }
}
