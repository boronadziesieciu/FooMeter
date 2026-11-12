using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using AscendAPP.Desktop.Controls;
using AscendAPP.Desktop.ViewModels;
namespace AscendAPP.Desktop.Services;

/// <summary>One policy for all layered overlays; only one small modifier sampler while protection is visible.</summary>
internal sealed class OverlayInteraction : IDisposable
{
    private static readonly HashSet<OverlayInteraction> Instances=new();
    private static DispatcherTimer? _timer;
    private readonly Window _window;private readonly Func<bool> _protected,_locked;private readonly Action _touch;private readonly bool _nativeKeys;
    private OverlayKeys _keys;private bool _heldClick,_dragging,_disposed,_temporary;
    internal bool EffectiveClickThrough {get;private set;}
    internal bool TemporaryInteractive=>_temporary;
    internal bool CanDrag=>!_locked()||_temporary;
    internal static bool SamplingActive=>_timer?.IsEnabled==true;
    internal static int RegisteredCount=>Instances.Count;
    internal event Action? Changed;
    internal OverlayInteraction(Window window,Func<bool> protection,Func<bool> locked,Action touch,bool nativeKeys)
    {
        _window=window;_protected=protection;_locked=locked;_touch=touch;_nativeKeys=nativeKeys;
        window.SourceInitialized+=SourceReady;window.IsVisibleChanged+=VisibilityChanged;window.Closed+=Closed;
        window.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler(PointerDown),true);
        window.AddHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(PointerUp),true);
        Instances.Add(this);Refresh();
    }
    private void SourceReady(object? s,EventArgs e)=>Refresh();
    private void VisibilityChanged(object s,DependencyPropertyChangedEventArgs e){if(!_window.IsVisible){_heldClick=false;_dragging=false;_keys=default;}Refresh();}
    private void Closed(object? s,EventArgs e)=>Dispose();
    private static void SyncSampler()
    {
        bool needed=Instances.Any(x=>x._nativeKeys&&x._window.IsVisible&&x._protected());
        if(needed){if(_timer is null){_timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(25)};_timer.Tick+=Poll;}if(!_timer.IsEnabled)_timer.Start();}
        else _timer?.Stop();
    }
    private static void Poll(object? sender,EventArgs e)
    {
        var keys=OverlayInput.ReadKeys();
        foreach(var item in Instances.ToArray())if(item._nativeKeys&&item._window.IsVisible&&item._protected())item.Apply(keys);
        SyncSampler();
    }
    internal void Refresh()
    {
        if(_disposed)return;
        var keys=_nativeKeys&&_window.IsVisible&&_protected()?OverlayInput.ReadKeys():_keys;
        Apply(keys);SyncSampler();
    }
    private void Apply(OverlayKeys keys)
    {
        _keys=keys;if(!keys.ButtonsDown)_heldClick=false;
        if(!_protected()){_heldClick=false;_dragging=false;}
        bool temporary=_protected()&&(keys.InteractionAlt||_heldClick||_dragging);
        bool changed=temporary!=_temporary;_temporary=temporary;EffectiveClickThrough=_protected()&&!temporary;
        var hwnd=new WindowInteropHelper(_window).Handle;if(hwnd!=IntPtr.Zero)OverlayInput.SetClickThrough(hwnd,EffectiveClickThrough);
        if(changed){if(temporary)_touch();Changed?.Invoke();}
    }
    private void PointerDown(object sender,MouseButtonEventArgs e)
    {
        if(_nativeKeys)Apply(OverlayInput.ReadKeys());
        if(_protected()&&_keys.InteractionAlt){_heldClick=true;Apply(_keys);_touch();_window.Activate();}
        // Alt-drag can also use plain background. Actual controls, player details keep their normal clicks.
        if(_keys.InteractionAlt&&e.ChangedButton==MouseButton.Left&&!IsInteractive(e.OriginalSource as DependencyObject))TryDrag(e);
    }
    private void PointerUp(object sender,MouseButtonEventArgs e)
    {
        if(_nativeKeys)Apply(OverlayInput.ReadKeys());
        else Apply(_keys with{ButtonsDown=false});
    }
    internal void TryDrag(MouseButtonEventArgs e)
    {
        if(e.Handled||e.LeftButton!=MouseButtonState.Pressed||!CanDrag||EffectiveClickThrough||IsInteractive(e.OriginalSource as DependencyObject))return;
        e.Handled=true;RunDrag(_window.DragMove);
    }
    internal bool RunDrag(Action move)
    {
        if(!CanDrag||EffectiveClickThrough)return false;
        _touch();_dragging=true;Apply(_keys);
        try{move();return true;}catch(InvalidOperationException){return false;}
        finally{_dragging=false;_heldClick=false;if(_nativeKeys)_keys=OverlayInput.ReadKeys();Apply(_keys);}
    }
    internal static bool IsInteractive(DependencyObject? node)
    {
        while(node is not null)
        {
            if(node is ButtonBase or TextBoxBase or PasswordBox or Selector or Slider or Thumb or ScrollBar)return true;
            if(node is FrameworkElement{DataContext:PlayerRow})return true;
            node=node is Visual or Visual3D?VisualTreeHelper.GetParent(node)??LogicalTreeHelper.GetParent(node):node is FrameworkContentElement content?content.Parent:LogicalTreeHelper.GetParent(node);
        }
        return false;
    }
    // Fixtures drive only this overlay's policy. They never change the real keyboard or send input to another app.
    internal void SampleForTest(OverlayKeys keys){if(_nativeKeys)throw new InvalidOperationException("Fixture input is preview-only.");Apply(keys);}
    internal void HoldClickForTest(){if(_nativeKeys)throw new InvalidOperationException("Fixture input is preview-only.");if(_keys.InteractionAlt)_heldClick=true;Apply(_keys);}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Instances.Remove(this);SyncSampler();Changed=null;
        _window.SourceInitialized-=SourceReady;_window.IsVisibleChanged-=VisibilityChanged;_window.Closed-=Closed;
        _window.RemoveHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler(PointerDown));
        _window.RemoveHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(PointerUp));
    }
}
