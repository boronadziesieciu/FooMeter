using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Settings;

namespace FooMeter.Linux;

public sealed class MeterWindow : Window
{
    private readonly LinuxMeter _meter=new(Program.Demo);
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
    private readonly DispatcherTimer _inputTimer=new(){Interval=TimeSpan.FromMilliseconds(60)};
    private readonly XInput _input=new();
    private CommandServer? _commands;
    private readonly LootReader _loot;
    private readonly ReminderScheduler _reminders=new();
    private readonly TextBlock _summary=Ui.Text("Waiting for combat",14);
    private readonly TextBlock _boss=Ui.Text("",11);
    private readonly TextBlock _status=Ui.Text("",11);
    private readonly StackPanel _players=new(){Spacing=4};
    private readonly StackPanel _root=new(){Spacing=8,Margin=new(10)};
    private readonly TextBlock _title=Ui.Text("FooMeter",18);
    private readonly List<Window> _children=[];
    private readonly Dictionary<string,Window> _panels=new();
    private readonly Dictionary<string,TextBlock> _panelStatus=new();
    private readonly Dictionary<string,StackPanel> _panelRows=new();
    private TextBox _petSearch=new(){Watermark="Find a pet"};
    private readonly HashSet<string> _alerts=[];
    private Window? _pickupWindow;
    private readonly TextBlock _pickupText=Ui.Text("",14);
    private TrayIcon? _tray;
    private bool _closing;
    private readonly LayoutTransformControl _scaled=new();
    private readonly Canvas _lootCanvas=new();
    private Window? _lootWindow;
    private readonly XInput _lootInput=new();
    private TextBlock? _lootStatus;
    private int _petHash;
    private string? _lastLootRegion;
    private int _lastRender;
    private bool _healingView;
    private MeterView? _view;
    private TextBlock? _captureStatus;
    public MeterWindow()
    {
        _loot=new(_meter);
        Title="FooMeter Linux";Width=440;Height=360;MinWidth=330;MinHeight=100;
        SizeToContent=SizeToContent.Height;MaxHeight=800;SystemDecorations=SystemDecorations.None;CanResize=true;Topmost=true;FontSize=12;
        var header=new DockPanel();
        var grip=Ui.Text("⠿",20);grip.PointerPressed+=(_,e)=>{if(!_meter.Preferences.PositionLocked&&e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};
        header.Children.Add(grip);_title.Text=Program.Demo?"FooMeter · DEMO":"FooMeter";header.Children.Add(_title);
        var menu=new Menu {ItemsSource=new[]{
            Item("☰",null,
                Item("Capture…",()=>CaptureWindow()),Item("Settings…",()=>Settings()),Item("Fight history…",()=>Open(ReviewWindows.History(_meter))),
                Item("Events",()=>TogglePanel("Events")),Item("Healing view",()=>{_healingView=!_healingView;_lastRender=0;}),Item("Pets",()=>TogglePanel("Pets")),Item("New encounter",()=>_meter.Reset()),
                Item("One-line strip",()=>{_meter.Preferences.CombatStrip=!_meter.Preferences.CombatStrip;Save();}),
                Item("Copy result",()=>_ = Copy()),Item("Export current CSV…",()=>_ = Safe(()=>Ui.Export(this,_meter.View().Fight,false))),
                Item("Click-through (hold Alt to interact)",()=>ToggleInput()),Item("Linux integration…",()=>LinuxMenu()),
                Item("Minimize",()=>WindowState=WindowState.Minimized),Item("Quit",()=>Close()))}};
        DockPanel.SetDock(menu,Dock.Right);header.Children.Insert(0,menu);
        _root.Children.Add(header);_root.Children.Add(_summary);_root.Children.Add(_boss);_root.Children.Add(_players);_root.Children.Add(_status);
        _scaled.Child=_root; Content=new Border{CornerRadius=new(8),Child=_scaled};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape)_meter.Preferences.ClickThrough=false;};
        if(_meter.Preferences.Left is {} savedX&&_meter.Preferences.Top is {} savedY)Position=new((int)savedX,(int)savedY);
        PositionChanged+=(_,_)=>{if(!_closing){_meter.Preferences.Left=Position.X;_meter.Preferences.Top=Position.Y;}};
        _timer.Tick+=(_,_)=>Paint();_inputTimer.Tick+=(_,_)=>_input.Apply(this,_meter.Preferences.ClickThrough);
        Opened+=async(_,_)=> {
            ApplySettings();SetupTray();Paint();_timer.Start();_inputTimer.Start();
            if(!Program.Demo)try{_commands=new(Execute);}catch(Exception e){_status.Text=e.Message;}
            if(Program.SmokePath is {} path) {await Smoke(path);return;}
            if(!Program.Demo&&_meter.Preferences.AutoCapture) {
                if(_meter.Preferences.Adapter.Length>0)await Safe(()=>_meter.Start(_meter.Preferences.Adapter));else CaptureWindow();
            }
        };
        Closing+=async(_,e)=> {
            if(_closing)return;e.Cancel=true;_closing=true;_timer.Stop();_inputTimer.Stop();
            foreach(var child in _children.ToArray())child.Close();
            _commands?.Dispose();_tray?.Dispose();_input.Dispose();_loot.Dispose();_lootWindow?.Close();_pickupWindow?.Close();_lootInput.Dispose();
            try{await _meter.Stop();Save();_meter.Dispose();}catch(Exception ex){Console.Error.WriteLine(ex.Message);}Close();
        };
    }
    private static MenuItem Item(string label,Action? action,params MenuItem[] children)
    {var item=new MenuItem{Header=label};if(children.Length>0)item.ItemsSource=children;if(action is not null)item.Click+=(_,_)=>action();return item;}
    private void Open(Window window){_children.Add(window);window.Closed+=(_,_)=>_children.Remove(window);window.Show(this);}
    private async Task Safe(Func<Task> action) {try{await action();}catch(Exception e){_status.Text=e.Message;var w=Ui.Window("FooMeter · Action failed",Ui.Scroll(Ui.Text(e.Message)),480,240);Open(w);}}
    private void Save(){_meter.SavePreferences(_meter.Preferences);ApplySettings();}
    private void ApplySettings()
    {
        if(_closing)return;
        var p=_meter.Preferences;Opacity=p.OverlayOpacity;Background=Brush.Parse(p.PanelColor);_title.Foreground=Brush.Parse(p.LogoFollowsAccent?p.AccentColor:p.LogoColor);
        _scaled.LayoutTransform=new ScaleTransform(p.UiScale,p.UiScale);
        _timer.Interval=TimeSpan.FromMilliseconds(p.UpdateMs);_lastRender=0;
        if(p.Left is {} x&&p.Top is {} y&&!IsVisible)Position=new((int)x,(int)y);
        if(!p.ShowEventOverlay&&_panels.ContainsKey("Events"))TogglePanel("Events");
        if(!p.ShowPetOverlay&&_panels.ContainsKey("Pets"))TogglePanel("Pets");
        if(p.ShowEventOverlay&&!_panels.ContainsKey("Events"))TogglePanel("Events");
        if(p.ShowPetOverlay&&!_panels.ContainsKey("Pets"))TogglePanel("Pets");
    }
    private void Settings(){var window=new SettingsWindow(_meter,()=>{ApplySettings();Paint();});Open(window);}
    private async Task Copy(){await Safe(async()=>{if(Clipboard is {} c)await c.SetTextAsync(Ui.Share(_meter.View().Fight,_meter.Preferences.CopyFormat=="Multi-line",_meter.Preferences.HideNames));});}
    private void ToggleInput(){_meter.Preferences.ClickThrough=!_meter.Preferences.ClickThrough;Save();}
    private void Execute(string command)
    {switch(command){case "show":Show();Activate();break;case "hide":Hide();break;case "toggle":if(IsVisible)Hide();else Show();break;case "reset":_meter.Reset();break;case "copy":_ = Copy();break;case "strip":_meter.Preferences.CombatStrip=!_meter.Preferences.CombatStrip;Save();break;case "events":TogglePanel("Events");break;case "pets":TogglePanel("Pets");break;case "clickthrough":ToggleInput();break;case "settings":Settings();break;case "quit":Close();break;}}
    private void SetupTray()
    {
        string icon=Path.Combine(AppContext.BaseDirectory,"assets","FooMeter.ico");if(!File.Exists(icon))return;
        _tray=new TrayIcon {Icon=new WindowIcon(icon),ToolTipText="FooMeter Linux"};
        var menu=new NativeMenu();
        foreach(var (name,command) in new[]{("Show / hide","toggle"),("Settings","settings"),("New encounter","reset"),("Copy result","copy"),("Events","events"),("Pets","pets"),("Quit","quit")})
        {var entry=new NativeMenuItem(name);entry.Click+=(_,_)=>Execute(command);menu.Add(entry);}
        _tray.Menu=menu;_tray.Clicked+=(_,_)=>Execute("show");TrayIcon.SetIcons(Avalonia.Application.Current!,new TrayIcons{_tray});
    }
    private void CaptureWindow()
    {
        var adapters=new ComboBox{MinWidth=260};var port=new TextBox{Text=_meter.Preferences.Port,Watermark="13328"};
        _captureStatus=Ui.Text(_meter.View().Status);var window=Ui.Window("FooMeter · Capture",new Border(),580,440);
        async Task Refresh(){var rows=await LinuxMeter.Devices();adapters.ItemsSource=rows;adapters.SelectedIndex=Math.Max(0,Array.FindIndex(rows,r=>r.Contains(". "+_meter.Preferences.Adapter+" ")||r.EndsWith(". "+_meter.Preferences.Adapter)));}
        var panel=Ui.Stack(Ui.Text("Packet capture",22),Ui.Text("Start capture before entering your character. Use your Ethernet adapter or any. Requires dumpcap capture permissions."),adapters,Ui.Text("Game TCP port (blank = 13328)"),port,
            Ui.Async("Refresh adapters",Refresh,e=>_captureStatus.Text=e.Message),
            Ui.Async("Start capture",async()=>{if(adapters.SelectedItem is not string row)throw new InvalidOperationException("Select an adapter.");if(port.Text?.Length>0&&(!int.TryParse(port.Text,out int v)||v is <1 or >65535))throw new FormatException("Invalid TCP port");_meter.Preferences.Port=port.Text??"";string name=row[(row.IndexOf(". ")+2)..].Split(' ')[0];await _meter.Start(name);},e=>_captureStatus.Text=e.Message),
            Ui.Async("Stop capture",()=>_meter.Stop(),e=>_captureStatus.Text=e.Message),_captureStatus,Ui.Text("No packets? Check dumpcap -D and group membership. Packets but no identity? Enter the character again while capture is running.",12));
        window.Content=new Border{Padding=new(16),Child=Ui.Scroll(panel)};Open(window);_ = Safe(Refresh);
    }
    private void TogglePanel(string name)
    {
        if(_panels.Remove(name,out var old)){old.Close();return;}
        var rows=new StackPanel{Spacing=8};var status=Ui.Text("");var panel=Ui.Stack(status);
        if(name=="Pets") {_petSearch=new TextBox{Watermark="Find a pet",Text=_petSearch.Text};panel.Children.Add(_petSearch);var controls=new WrapPanel();
            var pinned=new CheckBox{Content="Pinned only",IsChecked=_meter.Preferences.PetOnlyPinned};pinned.IsCheckedChanged+=(_,_)=>{_meter.Preferences.PetOnlyPinned=pinned.IsChecked==true;Save();};controls.Children.Add(pinned);
            var complete=new CheckBox{Content="Hide maxed",IsChecked=_meter.Preferences.PetHideCompleted};complete.IsCheckedChanged+=(_,_)=>{_meter.Preferences.PetHideCompleted=complete.IsChecked==true;Save();};controls.Children.Add(complete);panel.Children.Add(controls);}
        panel.Children.Add(rows);var window=Ui.Window("FooMeter · "+name,new Border(),name=="Pets"?360:320,520);window.Topmost=true;
        window.PositionChanged+=(_,_)=>{var p=_meter.Preferences;if(name=="Events"){p.EventOverlayLeft=window.Position.X;p.EventOverlayTop=window.Position.Y;}else{p.PetOverlayLeft=window.Position.X;p.PetOverlayTop=window.Position.Y;}};
        window.SystemDecorations=SystemDecorations.None;
        var grip=Ui.Text("⠿ "+name,17);
        grip.PointerPressed+=(_,e)=>{var p=_meter.Preferences;if(!(name=="Pets"?p.PetOverlayLocked:p.EventOverlayLocked)&&e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)window.BeginMoveDrag(e);};
        panel.Children.Insert(0,Ui.Button("Close "+name,()=>window.Close()));panel.Children.Insert(0,grip);
        var scale=new LayoutTransformControl{Child=Ui.Scroll(panel)};window.Content=new Border{Padding=new(10),Child=scale};
        var prefs=_meter.Preferences;scale.LayoutTransform=new ScaleTransform(name=="Pets"?prefs.PetOverlayScale:prefs.EventOverlayScale??prefs.UiScale,name=="Pets"?prefs.PetOverlayScale:prefs.EventOverlayScale??prefs.UiScale);
        double? x=name=="Events"?prefs.EventOverlayLeft:prefs.PetOverlayLeft,y=name=="Events"?prefs.EventOverlayTop:prefs.PetOverlayTop;
        if(x is {} px&&y is {} py)window.Position=new((int)px,(int)py);
        window.Closed+=(_,_)=>{_petHash=0;_panels.Remove(name);_panelRows.Remove(name);_panelStatus.Remove(name);if(!_closing){if(name=="Events")_meter.Preferences.ShowEventOverlay=false;else _meter.Preferences.ShowPetOverlay=false;}};
        _panels[name]=window;_panelRows[name]=rows;_panelStatus[name]=status;if(name=="Events")prefs.ShowEventOverlay=true;else prefs.ShowPetOverlay=true;Open(window);
    }
    private void Paint()
    {
        var view=_meter.View();_view=view;var f=view.Fight;var p=_meter.Preferences;var now=DateTimeOffset.UtcNow;
        _status.Text=Program.Demo?"DEMO · synthetic data":view.Status;
        if(_captureStatus is not null)_captureStatus.Text=view.Status+"\n"+view.Diagnostics.Summary;
        _summary.Text=p.CombatStrip?$"{f.TargetName??"Waiting"} · {f.DurationSeconds:N0}s · {f.PartyDps:N0} DPS":$"{f.TargetName??"Waiting for combat"} · {f.DurationSeconds:N0}s"+(p.ShowPartyDps?$"\n{f.PartyDps:N0} DPS":"")+(!p.HideTotalDamage?$" · {f.TotalDamage:N0} damage":"")+$" · {f.TotalHealing:N0} healing";
        _boss.IsVisible=!p.CombatStrip;
        _boss.Text=(f.Boss is {} targetBoss?$"HP {targetBoss.CurrentHp?.ToString("N0")??"?"} / {targetBoss.MaxHp?.ToString("N0")??"?"}":"")+
            (view.Forecast.KillSeconds is {} kill?$" · estimated defeat {kill:N0}s":"")+(p.ShowBerserk&&view.Forecast.BerserkSeconds is {} rage?$" · berserk {rage:N0}s":"");
        _players.IsVisible=!p.CombatStrip;
        int hash=HashCode.Combine(f.TotalDamage,f.TotalHealing,f.DurationSeconds,p.HideNames,p.VisiblePlayers,p.AccentColor);
        if(hash!=_lastRender){_lastRender=hash;_players.Children.Clear();foreach(var player in (_healingView?f.Players.OrderByDescending(x=>x.Healing):f.Players.AsEnumerable()).Take(p.VisiblePlayers)){
            var rating=view.Ratings.GetValueOrDefault(player.ActorId);var content=new DockPanel();var icon=Ui.Picture("Classes/"+player.ClassName+".png");if(icon is not null)content.Children.Add(icon);
            var label=Ui.Stack(Ui.Text($"{(player.IsSelf?"★ ":"")}{(p.HideNames&&!player.IsSelf?player.ClassName??"Player":player.Name)} · GS {(rating?.ManualGearScore==true?"*":"")}{rating?.GearScore?.ToString()??"?"} / CP {(rating?.ManualCombatPower==true?"*":"")}{rating?.CombatPower?.ToString()??"?"}"),
                new ProgressBar{Minimum=0,Maximum=1,Value=player.Share,Height=3,Foreground=Brush.Parse(p.AccentColor)},Ui.Text(_healingView?$"{player.Hps(f.DurationSeconds):N0} HPS · {player.Healing:N0} estimated healing":$"{player.Dps:N0} DPS · {player.Damage:N0} damage · {player.Share:P0}",12));content.Children.Add(label);
            var button=new Button{Content=content,HorizontalContentAlignment=HorizontalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Stretch};button.Click+=(_,_)=>Open(ReviewWindows.Breakdown(f,player));_players.Children.Add(button);
        }}
        Opacity=p.FadeIdle&&!f.IsActive&&f.Players.Count>0&&(now-f.StartedAt.AddSeconds(f.DurationSeconds)).TotalSeconds>=p.FadeAfterSeconds?p.FadedOpacity:p.OverlayOpacity;
        if(p.GameTimerAlerts)foreach(var e in view.Events)foreach(var lead in new EventReminder{LeadMinutes=p.GameTimerLeadMinutes}.Leads()) {
            var left=e.CountdownAt-now;if(left.TotalSeconds>=lead*60&&left.TotalSeconds<lead*60+2)NotifyOnce(e.Event.Id+e.CountdownAt.UtcTicks+":"+lead,e.Event.Name+" in "+lead+" minutes");
        }
        foreach(var pair in _panels.ToArray()){
            var name=pair.Key;var window=pair.Value;
            window.Background=Brush.Parse(p.PanelColor);
            if(window.Content is Border border && border.Child is LayoutTransformControl scale) {
                double value=name=="Pets"?p.PetOverlayScale:p.EventOverlayScale??p.UiScale;
                if(scale.LayoutTransform is not ScaleTransform existing||existing.ScaleX!=value)scale.LayoutTransform=new ScaleTransform(value,value);
            }var rows=_panelRows[name];if(name=="Events")rows.Children.Clear();
            if(name=="Events") {
                window.Opacity=p.EventOverlayOpacity??p.OverlayOpacity;
                if(p.EventVisibility=="Outside fights"&&f.IsActive){window.Hide();continue;}if(!window.IsVisible)window.Show();
                _panelStatus[name].Text="Events & bosses · "+now.ToLocalTime().ToString(p.Clock24?"HH:mm":"hh:mm tt");
                foreach(var e in view.Events.Where(e=>e.Event.Id.Contains("shugo",StringComparison.OrdinalIgnoreCase)?p.ShowShugoTimer:e.Event.Id.Contains("rift",StringComparison.OrdinalIgnoreCase)?p.ShowRiftTimer:p.ShowInvasionTimer)) {
                    var left=e.CountdownAt-now;rows.Children.Add(Ui.Text($"{e.Event.Name}{(e.Event.Possible?" (possible)":"")}\n{(e.InWindow?"Ends":"Starts")} in {left:hh\\:mm\\:ss}"));

                }
                if(p.ShowFieldBossTimers)foreach(var boss in view.Bosses)rows.Children.Add(Ui.Text(boss.Boss.Name+" · "+(boss.Observation is null?"Unknown":boss.Observation.Alive?"Observed alive":boss.Due is {} at?$"{Math.Max(0,(at-now).TotalMinutes):N0} minutes":"Unknown respawn")));
                foreach(var reminder in p.Reminders){var next=ReminderScheduler.Next(reminder,now);if(next is {} at)rows.Children.Add(Ui.Text($"Reminder: {reminder.Name} · {at.ToLocalTime():g}"));}
            } else {
                window.Opacity=p.PetOverlayOpacity;_panelStatus[name].Text=view.PetStatus;
                int petHash=HashCode.Combine(view.PetStatus,_petSearch.Text,p.PetOnlyPinned,p.PetHideCompleted,string.Join(',',p.PinnedPets),string.Join(';',view.Pets.Select(x=>$"{x.Id}:{x.Level}:{x.Points}:{x.Confirmed}")));
                if(petHash==_petHash)continue;_petHash=petHash;rows.Children.Clear();
                foreach(var pet in view.Pets.Where(x=>(!p.PetOnlyPinned||p.PinnedPets.Contains(x.Id))&&(!p.PetHideCompleted||!x.Complete)&&x.Name.Contains(_petSearch.Text??"",StringComparison.OrdinalIgnoreCase))) {
                    var dock=new DockPanel();var picture=Ui.Picture("Pets/"+pet.Id+".png",42);if(picture is not null)dock.Children.Add(picture);
                    var pin=new CheckBox{Content=pet.Name+"\nLevel "+(pet.Level?.ToString()??"?")+" · "+pet.Progress,IsChecked=p.PinnedPets.Contains(pet.Id)};
                    pin.IsCheckedChanged+=(_,_)=>{if(pin.IsChecked==true&&!p.PinnedPets.Contains(pet.Id))p.PinnedPets.Add(pet.Id);else if(pin.IsChecked!=true)p.PinnedPets.Remove(pet.Id);Save();};dock.Children.Add(pin);rows.Children.Add(dock);
                }
            }
        }
        foreach(var alert in _reminders.Poll(p.Reminders,now))NotifyOnce(alert.Id+":"+alert.Due.UtcTicks,alert.Name+" in "+alert.LeadMinutes+" minutes");
        var pickup=_meter.Pickup;
        if(p.ShowPetPickups&&pickup is {} gain&&now-gain.At<TimeSpan.FromSeconds(8)) {
            if(_pickupWindow is null) {_pickupWindow=Ui.Window("FooMeter · Soul pickup",_pickupText,300,110);_pickupWindow.Topmost=true;_pickupWindow.ShowInTaskbar=false;_pickupWindow.Closed+=(_,_)=>_pickupWindow=null;Open(_pickupWindow);}
            _pickupText.Text=$"{gain.Name} +{gain.Amount} souls\n"+(gain.Confirmed?$"Level {gain.Level} · {gain.Points} points":"Total unknown · waiting for collection sync");
        } else if(_pickupWindow is not null){_pickupWindow.Close();_pickupWindow=null;}
        if(_lootStatus is not null)_lootStatus.Text=_loot.Status;
        if(_lootWindow is not null)UpdateLoot();
    }
    private void NotifyOnce(string key,string text)
    {if(!_alerts.Add(key))return;if(_alerts.Count>5000)_alerts.Clear();_ = LootReader.Run("notify-send",CancellationToken.None,"FooMeter",text).ContinueWith(_=>{});}
    private void UpdateLoot()
    {
        var w=_lootWindow!;_lootCanvas.Children.Clear();
        if(!_loot.Running||_loot.Rows.Count==0){w.Hide();return;}
        var parts=_loot.Region.Split(new[]{',',' ','x'},StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length!=4)return;
        if(_lastLootRegion!=_loot.Region){_lastLootRegion=_loot.Region;w.Position=new(int.Parse(parts[0])+int.Parse(parts[2])+_meter.Preferences.LootCounterGap,int.Parse(parts[1]));w.Height=int.Parse(parts[3]);}
        foreach(var row in _loot.Rows){var label=Ui.Text(row.Text+(row.Fresh?"":" *"),14);label.Foreground=Brush.Parse(_meter.Preferences.AccentColor);Canvas.SetTop(label,row.Y);_lootCanvas.Children.Add(label);}
        if(!w.IsVisible)w.Show();_lootInput.Apply(w,true);
    }
    private void StartLoot()
    {
        _loot.Start();
        if(_lootWindow is not null)return;
        _lootWindow=new Window{Title="FooMeter · Loot counters",Width=160,Height=500,SystemDecorations=SystemDecorations.None,CanResize=false,Topmost=true,ShowInTaskbar=false,Background=Brushes.Transparent,TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent},Content=_lootCanvas};
        _lootWindow.Closed+=(_,_)=>_lootWindow=null;
    }
    private void LinuxMenu()
    {
        var info=Ui.Text("");_lootStatus=info;var commands=new TextBox{AcceptsReturn=true,IsReadOnly=true,MinHeight=140,Text="Hyprland bind commands (replace /path with your extracted folder):\n/path/run-foometer.sh --command toggle\n/path/run-foometer.sh --command reset\n/path/run-foometer.sh --command copy\n/path/run-foometer.sh --command clickthrough\n/path/run-foometer.sh --command strip\n/path/run-foometer.sh --command events\n/path/run-foometer.sh --command pets"};
        var panel=Ui.Stack(Ui.Text("Linux integration",22),commands,Ui.Text("Assign these commands to keys in Hyprland. The tray can always show the window; Alt temporarily restores XWayland input when click-through is enabled."),
            Ui.Button("Toggle click-through",ToggleInput),Ui.Text("Loot-row OCR",18),Ui.Text("Install grim, slurp, tesseract and English data. Select only the loot text region. Reading begins only after you enable it."),
            Ui.Async("Choose loot region",async()=>{await _loot.Choose();info.Text=_loot.Status;},e=>info.Text=e.Message),
            Ui.Button("Start loot reader",()=>{try{StartLoot();}catch(Exception e){info.Text=e.Message;}}),
            Ui.Button("Stop loot reader",()=>{_loot.Dispose();info.Text=_loot.Status;}),info,Ui.Text("Local reminders",18),Ui.Button("Add reminder…",Reminders),
            Ui.Text("Profile: "+_meter.ProfileDirectory),Ui.Button("Open profile folder",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open",_meter.ProfileDirectory){UseShellExecute=false})),
            Ui.Button("Open Linux update page",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open","https://github.com/boronadziesieciu/FooMeter/tree/linux-preview/linux-preview"){UseShellExecute=false})),
            Ui.Button("Diagnostics",()=>Open(Ui.Window("FooMeter · Diagnostics",Ui.Scroll(Ui.Text(_meter.View().Diagnostics.Summary+"\n"+_meter.View().Identity+"\n"+_meter.View().HistoryStatus+"\n"+_loot.Status))))));Open(Ui.Window("FooMeter · Linux",Ui.Scroll(panel)));
    }
    private void Reminders()
    {
        var name=new TextBox{Text="Reminder"};var minutes=new TextBox{Text="30"};var repeat=new TextBox{Text="0"};var status=Ui.Text("");var w=Ui.Window("FooMeter · Reminders",new Border(),460,500);var list=new StackPanel();
        void Refresh(){list.Children.Clear();foreach(var r in _meter.Preferences.Reminders.ToArray())list.Children.Add(Ui.Button("Delete "+r.Name+" · "+r.At.ToLocalTime().ToString("g"),()=>{_meter.Preferences.Reminders.Remove(r);Save();Refresh();}));}Refresh();
        w.Content=Ui.Scroll(Ui.Stack(Ui.Text("Reminder name"),name,Ui.Text("Due in minutes"),minutes,Ui.Text("Repeat every minutes (0 = once)"),repeat,Ui.Button("Add",()=>{if(!int.TryParse(minutes.Text,out int m)||m is <1 or >10080||!int.TryParse(repeat.Text,out int r)||r is <0 or >10080){status.Text="Enter valid minutes (1–10080).";return;}_meter.Preferences.Reminders.Add(new(){Name=name.Text??"Reminder",At=DateTimeOffset.Now.AddMinutes(m),RepeatMinutes=r});Save();Refresh();}),status,list));Open(w);
    }
    private async Task Smoke(string path)
    {
        await Task.Delay(500);
        try{
            var view=_meter.View();if(view.Fight.TotalDamage!=20000||_players.Children.Count!=2)throw new Exception("Combat view failed");
            _input.Apply(this,true);if(_input.HasInput(this))throw new Exception("Click-through input shape was not cleared");
            _input.Apply(this,false);if(!_input.HasInput(this))throw new Exception("Input shape did not restore");
            bool received=false;using(var server=new CommandServer(command=>received=command=="settings")){CommandServer.Send("settings");await Task.Delay(120);if(!received)throw new Exception("Hyprland command IPC failed");}
            var settings=new SettingsWindow(_meter,()=>{});Open(settings);await Task.Delay(100);
            var tabs=settings.GetVisualDescendants().OfType<TabControl>().First();
            for(int i=0;i<((System.Collections.IEnumerable)tabs.ItemsSource!).Cast<object>().Count();i++){tabs.SelectedIndex=i;await Task.Delay(50);}
            tabs.SelectedIndex=2;await Task.Delay(100);
            using(var screenshot=new RenderTargetBitmap(new((int)settings.Bounds.Width,(int)settings.Bounds.Height))){screenshot.Render(settings);screenshot.Save(Path.Combine(Path.GetDirectoryName(path)!,"FooMeterSettings.png"));}
            var accent=settings.GetVisualDescendants().OfType<TextBox>().Single(x=>x.Name=="AccentColor");accent.Text="#34C4A8";
            settings.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string=="Save and apply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(_meter.Preferences.AccentColor!="#34C4A8")throw new Exception("Settings Save did not apply edited accent");
            ApplySettings();
            TogglePanel("Events");TogglePanel("Pets");Open(ReviewWindows.Breakdown(view.Fight,view.Fight.Players[0]));var history=ReviewWindows.History(_meter);Open(history);Paint();await Task.Delay(400);
            var historyList=history.GetVisualDescendants().OfType<ListBox>().First();historyList.SelectedIndex=0;historyList.SelectedItems!.Add(historyList.Items[1]);
            if(!history.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text?.Contains("DPS change:")==true))throw new Exception("History comparison did not update");
            if(settings.EditorCount<40||_panelRows["Pets"].Children.Count!=207||_panelRows["Events"].Children.Count<2)throw new Exception("Menus did not populate");
            foreach(var child in _children.Where(c=>c.IsVisible).ToArray()){
                using var shot=new RenderTargetBitmap(new(Math.Max(1,(int)child.Bounds.Width),Math.Max(1,(int)child.Bounds.Height)));shot.Render(child);
                string suffix=string.Concat(child.Title!.Where(char.IsLetterOrDigit));shot.Save(Path.Combine(Path.GetDirectoryName(path)!,suffix+".png"));
            }
            using var image=new RenderTargetBitmap(new((int)Bounds.Width,(int)Bounds.Height));image.Render(this);image.Save(Path.ChangeExtension(path,".png"));
            File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=true,Players=_players.Children.Count,Settings=settings.EditorCount,Pets=_panelRows["Pets"].Children.Count,Windows=_children.Count}));
        }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=false,Error=e.ToString()}));Environment.ExitCode=1;}Close();
    }
}
