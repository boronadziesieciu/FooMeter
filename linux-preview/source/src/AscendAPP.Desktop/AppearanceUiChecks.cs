using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using AscendAPP.Core.Settings;
using AscendAPP.Core.Pets;
using AscendAPP.Desktop.Services;
using AscendAPP.Desktop.Controls;
namespace AscendAPP.Desktop;
internal static class AppearanceUiChecks
{
    private static IEnumerable<DependencyObject> Children(DependencyObject n){yield return n;foreach(var x in LogicalTreeHelper.GetChildren(n).OfType<DependencyObject>())foreach(var child in Children(x))yield return child;}
    private static double Layout(Window w){var e=(FrameworkElement)w.Content;e.Measure(new Size(w.Width,double.PositiveInfinity));e.Arrange(new Rect(0,0,w.Width,e.DesiredSize.Height));e.UpdateLayout();return e.DesiredSize.Height;}
    public static void Run(Action<bool,string> check)
    {
        using var c=new MeterController(preview:true);var meter=c.CreateOverlay();var events=c.CreateEventOverlay();var pets=c.CreatePetOverlay();var settings=new SettingsWindow(c);var popup=c.CreatePetPickup();
        try{
            double eventHeight=Layout(events);
            check(events.Width==300&&eventHeight<=260,"Six event/boss timers fit a compact single-line overlay under 260px tall");
            var oldBrush=(SolidColorBrush)AppearanceTheme.Brush("#F28DB8");var oldLogo=AppearanceTheme.LogoImage;
            c.Showcase();var before=c.Model.Latest!;
            c.Preferences.AccentColor="#81B9FF";c.Preferences.PanelColor="#101214";c.ApplyPreferences();Layout(meter);Layout(events);Layout(pets);
            check(((SolidColorBrush)((Border)meter.FindName("Shell")).Background).Color==(Color)ColorConverter.ConvertFromString("#101214"),"An already-created meter immediately adopts the chosen panel background");
            check(oldBrush.Color==(Color)ColorConverter.ConvertFromString("#81B9FF")&&!oldBrush.IsFrozen,"Live accent brushes update in place for existing controls");
            check(((SolidColorBrush)((Border)events.FindName("Shell")).Background).Color==((SolidColorBrush)((Border)meter.FindName("Shell")).Background).Color,"Event and damage overlays share the same selected colors");
            check(!ReferenceEquals(oldLogo,AppearanceTheme.LogoImage)&&meter.Icon==AppearanceTheme.LogoImage,"Changing the accent updates the running app's flame and window icon");
            using(var icon=AppearanceTheme.CreateTrayIcon())check(icon.Width==64&&icon.Height==64,"The Windows tray receives a valid recolored flame icon");
            c.Preferences.LogoFollowsAccent=false;c.Preferences.LogoColor="#EBC574";c.Preferences.AccentColor="#83D5C4";c.ApplyPreferences();
            check(((SolidColorBrush)Application.Current.Resources["Logo"]).Color==(Color)ColorConverter.ConvertFromString("#EBC574")&&oldBrush.Color==(Color)ColorConverter.ConvertFromString("#83D5C4"),"Logo color can differ from the app accent");
            check(c.Model.Latest!.Id==before.Id&&c.Model.Latest.TotalDamage==before.TotalDamage,"Appearance changes preserve the active encounter and measured totals");
            settings.SelectPage("Appearance");
            var field=Children(settings).OfType<TextBox>().Single(x=>AutomationProperties.GetName(x)=="App accent hex");
            void Commit(){field.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,field,null){RoutedEvent=Keyboard.LostKeyboardFocusEvent});}
            field.Text="invalid";Commit();check(c.Preferences.AccentColor=="#83D5C4","Invalid custom RGB input cannot corrupt the saved theme");
            field.Text="#BBA0FF";Commit();check(c.Preferences.AccentColor=="#BBA0FF"&&oldBrush.Color==(Color)ColorConverter.ConvertFromString("#BBA0FF"),"The actual color settings field applies a custom RGB choice live");
            c.Preferences.PanelColor="#FFFFFF";c.Preferences.AccentColor="#FFFFFF";c.ApplyPreferences();
            var surface=((SolidColorBrush)Application.Current.Resources["Surface"]).Color;var ink=((SolidColorBrush)Application.Current.Resources["Ink"]).Color;var accent=((SolidColorBrush)Application.Current.Resources["Accent"]).Color;
            check(AppearanceTheme.Contrast(surface,ink)>=7&&AppearanceTheme.Contrast(surface,accent)>=3,"Light backgrounds and low-contrast color choices retain readable text and controls");
            check(typeof(PetPortrait).Assembly.GetManifestResourceNames().Count(x=>x.StartsWith("FooMeter.Pets."))==207,"All 207 ID-matched pet portraits are embedded for offline use");
            var portraits=Enumerable.Range(1001,300).Select(x=>PetPortrait.Artwork((uint)x)).Where(x=>x is not null).ToArray();
            check(portraits.Length==207&&portraits.All(x=>x!.PixelWidth==64&&x.PixelHeight==64&&x.IsFrozen),"Bundled pet portraits decode to cached, frozen 64px images");
            check(PetPortrait.Artwork(999999) is null&&ReferenceEquals(PetPortrait.Artwork(1006),PetPortrait.Artwork(1006)),"Unknown pets use a fallback and repeated portraits reuse the cache");
            popup.PreviewCounter(renderOnly:true);Layout(popup);
            check(((TextBlock)popup.FindName("Count")).Text=="14/25"&&((TextBlock)popup.FindName("PetName")).Text.Contains("DEMO"),"GIF-style pickup preview keeps the count prominent and labels synthetic data");
            c.Pets.Observe(new PetGainObservation("Fixture",1304,1006,2,DateTimeOffset.UtcNow));popup.Update(renderOnly:true);Layout(popup);
            check(((TextBlock)popup.FindName("Count")).Text=="+2"&&((TextBlock)popup.FindName("Detail")).Text.Contains("waiting"),"The small pickup counter shows real +N gains while collection totals are unknown");
            popup.Interaction.SampleForTest(new(true,false));check(!popup.Interaction.EffectiveClickThrough&&popup.Interaction.CanDrag,"Alt temporarily makes the pickup counter draggable");
            popup.Interaction.SampleForTest(new(false,false));check(popup.Interaction.EffectiveClickThrough,"The pickup counter normally passes clicks through to the game");
            c.Preferences.AccentColor=AppearanceColors.DefaultAccent;c.Preferences.PanelColor=AppearanceColors.DefaultPanel;c.Preferences.LogoColor=AppearanceColors.DefaultLogo;c.Preferences.LogoFollowsAccent=true;c.ApplyPreferences();
            c.Preferences.PetOnlyPinned=true;c.Preferences.PinnedPets=[1006];c.Pets.Observe(new PetCollectionObservation("Fixture",1304,[new(1006,1,14)],DateTimeOffset.UtcNow));pets.ApplyPreferences();pets.Refresh();Layout(pets);
            var row=((ItemsControl)pets.FindName("PetList")).Items.Cast<PetOverlayWindow.DisplayPet>().Single();
            check(row.Progress=="14/25"&&row.Value.Confirmed,"Portrait pet rows retain their actual soul count in the dedicated number column");
            check(System.Text.Json.JsonSerializer.Serialize(c.PetDiagnostics).Contains("PetStateFrames"),"Exportable pet diagnostics include raw-frame and published-event stages");
        }finally{settings.Close();popup.CloseForExit();pets.CloseForExit();meter.Close();}
    }
}
