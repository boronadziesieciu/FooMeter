using System.Windows;
using System.Windows.Controls;
using AscendAPP.Core.Combat;
namespace AscendAPP.Desktop;
internal static class MeterEventsUiChecks
{
    public static void Run(Action<bool,string> check)
    {
        var core=typeof(CombatEngine).Assembly;
        check(!core.GetTypes().Any(t=>t.Name.Contains("TargetSkill",StringComparison.Ordinal)||t.Name.Contains("Cooldown",StringComparison.Ordinal)||t.Name is "TargetProfileClient" or "PlayerSelectionReader" or "TrackerPreset"),"Compiled core has no target-build lookup, cooldown tracker, cast/selection reader or skill timer preset types");
        check(!typeof(MeterController).Assembly.GetTypes().Any(t=>t.Name.StartsWith("TargetSkills",StringComparison.Ordinal)),"Compiled desktop has no skill-tracker overlay, service or refresh timer");
        using var c=new MeterController(preview:true);var meter=c.CreateOverlay();var events=c.CreateEventOverlay();var settings=new SettingsWindow(c);
        try
        {
            check(!meter.ActionsMenu().Items.OfType<MenuItem>().Any(i=>(i.Header?.ToString()??"").Contains("target skills",StringComparison.OrdinalIgnoreCase)),"Meter actions offer no retired target-skill overlay");
            c.Preferences.UiScale=1.2;c.Preferences.OverlayOpacity=.8;c.ApplyPreferences();
            check(Math.Abs(events.Width-360)<.001&&events.Opacity==.8,"Existing profiles retain the inherited event scale and opacity");
            c.Preferences.EventOverlayScale=.8;c.Preferences.EventOverlayOpacity=.45;c.ApplyPreferences();
            check(Math.Abs(events.Width-240)<.001&&events.Opacity==.45&&Math.Abs(meter.Width-504)<.001&&meter.Opacity==.8,"Event-only size and transparency do not resize or fade the DPS meter");
            c.Preferences.UiScale=1.5;c.ApplyPreferences();check(events.Width==240&&events.Opacity==.45,"Changing the meter scale preserves a separate event size");
            settings.SelectPage("Event tracking");var nav=(StackPanel)settings.FindName("Navigation");
            check(!nav.Children.OfType<Button>().SelectMany(b=>((StackPanel)b.Content).Children.OfType<TextBlock>()).Any(t=>t.Text is "Target skills" or "Buff tracking"),"Settings exposes only current meter/event features");
            var page=(StackPanel)settings.FindName("Page");IEnumerable<DependencyObject> Children(DependencyObject n){yield return n;foreach(var x in LogicalTreeHelper.GetChildren(n).OfType<DependencyObject>())foreach(var child in Children(x))yield return child;}
            var follow=Children(page).OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Follow meter appearance");follow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(c.Preferences.EventOverlayScale is null&&c.Preferences.EventOverlayOpacity is null&&events.Width==450&&events.Opacity==.8,"Follow meter appearance resets overrides through the real settings action");
        }
        finally{settings.Close();meter.Close();}
        var t=DateTimeOffset.UtcNow;
        EncounterSnapshot Fight(int ago,int code,string name)=>new(Guid.NewGuid(),t.AddMinutes(-ago),10,false,1000,0,100,[new(17,"Hero",1000,0,100,0,1,2,.5,[],"Ranger",true)],Boss:new(900,code,name,null,null));
        EncounterSnapshot one=Fight(1,123,"Fixture Boss"),two=Fight(2,123,"Fixture Boss"),other=Fight(3,456,"Different Boss");var history=new FightHistoryWindow([other,one,two]);
        try
        {
            var list=(ListBox)history.FindName("Fights");var search=(TextBox)history.FindName("SearchBox");var baseline=(ComboBox)history.FindName("Baseline");var summary=(TextBlock)history.FindName("ComparisonSummary");
            check(list.Items.Count==3&&baseline.Items.Count==1&&summary.Text.Contains("Party eDPS 0.0%"),"Actual history controls select only same-target baselines and show comparison values");
            search.Text="different";check(list.Items.Count==1&&baseline.Items.Count==0&&summary.Text.Contains("second fight"),"History search and no-baseline state update without leaving a stale comparison");
            search.Text="no such boss";check(list.Items.Count==0&&!((Button)history.FindName("OpenButton")).IsEnabled,"An empty history search disables breakdown actions");
            search.Text="Ranger";check(list.Items.Count==3,"Class search restores matching encounters through the native search box");
            var content=(FrameworkElement)history.Content;content.Measure(new Size(760,540));content.Arrange(new Rect(0,0,760,540));content.UpdateLayout();check(list.ActualHeight>150&&summary.ActualHeight>=50,"History layout leaves usable list and comparison space");
            IEnumerable<DependencyObject> Visuals(DependencyObject n){yield return n;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(n);i++)foreach(var v in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(n,i)))yield return v;}
            var labels=Visuals(baseline).OfType<TextBlock>().Select(x=>x.Text).ToArray();
            check(labels.Any(x=>x.Contains("Fixture Boss"))&&!labels.Any(x=>x.Contains("Item {")),"Custom combo template renders the display label instead of an internal record dump");
        }
        finally{history.Close();}
    }
}
