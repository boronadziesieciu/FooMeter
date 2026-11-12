using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AscendAPP.Desktop.Controls;
using AscendAPP.Desktop.ViewModels;
namespace AscendAPP.Desktop;
internal static class CompactMeterUiChecks
{
    static IEnumerable<DependencyObject> Visuals(DependencyObject node){yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var c in Visuals(VisualTreeHelper.GetChild(node,i)))yield return c;}
    public static void Run(Action<bool,string> check)
    {
        string[] classes=["Gladiator","Templar","Assassin","Ranger","Sorcerer","Spiritmaster","Cleric","Chanter","Brawler"];
        check(classes.All(c=>ClassBadge.Artwork(c) is {IsFrozen:true,PixelWidth:>0,PixelHeight:>0}),"Every class has immutable embedded original artwork without external PNG files");
        check(ReferenceEquals(ClassBadge.Artwork(" Elementalist "),ClassBadge.Artwork("Spiritmaster"))&&ReferenceEquals(ClassBadge.Artwork("fighter"),ClassBadge.Artwork("Brawler"))&&ReferenceEquals(ClassBadge.Artwork("templar"),ClassBadge.Artwork("Templar")),"Class aliases, letter case and surrounding whitespace resolve to the correct shared emblem");
        check(ClassBadge.Artwork("../../other") is null&&ClassBadge.Artwork("Unknown") is null,"Unknown class names do not resolve an unrelated icon or a filesystem path");
        var core=typeof(AscendAPP.Core.Protocol.ExperimentalGlobalDecoder).Assembly;
        check(!core.GetTypes().Any(t=>t.FullName!.Contains("ForceMap",StringComparison.Ordinal)||t.Name is "OwnPositionReader" or "OwnMotionReader" or "OwnPositionDiagnostics")&&!core.GetManifestResourceNames().Any(n=>n.Contains("ForceMaps",StringComparison.Ordinal)),"Compiled core contains neither shared-map types, position readers nor map resources");
        using var c=new MeterController(preview:true);var window=c.CreateOverlay();var settings=new SettingsWindow(c);
        try
        {
            c.Showcase();var now=DateTimeOffset.UtcNow;
            foreach(var row in c.Model.Rows)row.DisplayName="A deliberately very long character name";
            c.Model.ApplyRatings(row=>new(row.ActorId,row.Name,1318,99999,999999999,now,"Synthetic layout fixture"),now);
            void Layout(){window.ApplyPreferences();var content=(FrameworkElement)window.Content;content.Measure(new Size(window.Width,double.PositiveInfinity));content.Arrange(new Rect(0,0,window.Width,content.DesiredSize.Height));content.UpdateLayout();}
            Layout();
            var lines=Visuals(window.Content as DependencyObject??window).OfType<Grid>().Where(g=>g.Name=="PlayerLine").ToArray();
            check(lines.Length==9,"All nine showcase class rows instantiate inside the compact scrolling meter");
            bool SingleLine(Grid line)
            {
                var elements=line.Children.OfType<FrameworkElement>().Where(e=>e.Visibility==Visibility.Visible).ToArray();
                if(elements.Length<5)return false;
                var bounds=elements.Select(e=>e.TransformToAncestor(line).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight))).ToArray();
                return bounds.All(b=>b.Left>=-1&&b.Right<=line.ActualWidth+1&&Math.Abs((b.Top+b.Bottom)/2-line.ActualHeight/2)<1.1)&&bounds.Zip(bounds.Skip(1),(a,b)=>a.Right<=b.Left+1).All(ok=>ok);
            }
            check(lines.All(line=>SingleLine(line)&&line.ActualHeight==26),"Name, GS, CP, rate and total occupy one 26px row without overlap");
            check(lines.All(line=>line.Children.OfType<TextBlock>().Where(t=>t.Name is "PlayerGearScore" or "PlayerCombatPower" or "PlayerRate" or "total").All(t=>t.DesiredSize.Width<=t.ActualWidth+t.Margin.Left+t.Margin.Right+1)),"Large supported GS/CP and damage values fit their measured columns");
            check(lines.All(line=>line.Children.OfType<TextBlock>().Single(t=>t.Name=="PlayerName").TextTrimming==TextTrimming.CharacterEllipsis),"Long player names trim within their column and leave ratings and damage readable");
            double nameWidth=lines[0].Children.OfType<TextBlock>().Single(t=>t.Name=="PlayerName").ActualWidth;
            c.Preferences.HideTotalDamage=true;c.Preferences.UiScale=.8;c.ApplyPreferences();Layout();
            lines=Visuals((DependencyObject)window.Content).OfType<Grid>().Where(g=>g.Name=="PlayerLine").ToArray();
            check(lines.All(SingleLine)&&lines.All(line=>line.Children.OfType<TextBlock>().Single(t=>t.Name=="total").Visibility==Visibility.Collapsed),"Smallest UI scale and hidden totals preserve a single aligned row");
            check(lines[0].Children.OfType<TextBlock>().Single(t=>t.Name=="PlayerName").ActualWidth<nameWidth,"Short names use their content width instead of reserving a wide empty column");
            var packed=lines[0];var n=packed.Children.OfType<TextBlock>().Single(t=>t.Name=="PlayerName");var gs=packed.Children.OfType<TextBlock>().Single(t=>t.Name=="PlayerGearScore");var cp=packed.Children.OfType<TextBlock>().Single(t=>t.Name=="PlayerCombatPower");
            var nr=n.TransformToAncestor(packed).TransformBounds(new Rect(0,0,n.ActualWidth,n.ActualHeight));var gr=gs.TransformToAncestor(packed).TransformBounds(new Rect(0,0,gs.ActualWidth,gs.ActualHeight));var cr=cp.TransformToAncestor(packed).TransformBounds(new Rect(0,0,cp.ActualWidth,cp.ActualHeight));
            check(gr.Left-nr.Right<=12&&cr.Left-gr.Right<=9,"GS and CP sit immediately beside the name instead of occupying distant fixed columns");
            c.Preferences.HideTotalDamage=false;c.Preferences.UiScale=1.5;c.ApplyPreferences();c.Model.ShowHealing=true;Layout();
            lines=Visuals((DependencyObject)window.Content).OfType<Grid>().Where(g=>g.Name=="PlayerLine").ToArray();
            check(lines.All(SingleLine)&&c.Model.RateTitle.StartsWith("eHPS"),"Healing rows retain the same compact geometry at the largest supported UI scale");
            var scroller=(ScrollViewer)window.FindName("PlayerScroller");check(scroller.MaxHeight<=29*c.Preferences.VisiblePlayers,"Visible-row limits use the slim row height instead of wasting space on old 44px slots");
            var menu=window.ActionsMenu();check(!menu.Items.OfType<MenuItem>().Any(i=>(i.Header?.ToString()??"").Contains("map",StringComparison.OrdinalIgnoreCase)),"The damage-overlay menu contains no removed map action");
            check(!c.DebugText.Contains("Force Map",StringComparison.OrdinalIgnoreCase),"Runtime diagnostics have no map connection or position collection section");
            settings.SelectPage("General");var nav=(StackPanel)settings.FindName("Navigation");check(!nav.Children.OfType<Button>().SelectMany(b=>((StackPanel)b.Content).Children.OfType<TextBlock>()).Any(t=>t.Text.Contains("map",StringComparison.OrdinalIgnoreCase)),"Settings navigation contains only remaining features");
        }
        finally{settings.Close();window.Close();}
    }
}
