using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AscendAPP.Core.Settings;
namespace AscendAPP.Desktop.Services;
public sealed class DesktopIntegration : IDisposable
{
    private readonly OverlayWindow _window;private readonly MeterController _c;private readonly IntPtr _hwnd;private readonly HwndSource? _source;
    private readonly Dictionary<int,Action> _actions=new();private System.Windows.Forms.NotifyIcon? _tray;private string _keys="";
    private System.Drawing.Icon? _trayIcon;private string _logoSignature="";
    private DateTimeOffset _probe;private bool _wasGame,_autoHidden;
    public bool HasTray=>_tray is not null;
    public DesktopIntegration(OverlayWindow window,MeterController controller)
    {
        _window=window;_c=controller;_hwnd=new WindowInteropHelper(window).Handle;_source=HwndSource.FromHwnd(_hwnd);_source?.AddHook(Message);
        try{
            _tray=new System.Windows.Forms.NotifyIcon{Icon=_trayIcon=AppearanceTheme.CreateTrayIcon(),Text="FooMeter · Compact",Visible=true};
            var menu=new System.Windows.Forms.ContextMenuStrip{BackColor=System.Drawing.Color.FromArgb(28,36,44),ForeColor=System.Drawing.Color.FromArgb(230,237,244)};
            menu.Items.Add("Show overlay",null,(_,_)=>_window.Dispatcher.Invoke(_c.Restore));
            menu.Items.Add("Show / hide event timers",null,(_,_)=>_window.Dispatcher.Invoke(_c.ToggleEventOverlay));
            menu.Items.Add("Show / hide pet souls",null,(_,_)=>_window.Dispatcher.Invoke(_c.TogglePetOverlay));
            menu.Items.Add("Settings",null,(_,_)=>_window.Dispatcher.Invoke(()=>_c.ShowSettings()));
            menu.Items.Add("Disable click-through",null,(_,_)=>_window.Dispatcher.Invoke(_c.Restore));
            menu.Items.Add("Exit",null,(_,_)=>_window.Dispatcher.Invoke(_c.Quit));
            _tray.ContextMenuStrip=menu;_tray.DoubleClick+=(_,_)=>_window.Dispatcher.Invoke(_c.Restore);
        }catch{_tray?.Dispose();_tray=null;_c.Notice("Tray unavailable. Click-through stays off.");}
        if(!_c.Preferences.RememberClickThrough)_c.Preferences.ClickThrough=false;
    }
    public void SuspendKeys(){foreach(int id in _actions.Keys)UnregisterHotKey(_hwnd,id);_actions.Clear();_keys="";}
    public void Apply()
    {
        var p=_c.Preferences;
        if(_tray is not null&&_logoSignature!=AppearanceTheme.LogoSignature){var icon=AppearanceTheme.CreateTrayIcon();_tray.Icon=icon;_trayIcon?.Dispose();_trayIcon=icon;_logoSignature=AppearanceTheme.LogoSignature;}
        if(!HasTray)p.ClickThrough=false;
        _window.RefreshInput();
        string signature=$"{p.HideHotkey}|{p.ResetHotkey}|{p.ClearHotkey}|{p.CopyHotkey}|{p.ClickThroughHotkey}|{p.EventsHotkey}|{p.StripHotkey}";
        if(signature==_keys)return;_keys=signature;
        foreach(int id in _actions.Keys)UnregisterHotKey(_hwnd,id);_actions.Clear();
        var bindings=new (string,Action)[]{(p.HideHotkey,_c.ToggleOverlay),(p.ResetHotkey,_c.ResetEncounter),(p.ClearHotkey,_c.ClearMeter),(p.CopyHotkey,_c.CopySummary),(p.ClickThroughHotkey,_c.ToggleClickThrough),(p.EventsHotkey,_c.ToggleEventOverlay),(p.StripHotkey,_c.ToggleCombatStrip)};
        var seen=new HashSet<HotkeyChord>();var failures=new List<string>();int next=110;
        foreach(var (text,action)in bindings)
        {
            if(!HotkeyChord.TryParse(text,out var chord)){failures.Add("Invalid hotkey: "+text);continue;}
            if(chord is null)continue;
            int id=next++;
            if(!seen.Add(chord)||!RegisterHotKey(_hwnd,id,chord.Modifiers|0x4000,chord.VirtualKey)){failures.Add("Hotkey unavailable: "+text);continue;}
            _actions[id]=action;
        }
        _c.IntegrationStatus=string.Join("; ",failures);if(failures.Count>0)_c.Notice(failures[0]);
    }
    private IntPtr Message(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {if(msg==0x312&&_actions.TryGetValue(w.ToInt32(),out var action)){action();handled=true;}return IntPtr.Zero;}
    public void Notify(string title,string text){try{_tray?.ShowBalloonTip(4000,title,text,System.Windows.Forms.ToolTipIcon.Info);}catch{}}
    public void Tick()
    {
        if(DateTimeOffset.UtcNow-_probe<TimeSpan.FromSeconds(1))return;_probe=DateTimeOffset.UtcNow;
        var p=_c.Preferences;if(!p.OnlyAboveGame&&!p.TrayWhenGameCloses){_window.Topmost=true;return;}
        bool running=false,focused=false;GetWindowThreadProcessId(GetForegroundWindow(),out uint foreground);
        try{foreach(var proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(p.GameProcess))){using(proc){running=true;if(proc.Id==foreground)focused=true;}}}catch{}
        _window.Topmost=!p.OnlyAboveGame||focused||foreground==Environment.ProcessId;
        if(p.TrayWhenGameCloses&&HasTray){if(_wasGame&&!running){_window.Hide();_c.AutoHideEventOverlay(true);_c.AutoHidePetOverlay(true);_autoHidden=true;}else if(!_wasGame&&running&&_autoHidden){_c.ResumeAfterGame();_c.AutoHideEventOverlay(false);_c.AutoHidePetOverlay(false);_autoHidden=false;}}
        _wasGame=running;
    }
    public void Dispose(){foreach(int id in _actions.Keys)UnregisterHotKey(_hwnd,id);_actions.Clear();_source?.RemoveHook(Message);if(_tray is {} tray){tray.Visible=false;tray.Dispose();_tray=null;_trayIcon?.Dispose();_trayIcon=null;}}
    [DllImport("user32.dll",SetLastError=true)]private static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
}
