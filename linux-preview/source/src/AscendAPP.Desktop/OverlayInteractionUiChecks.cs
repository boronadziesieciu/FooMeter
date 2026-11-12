using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AscendAPP.Desktop.Services;
using AscendAPP.Desktop.ViewModels;
namespace AscendAPP.Desktop;
internal static class OverlayInteractionUiChecks
{
    static IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;foreach(var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var x in Children(c))yield return x;}
    public static void Run(Action<bool,string> check)
    {
        int before=OverlayInteraction.RegisteredCount;
        using(var c=new MeterController(preview:true))
        {
            var main=c.CreateOverlay();var timers=c.CreateEventOverlay();
            var windows=new Window[]{main,timers};var inputs=new[]{main.Interaction,timers.Interaction};
            var handles=windows.Select(w=>new WindowInteropHelper(w).EnsureHandle()).ToArray();
            try
            {
                check(inputs.All(x=>!x.EffectiveClickThrough)&&handles.All(h=>!OverlayInput.IsClickThrough(h)),"All unlocked overlay HWNDs accept ordinary input");
                c.Preferences.PositionLocked=true;c.ApplyPreferences();
                check(main.Interaction.EffectiveClickThrough&&OverlayInput.IsClickThrough(handles[0])&&!timers.Interaction.EffectiveClickThrough,"Locking the meter enables native click-through only on that overlay");
                c.Preferences.EventOverlayLocked=true;c.ApplyPreferences();
                check(inputs.All(x=>x.EffectiveClickThrough)&&handles.All(OverlayInput.IsClickThrough),"Each independent lock sets click-through on meter and timers HWNDs");
                foreach(var input in inputs)input.SampleForTest(new(true,false));
                check(inputs.All(x=>x.TemporaryInteractive&&x.CanDrag&&!x.EffectiveClickThrough)&&handles.All(h=>!OverlayInput.IsClickThrough(h)),"Holding Alt temporarily restores native hit testing and movement across all overlays");
                check(c.Preferences.PositionLocked&&c.Preferences.EventOverlayLocked,"Temporary Alt interaction never changes the saved position locks");
                foreach(var input in inputs)input.SampleForTest(new(false,false));
                check(inputs.All(x=>!x.TemporaryInteractive&&!x.CanDrag&&x.EffectiveClickThrough)&&handles.All(OverlayInput.IsClickThrough),"Releasing Alt immediately restores all locked overlays' native protection");
                main.Interaction.SampleForTest(new(true,true));main.Interaction.HoldClickForTest();main.Interaction.SampleForTest(new(false,true));
                check(main.Interaction.TemporaryInteractive&&!OverlayInput.IsClickThrough(handles[0]),"Releasing Alt during an overlay click retains input until the button is released");
                main.Interaction.SampleForTest(new(false,false));check(main.Interaction.EffectiveClickThrough,"Releasing the click ends its temporary interaction latch");
                main.Interaction.SampleForTest(new(true,false,true));check(main.Interaction.EffectiveClickThrough&&!main.Interaction.CanDrag,"AltGr or conflicting Ctrl/Windows modifiers do not unlock a protected overlay");
                bool moved=false;check(!main.Interaction.RunDrag(()=>moved=true)&&!moved,"A locked overlay cannot move without its Alt override");
                main.Interaction.SampleForTest(new(true,true));double oldLeft=double.IsFinite(main.Left)?main.Left:100;main.Left=oldLeft;bool heldDuringDrag=false;
                bool dragged=main.Interaction.RunDrag(()=>{main.Left+=24;main.Interaction.SampleForTest(new(false,true));heldDuringDrag=!main.Interaction.EffectiveClickThrough;main.Interaction.SampleForTest(new(false,false));});
                check(dragged&&main.Left==oldLeft+24&&heldDuringDrag&&main.Interaction.EffectiveClickThrough,"Alt-started movement can finish after Alt release and then restores click-through");
                main.Interaction.SampleForTest(new(true,true));check(!main.Interaction.RunDrag(()=>throw new InvalidOperationException("fixture cancellation")),"A cancelled native drag is safely handled");main.Interaction.SampleForTest(new(false,false));
                check(main.Interaction.EffectiveClickThrough,"Cancelled drags do not leave an interaction latch behind");
                c.ToggleClickThrough();check(!c.Preferences.ClickThrough&&!c.Preferences.PositionLocked&&!c.Preferences.EventOverlayLocked&&inputs.All(x=>!x.EffectiveClickThrough),"Click-through recovery clears all locks and restores both overlay HWNDs");
                c.ToggleClickThrough();check(c.Preferences.ClickThrough&&inputs.All(x=>x.EffectiveClickThrough)&&handles.All(OverlayInput.IsClickThrough),"Explicit global click-through applies immediately to the timer overlay as well as meter");
                foreach(var input in inputs)input.SampleForTest(new(true,false));check(inputs.All(x=>!x.EffectiveClickThrough)&&c.Preferences.ClickThrough,"Alt also temporarily overrides global click-through without changing its preference");
                foreach(var input in inputs)input.SampleForTest(new(false,false));
                c.Preferences.ClickThrough=false;c.Preferences.PositionLocked=true;c.Preferences.EventOverlayLocked=true;c.ApplyPreferences();
                c.Restore();check(!c.Preferences.ClickThrough&&!c.Preferences.PositionLocked&&!c.Preferences.EventOverlayLocked&&inputs.All(x=>!x.EffectiveClickThrough),"Show-overlay recovery restores clicks even when saved locks were active");main.Hide();
                foreach(var window in windows)
                {
                    var grip=Children(window).OfType<Border>().Single(x=>x.Name=="MoveHandle");
                    check(grip.Width>=28&&grip.Height>=26&&grip.Cursor==Cursors.SizeAll&&grip.Child is Controls.Symbol{Kind:"Grip"},window.Title+" has a visible full-size move grip");
                    check(Children(window).OfType<Panel>().Any(x=>(x is Grid||x is DockPanel)&&(x.Height==(window is EventOverlayWindow?30:36)||x.Height==38)&&x.Background is SolidColorBrush{Color.A:0}),window.Title+" exposes the full transparent title-bar drag surface");
                }
                var button=new Button{Content=new TextBlock{Text="fixture control"}};check(OverlayInteraction.IsInteractive(button)&&OverlayInteraction.IsInteractive((DependencyObject)button.Content)&&OverlayInteraction.IsInteractive(new Slider()),"Buttons and their content remain controls during Alt interaction");
                check(OverlayInteraction.IsInteractive(new Border{DataContext=new PlayerRow()})&&!OverlayInteraction.IsInteractive(new Border()),"Player detail rows retain their clicks while plain background can be dragged");
                c.Showcase();c.Preferences.FocusMode=true;c.Preferences.PositionLocked=true;c.ApplyPreferences();
                main.Interaction.SampleForTest(new(true,false));check(((FrameworkElement)main.FindName("Header")).Visibility==Visibility.Visible,"Alt interaction exposes the meter header and grip even in combat focus mode");
                check(!OverlayInteraction.SamplingActive&&!c.NativePositionLookupAllowed&&!c.CaptureRunning,"Preview checks never poll real modifiers, capture traffic or read the game process");
            }
            finally{timers.CloseForExit();main.Close();}
        }
        check(OverlayInteraction.RegisteredCount==before,"Closed overlays release their shared input registrations");
        // Native style/sampler checks use invisible off-screen fixture windows, with no injected input.
        var a=new Window{Width=100,Height=60,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent};
        var b=new Window{Width=100,Height=60,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent};
        bool protect=true;using(var ai=new OverlayInteraction(a,()=>protect,()=>protect,()=>{},true))using(var bi=new OverlayInteraction(b,()=>protect,()=>protect,()=>{},true))
        {
            try{a.Show();b.Show();check(OverlayInteraction.SamplingActive,"One shared modifier sampler runs when protected overlays are visible");a.Hide();check(OverlayInteraction.SamplingActive,"Hiding one overlay does not stop recovery for another visible overlay");b.Hide();check(!OverlayInteraction.SamplingActive,"The sampler stops when every protected overlay is hidden");a.Show();protect=false;ai.Refresh();bi.Refresh();check(!OverlayInteraction.SamplingActive,"The sampler stops immediately when click protection is no longer needed");}
            finally{a.Close();b.Close();}
        }
        check(OverlayInteraction.RegisteredCount==before&&!OverlayInteraction.SamplingActive,"Native fixture teardown releases registrations and leaves no polling timer");
    }
}
