using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using AscendAPP.Core.Settings;
using AscendAPP.Desktop.Controls;
using AscendAPP.Desktop.Services;
namespace AscendAPP.Desktop;
public partial class SettingsWindow : Window
{
    private readonly MeterController _c;private MeterPreferences P=>_c.Preferences;private string _selected="General";private TextBlock? _debug;
    private FrameworkElement? _logoEditor;
    private readonly Dictionary<string,Button> _nav=new();
    private static Brush Muted=>Brush("#A59AA7");private static Brush Accent=>Brush("#F28DB8");
    private static Brush Brush(string color)=>AppearanceTheme.Brush(color);
    public SettingsWindow(MeterController controller)
    {
        InitializeComponent();_c=controller;DataContext=controller.Model;
        foreach(var (name,icon)in new[]{("General","Sliders"),("Combat","Combat"),("Display","Display"),("Appearance","Flame"),("Overlay behaviour","Overlay"),("Audio","Audio"),("Event tracking","Bell"),("Pet tracking","Pet"),("Keybinds","Keyboard"),("Character","Account"),("Account","Account"),("Integrations","Plug"),("Changelog","Notes"),("Debug","Debug")})
        {
            var panel=new StackPanel{Orientation=Orientation.Horizontal};panel.Children.Add(new Symbol{Kind=icon,Width=14,Height=14,Margin=new Thickness(0,0,9,0)});panel.Children.Add(new TextBlock{Text=name,FontSize=11,VerticalAlignment=VerticalAlignment.Center});
            var b=new Button{Content=panel,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(9,7,5,7),Margin=new Thickness(0,0,0,3),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent};
            b.Click+=(_,_)=>SelectPage(name);_nav[name]=b;Navigation.Children.Add(b);
        }
        _c.Refreshed+=RefreshDebug;Closed+=(_,_)=>_c.Refreshed-=RefreshDebug;SelectPage("General");
    }
    public void SelectPage(string name)
    {
        _selected=name;Page.Children.Clear();_debug=null;PageScroll.ScrollToTop();
        foreach(var (title,b)in _nav){b.Background=title==name?Brush("#33212D"):Brushes.Transparent;b.BorderBrush=title==name?Brush("#9F5A82"):Brushes.Transparent;b.Foreground=title==name?Accent:Muted;}
        switch(name){case "General":General();break;case "Combat":Combat();break;case "Display":Display();break;case "Appearance":Appearance();break;case "Overlay behaviour":Behaviour();break;case "Audio":Audio();break;case "Event tracking":Events();break;case "Pet tracking":Pets();break;case "Keybinds":Keybinds();break;case "Account":Account();break;case "Integrations":Integrations();break;case "Changelog":Changelog();break;case "Character":Character();break;case "Tickets":Tickets();break;default:Debug();break;}
    }
    private void Change(){_c.ApplyPreferences();}
    private void Section(string text){Page.Children.Add(new TextBlock{Text=text.ToUpperInvariant(),FontSize=9,FontWeight=FontWeights.SemiBold,Foreground=Brush("#958498"),Margin=new Thickness(0,13,0,9)});}
    private void Help(string text){Page.Children.Add(new TextBlock{Text=text,FontSize=10,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12),LineHeight=16});}
    private void Divider()=>Page.Children.Add(new Border{Height=1,Background=Brush("#2D252F"),Margin=new Thickness(0,10,0,3)});
    private void Row(string title,string? detail,FrameworkElement control)
    {
        var g=new Grid{Margin=new Thickness(0,0,0,13)};g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var label=new StackPanel{Margin=new Thickness(0,2,20,0)};label.Children.Add(new TextBlock{Text=title,FontSize=11,FontWeight=FontWeights.Medium});if(!string.IsNullOrWhiteSpace(detail))label.Children.Add(new TextBlock{Text=detail,FontSize=10,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0),LineHeight=15});
        System.Windows.Automation.AutomationProperties.SetName(control,title);
        g.Children.Add(label);Grid.SetColumn(control,1);control.VerticalAlignment=VerticalAlignment.Top;g.Children.Add(control);Page.Children.Add(g);
    }
    private CheckBox Toggle(bool value,Action<bool> set,bool enabled=true){var b=new CheckBox{IsChecked=value,IsEnabled=enabled,Margin=new Thickness(0,2,0,0)};b.Checked+=(_,_)=>{set(true);Change();};b.Unchecked+=(_,_)=>{set(false);Change();};return b;}
    private ComboBox Choice<T>(IEnumerable<T> choices,T value,Action<T> set,double width=160)
    {var b=new ComboBox{ItemsSource=choices,SelectedItem=value,Width=width};b.SelectionChanged+=(_,_)=>{if(b.SelectedItem is T v){set(v);Change();}};return b;}
    private TextBox Field(string value,Action<string> set,double width=180)
    {var b=new TextBox{Text=value,Width=width};void Commit(){set(b.Text.Trim());Change();}b.LostKeyboardFocus+=(_,_)=>Commit();b.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Commit();Keyboard.ClearFocus();}};return b;}
    private TextBox Number(int value,int min,int max,Action<int> set)
    {var b=new TextBox{Text=value.ToString(),Width=70,TextAlignment=TextAlignment.Center};void Commit(){if(int.TryParse(b.Text,out int n)&&n>=min&&n<=max){set(n);b.BorderBrush=Brush("#423646");Change();}else{b.BorderBrush=Brush("#C98B86");_c.Notice($"Enter a value from {min} to {max}.");}}b.LostKeyboardFocus+=(_,_)=>Commit();b.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Commit();Keyboard.ClearFocus();}};return b;}
    private void Slider(string title,string detail,double value,double min,double max,Action<double> set,Func<double,string> format)
    {
        Row(title,detail,new TextBlock());
        var g=new Grid{Margin=new Thickness(0,-6,0,16)};g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(70)});
        var label=new TextBlock{Text=format(value),Foreground=Accent,FontSize=10,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Right};Grid.SetColumn(label,1);
        var slider=new Slider{Value=value,Minimum=min,Maximum=max,SmallChange=(max-min)/100,VerticalAlignment=VerticalAlignment.Center,Foreground=Accent,Margin=new Thickness(0,0,15,0)};
        System.Windows.Automation.AutomationProperties.SetName(slider,title);
        slider.ValueChanged+=(_,_)=>{set(slider.Value);label.Text=format(slider.Value);Change();};g.Children.Add(slider);g.Children.Add(label);Page.Children.Add(g);
    }
    private Button ActionButton(string text,string icon,Action action)
    {var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(new Symbol{Kind=icon,Width=13,Height=13,Margin=new Thickness(0,0,7,0)});content.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center});var b=new Button{Content=content,Margin=new Thickness(0,0,7,7)};System.Windows.Automation.AutomationProperties.SetName(b,text);b.Click+=(_,_)=>action();return b;}
    private void Actions(params Button[] buttons){var p=new WrapPanel{Margin=new Thickness(0,3,0,8)};foreach(var b in buttons)p.Children.Add(b);Page.Children.Add(p);}
    private void Unavailable(string title,string detail)=>Row(title,detail,new TextBlock{Text="Unavailable",FontSize=10,Foreground=Brush("#958498"),Margin=new Thickness(0,3,0,0)});
    private void General()
    {
        Section("Region and language");Row("Report region","Labels exported reports. It does not infer a regional game schedule.",Choice(new[]{"Europe","North America","Asia"},P.Region,x=>P.Region=x));Row("Language","FooMeter uses English throughout.",new TextBlock{Text="English",Foreground=Accent,Margin=new Thickness(0,4,0,0)});
        Divider();Section("Capture");Row("Start capture with the app","The compact overlay opens first and waits for your game traffic.",Toggle(P.AutoCapture,x=>P.AutoCapture=x));Row("VPN / adapter compatibility","Capture on all adapters, including Npcap loopback. Restart capture after changing.",Toggle(P.AutoAdapters,x=>{P.AutoAdapters=x;_c.Model.AutoAdapters=x;}));
        Row("Network adapter","Used when compatibility capture is off.",Choice(_c.Model.Devices,_c.Model.SelectedDevice!,x=>{_c.Model.SelectedDevice=x;P.Adapter=x.Device.Name;},250));
        Row("TCP port","Blank detects the game automatically. An optional port restricts capture.",Field(P.Port,x=>{P.Port=x;_c.Model.Port=x;},90));
        Actions(ActionButton("Start capture","Play",_c.StartCapture),ActionButton("Stop","Stop",_c.Stop),ActionButton("Refresh adapters","Reset",()=>{_c.RefreshAdapters();SelectPage("General");}));
        var status=new TextBlock{FontSize=10,Foreground=Muted,TextWrapping=TextWrapping.Wrap,LineHeight=15,Margin=new Thickness(0,2,0,12)};status.SetBinding(TextBlock.TextProperty,new Binding("Status"));Page.Children.Add(status);
        Divider();Section("Application");Help("FooMeter 0.10.29 · Combat, events and loot-row pet counters. Preferences are shared across versions in your Windows user data folder. Automatic updates are not available.");Actions(ActionButton("Your GS / CP","Account",()=>SelectPage("Character")),ActionButton("Character tickets","Ticket",()=>SelectPage("Tickets")),ActionButton("What changed","Notes",()=>SelectPage("Changelog")));
        Divider();Section("Saved data");Help(_c.UserDataInfo);Help("Older settings are imported automatically on first launch when found. To choose another file, Import older settings replaces your shared preferences and backs up the previous file first.");
        Actions(ActionButton("Open saved data folder","Notes",_c.OpenUserDataFolder),ActionButton("Import older settings","Import",()=>{_c.ImportSettings();SelectPage("General");}));
    }
    private void Combat()
    {
        Section("Encounters");Row("Idle timeout (seconds)","End an ordinary encounter when nobody deals damage for this long.",Number(P.IdleSeconds,5,600,x=>P.IdleSeconds=x));Row("Boss encounter reset detection","Keep a known boss encounter open during idle mechanics; finish on observed defeat, disengage or full-health reset.",Toggle(P.BossResetDetection,x=>P.BossResetDetection=x));Row("Capture players","Self is always included. Party members need the current roster and a matching name/server or unique nickname. Ambiguous identities stay excluded.",Choice(new[]{"Self only","Self and party"},P.CaptureScope,x=>P.CaptureScope=x,190));Help("Targets: bosses and training dummies only. Ordinary monsters, players and unclassified NPCs are excluded. English names and explicit NPC IDs are bundled.");
        Divider();Section("Healing score");Help("Live eHPS is a conservative HP-based estimate. Full-health heals and initial HoT announcements do not score. A partial heal scores only an isolated observed HP increase. Missing or stale HP, overlapping heals and intervening damage remain unverified and unscored. Expect up to 500 ms reconciliation plus the display refresh interval. Hover a player row or export diagnostics to see raw, excluded and unverified amounts.");
        Divider();Section("Training dummy");Row("Additional dummy NPC IDs","The 51 built-in dummy IDs include Training Scarecrow. Add comma-separated IDs only for missing dummies.",Field(P.DummyNpcIds,x=>P.DummyNpcIds=x,220));Row("Dummy parse length","0 is off. A timed parse stays complete until you reset it.",Number(P.DummySeconds,0,3600,x=>P.DummySeconds=x));Help("Changing capture filters starts a new encounter. Damage to adds and healing do not inflate a known boss's defeat estimate.");
    }
    private void Display()
    {
        Section("Layout");Row("One-line combat strip","Show your rate, party rate, target health and fight clock on one row. Ctrl+Shift+F6 toggles it.",Toggle(P.CombatStrip,x=>P.CombatStrip=x));
        Section("Numbers");Row("Smooth number transitions","A short fade on changed player rates and totals. Stops when values stop changing; follows Windows animation preferences.",Toggle(P.SmoothNumbers,x=>P.SmoothNumbers=x));Row("DPS calculation","Encounter divides by the whole fight. Active divides each player's damage by the span from their first to last damage record (minimum 1 second).",Choice(new[]{"Encounter","Active"},P.DpsCalculation,x=>P.DpsCalculation=x));Row("Hide total damage","Keep name, GS, CP and rate in each slim player bar.",Toggle(P.HideTotalDamage,x=>P.HideTotalDamage=x));Row("Show party DPS","Show the measured damage rate and membership scope below the bars.",Toggle(P.ShowPartyDps,x=>P.ShowPartyDps=x));Row("Show boss forecasts","Show estimated defeat, berserk countdown and time margin when data is available.",Toggle(P.ShowBerserk,x=>P.ShowBerserk=x));Row("Copy summary format","Copies actual character names, including when names are hidden on screen.",Choice(new[]{"Single line","Multi-line"},P.CopyFormat,x=>P.CopyFormat=x));Row("Time format","Used for local event times and ticket history.",Choice(new[]{"24-hour","12-hour"},P.Clock24?"24-hour":"12-hour",x=>P.Clock24=x=="24-hour"));
        Divider();Section("Overlay");Slider("Overlay opacity","",P.OverlayOpacity,0.2,1,x=>P.OverlayOpacity=x,x=>$"{x:P0}");Row("Hide other player names","Their class is shown instead. Copy and exports keep original names.",Toggle(P.HideNames,x=>P.HideNames=x));Slider("UI scale","The compact window resizes to keep text readable.",P.UiScale,0.8,1.5,x=>P.UiScale=x,x=>$"{x:P0}");Row("Visible player rows","Additional players scroll inside the same compact meter.",Number(P.VisiblePlayers,1,12,x=>P.VisiblePlayers=x));Slider("Update rate","Combat refresh rate. Idle refreshes use at least one second; the first admitted hit wakes the meter immediately.",P.UpdateMs,100,2000,x=>P.UpdateMs=(int)x,x=>$"{x:0} ms");
        Divider();Section("Requires additional game data");Help("GS and CP use verified own/party character records where available. Unknown scores remain —.");Unavailable("Death counter","Complete death records and causes are not decoded yet.");Unavailable("Leaderboard ranks and animated nameplates","These need a leaderboard and account service. They are not inferred from DPS.");
    }
    private void Appearance()
    {
        Section("Your colors");
        Help("Changes apply immediately to the meter, events, pet tracker, pickup counter and settings. Text contrast adapts to your background. Class colors and capture-status meanings stay distinct.");
        var presets=new WrapPanel{Margin=new Thickness(0,0,0,14)};
        foreach(var (name,color) in new[]{("Rose","#F28DB8"),("Mint","#83D5C4"),("Azure","#81B9FF"),("Violet","#BBA0FF"),("Gold","#EBC574"),("Crimson","#EE798B")})
        {
            var b=new Button{Content=name,Margin=new Thickness(0,0,6,6),Padding=new Thickness(10,6,10,6)};
            b.Click+=(_,_)=>{P.AccentColor=color;Change();SelectPage("Appearance");};presets.Children.Add(b);
        }
        Page.Children.Add(presets);
        ColorRow("App accent",P.AccentColor,x=>P.AccentColor=x);
        ColorRow("Panel background",P.PanelColor,x=>P.PanelColor=x);
        Row("Logo follows app accent","Turn off to choose a separate flame color.",Toggle(P.LogoFollowsAccent,x=>{P.LogoFollowsAccent=x;if(_logoEditor is not null)_logoEditor.IsEnabled=!x;}));
        _logoEditor=ColorRow("Flame logo",P.LogoColor,x=>P.LogoColor=x);_logoEditor.IsEnabled=!P.LogoFollowsAccent;
        var preview=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};preview.Children.Add(new Symbol{Kind="Brand",Width=30,Height=34,Margin=new Thickness(0,0,10,0)});preview.Children.Add(new TextBlock{Text="FooMeter",FontSize=17,VerticalAlignment=VerticalAlignment.Center});Page.Children.Add(preview);
        Actions(ActionButton("Restore default colors","Reset",()=>{P.AccentColor=AppearanceColors.DefaultAccent;P.PanelColor=AppearanceColors.DefaultPanel;P.LogoColor=AppearanceColors.DefaultLogo;P.LogoFollowsAccent=true;Change();SelectPage("Appearance");}));
        Help("Use the color swatch for a picker, or enter an RGB hex code such as #81B9FF. Changes are saved with your existing preferences. The running app and tray flame update; the executable's fixed Explorer icon stays pink.");
    }
    private FrameworkElement ColorRow(string title,string value,Action<string> set)
    {
        var panel=new StackPanel{Orientation=Orientation.Horizontal};
        var swatch=new Button{Width=32,Height=29,Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)),Margin=new Thickness(0,0,7,0),Padding=new Thickness(0),ToolTip="Choose "+title.ToLowerInvariant()};
        var field=new TextBox{Text=value,Width=100,MaxLength=9};System.Windows.Automation.AutomationProperties.SetName(field,title+" hex");
        void Commit(){if(!AppearanceColors.TryNormalize(field.Text,out var hex)){field.BorderBrush=Brush("#C98B86");_c.Notice("Use an RGB hex color, for example #81B9FF.");return;}set(hex);field.Text=hex;swatch.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));field.BorderBrush=Brush("#423646");Change();}
        field.LostKeyboardFocus+=(_,_)=>Commit();field.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Commit();Keyboard.ClearFocus();}};
        swatch.Click+=(_,_)=>{using var dialog=new System.Windows.Forms.ColorDialog{FullOpen=true,AnyColor=true,Color=System.Drawing.ColorTranslator.FromHtml(AppearanceColors.Normalize(field.Text,value))};if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){set("#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2"));Change();SelectPage("Appearance");}};
        panel.Children.Add(swatch);panel.Children.Add(field);Row(title,null,panel);return panel;
    }
    private void Behaviour()
    {
        Section("Overlay behaviour");Row("Focus mode","Hide the top controls during combat. Hover over the meter to reveal them.",Toggle(P.FocusMode,x=>P.FocusMode=x));Row("Fade when idle","Fade after no active encounter or interaction.",Toggle(P.FadeIdle,x=>P.FadeIdle=x));Slider("Faded opacity","",P.FadedOpacity,0.1,1,x=>P.FadedOpacity=x,x=>$"{x:P0}");Row("Fade after (seconds)","From the last combat event or interaction.",Number(P.FadeAfterSeconds,10,600,x=>P.FadeAfterSeconds=x));Row("Only above the game","Stay topmost while your configured game process is focused.",Toggle(P.OnlyAboveGame,x=>P.OnlyAboveGame=x));Row("Game process","Process name without .exe; used for focus and game-close behaviour.",Field(P.GameProcess,x=>P.GameProcess=x));Row("Click-through","Pass clicks through all overlays. Hold Alt to use controls or drag a background; release Alt to resume click-through.",Toggle(P.ClickThrough,x=>P.ClickThrough=x,_c.CanClickThrough));Row("Remember click-through","Restore that state on the next launch. The tray remains a recovery route.",Toggle(P.RememberClickThrough,x=>P.RememberClickThrough=x));Row("Lock position","Pass clicks through this overlay and prevent accidental moves. Hold Alt to interact or drag the grip.",Toggle(P.PositionLocked,x=>P.PositionLocked=x));Row("Minimize to tray","The minimize button hides the meter in the Windows tray.",Toggle(P.MinimizeToTray,x=>P.MinimizeToTray=x));Row("Tray when the game closes","Hide after the observed game process exits; restore when it opens again.",Toggle(P.TrayWhenGameCloses,x=>P.TrayWhenGameCloses=x));Help("Each overlay has a six-dot grip and a draggable title bar. Hold Alt while locked to interact or move it; Ctrl/Alt combinations such as AltGr do not enable this override. Ctrl+Shift+F10 or the tray’s Show overlay unlocks all overlays and restores clicks. Ctrl+Shift+F12 also recovers clicks.");
    }
    private void Audio()
    {
        Section("Volume");Slider("Event reminders","Sounds for your custom event reminders.",P.EventVolume,0,1,x=>P.EventVolume=x,x=>$"{x:P0}");Actions(ActionButton("Preview event sound","Audio",()=>AlertSound.Play(P.EventVolume)));Help("Volume 0 mutes event reminders. Alerts use a short generated chime; no external sound pack is required.");
    }
    private void Events()
    {
        Section("Game timers");Help("A separate compact window for scheduled events and the four Verteron field bosses. Use the bell in the meter or Ctrl+Shift+F7 to show or hide it.");
        Slider("Event overlay scale","Size only the event window. Existing profiles initially follow the meter scale.",P.EventOverlayScale??P.UiScale,.8,1.5,x=>P.EventOverlayScale=x,x=>$"{x:P0}");
        Slider("Event overlay opacity","Transparency only for event timers.",P.EventOverlayOpacity??P.OverlayOpacity,.2,1,x=>P.EventOverlayOpacity=x,x=>$"{x:P0}");
        Actions(ActionButton("Follow meter appearance","Reset",()=>{P.EventOverlayScale=null;P.EventOverlayOpacity=null;Change();SelectPage("Event tracking");}));
        Row("Show event overlay","Hiding the window does not stop enabled timer alerts.",Toggle(P.ShowEventOverlay,x=>P.ShowEventOverlay=x));
        Row("Shugo Festival","Hourly UTC schedule estimate.",Toggle(P.ShowShugoTimer,x=>P.ShowShugoTimer=x));
        Row("Spacetime Rifts","Every three hours from 00:00 UTC in the pinned timetable. Adjust below if your in-game countdown differs.",Toggle(P.ShowRiftTimer,x=>P.ShowRiftTimer=x));
        Row("Dimensional Invasion","Optional possible windows, not guaranteed event starts.",Toggle(P.ShowInvasionTimer,x=>P.ShowInvasionTimer=x));
        Row("Four field bosses","Eternal Gartua, Silent Dartan, Soul Ruler Kashapa and High Commander Lagta. Detect your character, then open the in-game map → Field bosses in Verteron.",Toggle(P.ShowFieldBossTimers,x=>P.ShowFieldBossTimers=x));
        Row("Rift time adjustment (minutes)","Shift only the rift timetable by -180 to +180 minutes. Schedule rows are estimates, not live confirmations.",Number(P.RiftShiftMinutes,-180,180,x=>P.RiftShiftMinutes=x));
        Row("Game timer alerts","Sound and tray reminders for selected schedules and observed boss deadlines. Off by default.",Toggle(P.GameTimerAlerts,x=>P.GameTimerAlerts=x));
        Row("Alert minutes before","Comma-separated lead times, 0–45. Due-time alert is included.",Field(P.GameTimerLeadMinutes,x=>P.GameTimerLeadMinutes=x));
        Actions(ActionButton("Show / hide timers","Bell",_c.ToggleEventOverlay),ActionButton("Reset timer position","Reset",_c.ResetEventOverlayPosition),ActionButton("Forget boss snapshots","Clear",_c.ForgetFieldBosses));
        Help("Boss countdowns continue through teleports and zone changes using the last confirmed server. Saved S… marks the retained server while character detection refreshes. A newly identified server uses its own cache. Snapshots stay saved separately by server. Seen alive means alive at the last observation. Due means refresh the map to confirm; no respawn cycle is assumed.");
        Divider();Section("Custom reminders");Help("Your own reminders stay available in the main meter. Dates include your local UTC offset.");Row("Show timers","Alerts can still sound when the timer row is hidden.",Choice(new[]{"Always","Outside fights"},P.EventVisibility,x=>P.EventVisibility=x));Actions(ActionButton("New reminder","Plus",()=>{P.Reminders.Add(new());Change();SelectPage("Event tracking");}));
        foreach(var r in P.Reminders.ToArray())
        {
            Divider();Row("Reminder name",null,Field(r.Name,x=>r.Name=x,230));Row("Enabled",null,Toggle(r.Enabled,x=>r.Enabled=x));Row("Starts at","Local time, with an optional UTC offset.",Field(r.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz"),x=>{if(DateTimeOffset.TryParse(x,CultureInfo.CurrentCulture,DateTimeStyles.AllowWhiteSpaces,out var time))r.At=time;else _c.Notice("Enter a date such as 2026-10-23 20:00 +02:00.");},230));Row("Repeat every (minutes)","0 is a one-time reminder.",Number(r.RepeatMinutes,0,10080,x=>r.RepeatMinutes=x));Row("Alert minutes before","Comma-separated lead times, 0–45. The start alert is included.",Field(r.LeadMinutes,x=>r.LeadMinutes=x));Actions(ActionButton("Remove reminder","Clear",()=>{P.Reminders.Remove(r);Change();SelectPage("Event tracking");}));
        }
        if(P.Reminders.Count==0)Help("No event reminders yet.");
    }
    private void RefreshDebug(){if(_debug is not null)_debug.Text=_c.DebugText;}
    private void Keybinds()
    {
        Section("Global hotkeys");Help("Click a binding, then press the combination. Escape cancels; Backspace clears. Another app may already own a shortcut—AscendAPP reports registration failures.");
        void KeyRow(string title,string icon,string value,Action<string> set){var b=ActionButton(value,icon,()=>{_c.SuspendShortcuts();try{var dialog=new HotkeyDialog{Owner=this};if(dialog.ShowDialog()==true){set(dialog.Chord);Change();SelectPage("Keybinds");}}finally{_c.ResumeShortcuts();}});b.MinWidth=170;Row(title,null,b);}
        KeyRow("Toggle combat strip","Strip",P.StripHotkey,x=>P.StripHotkey=x);KeyRow("Show / hide event timers","Bell",P.EventsHotkey,x=>P.EventsHotkey=x);KeyRow("Hide / restore overlay","Overlay",P.HideHotkey,x=>P.HideHotkey=x);KeyRow("Reset encounter","Reset",P.ResetHotkey,x=>P.ResetHotkey=x);KeyRow("Clear current meter","Clear",P.ClearHotkey,x=>P.ClearHotkey=x);KeyRow("Copy summary","Copy",P.CopyHotkey,x=>P.CopyHotkey=x);KeyRow("Toggle click-through","ClickThrough",P.ClickThroughHotkey,x=>P.ClickThroughHotkey=x);
        Divider();Help("The combat-strip shortcut switches between the normal compact meter and its single-line view.");
    }
    private void Pets()
    {
        Section("Pet-soul collection");
        Row("Loot counters","Draw progress to the left of Soul: pickup rows. Independent of the full pet window.",Toggle(P.ShowPetPickups,x=>P.ShowPetPickups=x));
        Row("Counter style","Loot rows match the reference animation. The floating card is optional.",Choice(new[]{"Beside loot rows","Floating card"},P.PetCounterStyle,x=>{P.PetCounterStyle=x;Dispatcher.BeginInvoke(new Action(()=>SelectPage("Pet tracking")));},180));
        Actions(ActionButton("Use counters only","Pet",_c.UsePetCountersOnly),ActionButton("Choose loot text area","Display",_c.ConfigureLootArea),ActionButton("Reset loot area","Reset",_c.ResetLootArea));
        Help(_c.LootCounterStatus);
        Help("The row counter reads only the selected area of the foreground game picture, in English, locally. It pauses when the game is not in front. No screenshots or recognized text are saved/uploaded. OCR locates names; only captured game data changes your collection totals.");
        Row("Space for item icons","Distance between the text area and the counter, adapted to text size.",Number(P.LootCounterGap,0,140,x=>P.LootCounterGap=x));
        Actions(ActionButton("Preview row counters","Pet",_c.PreviewLootCounters));
        if(P.PetCounterStyle=="Floating card")Actions(ActionButton("Preview floating card","Pet",_c.PreviewPetPickup),ActionButton("Reset card position","Reset",_c.ResetPetPickupPosition));
        Help(_c.Pets.Status);
        Row("Show pet window","A separate compact overlay. Soul counts update independently of DPS filters.",Toggle(P.ShowPetOverlay,_c.SetPetOverlayVisible));
        Row("Pinned pets only","Pin a pet from its row to keep your farming list focused.",Toggle(P.PetOnlyPinned,x=>P.PetOnlyPinned=x));
        Row("Hide completed pets","Pets at level 3 are hidden, except briefly after a pickup.",Toggle(P.PetHideCompleted,x=>P.PetHideCompleted=x));
        Slider("Pet overlay scale","Independent of the damage meter.",P.PetOverlayScale,.8,1.5,x=>P.PetOverlayScale=x,x=>$"{x:P0}");
        Slider("Pet overlay opacity","Keeps the game visible behind your farming list.",P.PetOverlayOpacity,.2,1,x=>P.PetOverlayOpacity=x,x=>$"{x:P0}");
        Actions(ActionButton("Show tracker","Pet",()=>_c.SetPetOverlayVisible(true)),ActionButton("Reset position","Reset",_c.ResetPetOverlayPosition));
        Divider();Section("Automatic synchronization");
        Help("Keep capture running before entering the world. If the tracker stays on Needs sync after re-entering your character, copy pet diagnostics below. No manual total is invented. A full collection establishes your levels and current souls; individual soul-gain messages update them. Saved values remain marked as saved until a new collection is received. After a capture gap, pickups still show +N but totals stay unconfirmed.");
        Help("Pet portraits are bundled for offline use. No kill-based estimates, game memory reads or online requests. Loot-row mode uses local, foreground-game text recognition only. Unknown pets show their numeric ID. Name data contains 207 English pet names; no map or radar is used.");
        Actions(ActionButton("Copy pet diagnostics","Copy",_c.CopyPetDiagnostics),ActionButton("Capture diagnostics","Debug",()=>SelectPage("Debug")));
    }
    private void Character()
    {
        Section("Character scores");Help(_c.CharacterProfileLabel);
        Row("Your gear score",_c.Model.SelfRatingInfo,new TextBlock{Text=_c.Model.SelfGearScore,Foreground=Accent,FontSize=18});
        Row("Your combat power",null,new TextBlock{Text=_c.Model.SelfCombatPower,Foreground=Accent,FontSize=18});
        Help(_c.ProfileStatus);
        Help("Automatic sources: the own login packet, party roster and nearby player profiles. Keep capture running before entering your character and joining a party. New observations update the scores; unsupported or missing fields stay unknown.");
        Help("No manual entry is used. Scores are the last values received from the game, with source and age in the tooltip. The Refresh button redraws known data; it does not request new values from the game.");
        Divider();Section("Observed profiles");
        Help("Recent actual profile values; this list includes nearby players. It does not add them to party DPS. Unknown or expired fields remain blank. Refresh updates this list without sending requests to the game.");
        var grid=new DataGrid{ItemsSource=_c.ProfileRows,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,Height=180,Margin=new Thickness(0,0,0,10)};
        foreach(var (title,binding,width)in new[]{("Player","Name",180),("Server","Server",80),("GS","GS",90),("CP","CP",90)})grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(binding),Width=width});
        Page.Children.Add(grid);
        Actions(ActionButton("Refresh displayed scores","Reset",()=>{_c.RefreshScreen();SelectPage("Character");}),ActionButton("Capture diagnostics","Debug",()=>SelectPage("Debug")));
    }
    private void Account(){Section("Account");Help("FooMeter currently works locally and does not require an account.");Unavailable("Sign-in and profile sync","A FooMeter account service is not configured.");Unavailable("Player ranks and nameplates","A separate leaderboard/profile service is required.");Actions(ActionButton("Observed character tickets","Ticket",()=>SelectPage("Tickets")));}
    private void Integrations(){Section("Local data");Help("Sessions and reports stay on your computer. Exported sessions can contain character names.");Actions(ActionButton("Import session","Import",_c.Import),ActionButton("Export session","Export",_c.ExportJson),ActionButton("Export ranking CSV","Export",_c.ExportCsv),ActionButton("Export diagnostics","Debug",_c.ExportReport));Divider();Section("Online services");Unavailable("Automatic uploads and account integrations","No upload endpoint or online account is configured. FooMeter does not send combat data anywhere.");}
    private void Tickets()
    {
        Section("Character tickets");Help("Last observed balances, not live queries. Start capture before character entry to update a character. A missing ticket is unknown. No caps or refills are assumed.");
        var grid=new DataGrid{ItemsSource=_c.Model.Tickets,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,MaxHeight=430,HeadersVisibility=DataGridHeadersVisibility.Column,FontSize=10};
        foreach(var (title,path,width)in new[]{("Character","Character",140),("Ticket / regular + bonus","Detail",260),("Available","Energy",70),("Last seen","Seen",145)})grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(path),Width=width});Page.Children.Add(grid);Actions(ActionButton("Forget saved balances","Clear",_c.ForgetTickets));
    }
    private void Changelog(){Section("0.10.29 · Loot-row counters");Help("• Progress is drawn beside visible Soul: loot rows, following their movement.\n• Local OCR only locates item names; game packets still establish totals.\n• Explicit Use counters only / hide window action.\n• Choose loot area and preview controls; floating card remains optional.");Divider();Section("0.10.28 · Your colors and compact events");Help("• Choose app accent, panel and flame colors in Appearance.\n• Slim single-line event/boss timers.\n• Offline portraits for 207 pets and a separate fading pickup counter.\n• Fixed a coalesced login ordering case; pet reports now distinguish raw frames, rejected layouts and bound totals.");Divider();Section("0.10.27 · FooMeter");Help("• Pink flame identity and a unified charcoal/pink theme.\n• Optional pet-soul overlay with pickups, pinned farming lists and automatic collection messages.\n• Separate per-character saved progress; gaps and cached totals are explicitly labelled.");Divider();Section("0.10.26 · Saved fights and combat strip");Help("• Fight history is saved locally across restarts, with full breakdowns, export and confirmed deletion.\n• Keep up to 80 fights within 16 MB; damaged files get a recovery copy.\n• Closing or stopping a fight keeps a partial record.\n• Toggle the one-line combat strip with Ctrl+Shift+F6.\n• Capture status now distinguishes traffic, framing, decoding, identity and filtered events.");Divider();Section("0.10.25 · Meter and events");Help("• Removed target cooldown tracking, public profile lookups and manual skill/buff timers.\n• Search encounter history by target, class or visible player.\n• Compare completed fights against the same target using full-encounter DPS and duration.\n• Independent event overlay scale and opacity.\n• Existing meter, event, score and ticket settings carry forward.");Divider();Section("0.10.24 · Slim Obsidian Flow");Help("• Compact 26px bars: original class icon, Name, GS, CP and damage numbers on one row.\n• Single-line footer, corrected scrolling and embedded class emblems.\n• Shared map removed.");Divider();Section("Retained features");Help("Automatic GS/CP, boss/dummy capture, confirmed party filtering, effective healing estimates, skill damage breakdowns, event/boss timers, character tickets, exports and Alt interaction remain available. Earlier release details are in the source documentation.");}
    private void Debug(){Section("Capture diagnostics");_debug=new TextBlock{Text=_c.DebugText,TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Muted,LineHeight=18,Margin=new Thickness(0,0,0,16)};Page.Children.Add(_debug);Actions(ActionButton("Save diagnostics report","Debug",_c.ExportReport));Divider();Section("Preview data");Help("These actions stop live capture and display labelled synthetic data.");Actions(ActionButton("Run demo","Play",_c.Demo),ActionButton("Load showcase","Display",_c.Showcase),ActionButton("Stop","Stop",_c.Stop));Divider();Unavailable("Additional death decoding","Additional verified Global message layouts are still required. Capture reports include counters and metadata, without raw packet bodies or endpoints.");}
    private void Drag_Click(object sender,MouseButtonEventArgs e){if(e.LeftButton!=MouseButtonState.Pressed)return;for(var node=e.OriginalSource as DependencyObject;node is not null;node=VisualTreeHelper.GetParent(node))if(node is System.Windows.Controls.Primitives.ButtonBase)return;try{DragMove();}catch(InvalidOperationException){}}
    private void Close_Click(object sender,RoutedEventArgs e){Page.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));Close();}
}
