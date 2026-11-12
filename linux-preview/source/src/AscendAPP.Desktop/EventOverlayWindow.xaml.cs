using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AscendAPP.Desktop.ViewModels;
namespace AscendAPP.Desktop;
public partial class EventOverlayWindow : Window
{
    internal Services.OverlayInteraction Interaction {get;}
    internal void RefreshInput()=>Interaction.Refresh();
    private readonly MeterController _c;private bool _positionReady,_allowClose;
    public EventOverlayWindow(MeterController c)
    {
        InitializeComponent();_c=c;DataContext=c.Model;
        Interaction=new(this,()=>c.Preferences.EventOverlayLocked||c.Preferences.ClickThrough&&c.CanClickThrough,()=>c.Preferences.EventOverlayLocked,c.Touch,!c.Preview);Interaction.Changed+=ApplyPreferences;
        Loaded+=(_,_)=>{_positionReady=true;ApplyPreferences();};
        LocationChanged+=(_,_)=>{if(_positionReady){c.Preferences.EventOverlayLeft=Left;c.Preferences.EventOverlayTop=Top;c.QueueSave();}};
        Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;c.SetEventOverlayVisible(false);}};
        ApplyPreferences();
    }
    public void ApplyPreferences()
    {
        var p=_c.Preferences;double uiScale=p.EventOverlayScale??p.UiScale;Width=300*uiScale;if(LayoutRoot.LayoutTransform is not ScaleTransform scale||scale.ScaleX!=uiScale)LayoutRoot.LayoutTransform=new ScaleTransform(uiScale,uiScale);Opacity=p.EventOverlayOpacity??p.OverlayOpacity;
        Topmost=_c.Overlay?.Topmost??true;
        LockIcon.Kind=p.EventOverlayLocked?"Lock":"Unlock";LockButton.ToolTip=p.EventOverlayLocked?"Locked · hold Alt to interact or drag":"Lock timers and pass clicks through";
        EventHeading.Visibility=_c.Model.ScheduledEvents.Count>0?Visibility.Visible:Visibility.Collapsed;
        BossHeading.Visibility=_c.Model.FieldBossTimers.Count>0?Visibility.Visible:Visibility.Collapsed;
        EmptyMessage.Visibility=_c.Model.ScheduledEvents.Count+_c.Model.FieldBossTimers.Count==0?Visibility.Visible:Visibility.Collapsed;
        TimerScroller.MaxHeight=Math.Max(100,SystemParameters.WorkArea.Height/uiScale-100);
        RefreshInput();if(!_positionReady)return;
        var a=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);var primary=SystemParameters.WorkArea;
        double meterLeft=_c.Overlay?.Left??double.NaN,meterTop=_c.Overlay?.Top??double.NaN;
        if(!double.IsFinite(meterLeft))meterLeft=primary.Right-420*p.UiScale-24;
        if(!double.IsFinite(meterTop))meterTop=primary.Top+80;
        double beside=meterLeft-Width-12;if(beside<a.Left)beside=meterLeft+(_c.Overlay?.Width??420*p.UiScale)+12;
        double x=p.EventOverlayLeft??beside,y=p.EventOverlayTop??meterTop;
        Left=Math.Clamp(x,a.Left,Math.Max(a.Left,a.Right-Width));Top=Math.Clamp(y,a.Top,Math.Max(a.Top,a.Bottom-Math.Max(80,ActualHeight)));
    }
    private void Drag_Click(object sender,MouseButtonEventArgs e)=>Interaction.TryDrag(e);
    private void Settings_Click(object sender,RoutedEventArgs e)=>_c.ShowSettings("Event tracking");
    private void Hide_Click(object sender,RoutedEventArgs e)=>_c.SetEventOverlayVisible(false);
    private void Lock_Click(object sender,RoutedEventArgs e){_c.Preferences.EventOverlayLocked=!_c.Preferences.EventOverlayLocked;_c.ApplyPreferences();}
    internal void CloseForExit(){if(_allowClose)return;_allowClose=true;Close();}
}
