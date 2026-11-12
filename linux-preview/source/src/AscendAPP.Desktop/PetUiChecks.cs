using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using AscendAPP.Core.Pets;
namespace AscendAPP.Desktop;
internal static class PetUiChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var c=new MeterController(preview:true);
        var meter=c.CreateOverlay();var pet=c.CreatePetOverlay();var settings=new SettingsWindow(c);
        try{
            double Layout(){var e=(FrameworkElement)pet.Content;e.Measure(new Size(pet.Width,double.PositiveInfinity));e.Arrange(new Rect(0,0,pet.Width,e.DesiredSize.Height));e.UpdateLayout();return e.DesiredSize.Height;}
            check(meter.Title.StartsWith("FooMeter")&&settings.Title.StartsWith("FooMeter")&&pet.Title.StartsWith("FooMeter"),"FooMeter name is consistent across the meter, settings and pet overlay");
            check(c.Preferences.PetHideCompleted&&!c.Preferences.PetOnlyPinned&&((CheckBox)pet.FindName("HideCompleted")).IsChecked==true,"Opening the pet overlay preserves saved/default filter switches");
            Layout();var initial=((ItemsControl)pet.FindName("PetList")).Items.Cast<PetOverlayWindow.DisplayPet>().ToArray();
            check(initial.Length==207&&initial.All(r=>r.Value.Level is null),"Unsynchronized pet rows do not display invented levels or counts");
            c.Pets.Observe(new PetCollectionObservation("FixtureSelf",1304,[new(1006,1,13),new(1097,2,40)],DateTimeOffset.UtcNow));
            c.Pets.Observe(new PetGainObservation("FixtureSelf",1304,1006,1,DateTimeOffset.UtcNow));pet.Refresh();Layout();
            check(((TextBlock)pet.FindName("PickupText")).Text.Contains("14/25")&&((Border)pet.FindName("Pickup")).Visibility==Visibility.Visible,"Actual pickup notification binds pet name, gain and remaining souls");
            var search=(TextBox)pet.FindName("Search");search.Text="Sparkie";pet.Refresh();Layout();
            var list=(ItemsControl)pet.FindName("PetList");
            check(list.Items.Count>0&&list.Items.Cast<PetOverlayWindow.DisplayPet>().All(r=>r.Name.Contains("Sparkie")),"Pet search filters the visible farming list");
            search.Text="Karnif";pet.Refresh();Layout();var generator=list.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
            static IEnumerable<DependencyObject> Visuals(DependencyObject o){yield return o;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var x in Visuals(VisualTreeHelper.GetChild(o,i)))yield return x;}
            var button=generator is not null?Visuals(generator).OfType<Button>().First():null;
            check(button is not null,"A rendered pet row exposes a pin button");
            button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(c.Preferences.PinnedPets.Count==1,"Pinning a rendered pet updates the persisted preference model");
            search.Text="";((CheckBox)pet.FindName("PinnedOnly")).IsChecked=true;pet.Refresh();Layout();check(list.Items.Count==1,"Pinned-only mode keeps the selected farming target visible");
            var stable=list.ItemsSource;for(int refresh=0;refresh<10;refresh++)pet.Refresh();check(ReferenceEquals(stable,list.ItemsSource),"Unchanged pet refreshes reuse rendered rows without list churn");
            c.Pets.Invalidate("fixture gap");pet.Refresh();check(((TextBlock)pet.FindName("Status")).Text.Contains("needs sync")&&!((PetOverlayWindow.DisplayPet)list.Items[0]).Value.Confirmed,"Native pet rows and status explicitly distinguish saved progress after a capture gap");
            double oldMeter=meter.Width;c.Preferences.PetOverlayScale=1.5;c.Preferences.PetOverlayOpacity=.55;c.ApplyPreferences();Layout();
            check(pet.Width==525&&pet.Opacity==.55&&meter.Width==oldMeter,"Pet scaling and opacity are independent of DPS meter appearance");
            c.Preferences.PetOverlayScale=.8;c.ApplyPreferences();check(Layout()<440&&pet.Width==280,"Smallest pet overlay remains bounded and readable");
            c.Preferences.PetOverlayLocked=true;c.ApplyPreferences();check(pet.Interaction.EffectiveClickThrough,"Locked pet overlay passes clicks through");
            pet.Interaction.SampleForTest(new(true,false));check(pet.Interaction.TemporaryInteractive&&!pet.Interaction.EffectiveClickThrough,"Alt temporarily restores pet overlay interaction");
            settings.SelectPage("Pet tracking");check(c.Preferences.PetHideCompleted,"Pet settings render without resetting the completed filter");
            c.Showcase();c.ToggleCombatStrip();check(c.Model.Rows.Count>0&&c.Preferences.CombatStrip,"Pet tracking leaves the existing combat meter and one-line strip available");
        }finally{settings.Close();pet.CloseForExit();meter.Close();}
    }
}
