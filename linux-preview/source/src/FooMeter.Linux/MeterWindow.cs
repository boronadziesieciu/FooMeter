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
    private readonly StackPanel _players=new(){Spacing=2};
    private readonly StackPanel _root=new(){Spacing=0,Margin=new(0)};
    private readonly TextBlock _title=OverlayStyle.Text("00:00",9);
    private readonly Grid _health=new(){Height=10,Margin=new(0,0,0,4)};
    private readonly Border _healthFill=new(){HorizontalAlignment=HorizontalAlignment.Left,Background=Brush.Parse("#24814C")};
    private readonly TextBlock _hpPercent=OverlayStyle.Text("—",8);
    private readonly Avalonia.Controls.Shapes.Ellipse _captureDot=new(){Width=5,Height=5,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,5,0)};
    private readonly TextBlock _modeText=OverlayStyle.Text("DPS",9,Brush.Parse("#F3D67A"));
    private Grid _hudFooter=null!;
    private readonly HashSet<Window> _fitPending=[];
    private readonly StackPanel _hudBody=new(){Spacing=0,Margin=new(6,0,6,0)};
    private Border _shell=null!;
    private ContextMenu _actions=null!;
    private readonly Dictionary<Window,Control> _overlayLayouts=new();
    private readonly List<Window> _children=[];
    private readonly Dictionary<string,Window> _panels=new();
    private readonly Dictionary<string,TextBlock> _panelStatus=new();
    private readonly Dictionary<string,StackPanel> _panelRows=new();
    private TextBox _petSearch=new(){Watermark="Find a pet"};
    private readonly HashSet<string> _alerts=[];
    private Window? _pickupWindow;
    private TextBlock _pickupText=OverlayStyle.Text("",11);
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
        Title="FooMeter Linux";FontSize=10;
        OverlayStyle.Configure(this,380);
        BuildHud();
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
    private void BuildHud()
    {
        var p=_meter.Preferences;
        _actions=new ContextMenu{ItemsSource=new[]{Item("Capture…",CaptureWindow),Item("Settings…",Settings),Item("Fight history…",()=>Open(ReviewWindows.History(_meter))),Item("Events",()=>TogglePanel("Events")),Item("Pet souls",()=>TogglePanel("Pets")),Item("New encounter",()=>_meter.Reset()),Item("Copy result",()=>_ = Copy()),Item("Export CSV…",()=>_ = Safe(()=>Ui.Export(this,_meter.View().Fight,false))),Item("One-line strip",()=>{_meter.Preferences.CombatStrip=!_meter.Preferences.CombatStrip;Save();}),Item("Click-through",ToggleInput),Item("Linux integration…",LinuxMenu),Item("Minimize",()=>WindowState=WindowState.Minimized),Item("Quit",()=>Close())}};
        var top=new Grid{Height=24,Margin=new(7,1,3,0),ColumnDefinitions=new("*,Auto")};
        _summary.FontSize=10;_summary.FontFamily=new FontFamily("DejaVu Sans Mono");_summary.TextWrapping=TextWrapping.NoWrap;_summary.TextTrimming=TextTrimming.CharacterEllipsis;_summary.VerticalAlignment=VerticalAlignment.Center;
        _summary.PointerPressed+=(_,e)=>{if((!_meter.Preferences.PositionLocked||e.KeyModifiers.HasFlag(KeyModifiers.Alt))&&e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)BeginMoveDrag(e);};top.Children.Add(_summary);
        var icons=new StackPanel{Orientation=Orientation.Horizontal};
        icons.Children.Add(OverlayStyle.Icon("Lock","Lock position · hold Alt for input",()=>{_meter.Preferences.PositionLocked=!_meter.Preferences.PositionLocked;Save();},p,20));
        icons.Children.Add(OverlayStyle.Icon("Play","Start / stop capture",()=>_ = Safe(async()=>{if(_meter.Running)await _meter.Stop();else if(_meter.Preferences.Adapter.Length>0)await _meter.Start(_meter.Preferences.Adapter);else CaptureWindow();}),p,20));
        icons.Children.Add(OverlayStyle.Icon("History","Fight history",()=>Open(ReviewWindows.History(_meter)),p,20));
        icons.Children.Add(OverlayStyle.Icon("Bell","Events and bosses",()=>TogglePanel("Events"),p,20));
        icons.Children.Add(OverlayStyle.Icon("Reset","Reset encounter",()=>_meter.Reset(),p,20));
        icons.Children.Add(OverlayStyle.Icon("Settings","Settings",Settings,p,20));
        icons.Children.Add(OverlayStyle.Icon("Menu","More actions",()=>_actions.Open(this),p,20));
        Grid.SetColumn(icons,1);top.Children.Add(icons);_root.Children.Add(top);
        _health.Children.Add(new Border{Background=Brush.Parse("#10271C")});_health.Children.Add(_healthFill);
        _hpPercent.Margin=new(4,0);_health.Children.Add(_hpPercent);
        _boss.FontSize=8;_boss.TextWrapping=TextWrapping.NoWrap;_boss.HorizontalAlignment=HorizontalAlignment.Right;_boss.Margin=new(4,0);_health.Children.Add(_boss);
        _hudBody.Children.Add(_health);_hudBody.Children.Add(new ScrollViewer{Content=_players,MaxHeight=174,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled});_root.Children.Add(_hudBody);
        var footer=new Grid{Height=20,Margin=new(7,1,5,0),ColumnDefinitions=new("Auto,Auto,*,Auto,Auto,Auto")};
        footer.Children.Add(_captureDot);
        Grid.SetColumn(_title,1);footer.Children.Add(_title);_status.FontSize=9;_status.Foreground=OverlayStyle.Muted;_status.TextWrapping=TextWrapping.NoWrap;_status.TextTrimming=TextTrimming.CharacterEllipsis;_status.Margin=new(5,0);_status.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(_status,2);footer.Children.Add(_status);
        var target=OverlayStyle.Text("TARGET",8,OverlayStyle.Muted);target.Margin=new(4,0);Grid.SetColumn(target,3);footer.Children.Add(target);
        var mode=new Button{Content=_modeText,Padding=new(4,0),Margin=new(2,0),Height=15,MinHeight=0,Background=Brush.Parse("#24251E"),BorderBrush=OverlayStyle.Line,BorderThickness=new(1),VerticalAlignment=VerticalAlignment.Center};mode.Click+=(_,_)=>{_healingView=!_healingView;_lastRender=0;};Grid.SetColumn(mode,4);footer.Children.Add(mode);
        var more=OverlayStyle.Icon("Menu","More actions",()=>_actions.Open(this),p,20);Grid.SetColumn(more,5);footer.Children.Add(more);_root.Children.Add(footer);_hudFooter=footer;
        _scaled.Child=_root;_shell=OverlayStyle.Shell(_scaled,p);_shell.CornerRadius=new(6);Content=_shell;
    }
    private void FitOverlay(Window window,Control content,int baseWidth)
    {
        if(content is LayoutTransformControl transform&&transform.Child is {} child)child.Width=baseWidth-6;
        if(!_fitPending.Add(window))return;
        // Child changes invalidate measure asynchronously. Fit after the layout pass,
        // otherwise the previous row count becomes the new window size and clips the HUD.
        Dispatcher.UIThread.Post(()=>{
            _fitPending.Remove(window);if(_closing||!window.IsVisible)return;
            double scale=window==this?_meter.Preferences.UiScale:window.Title=="FooMeter · Events"?_meter.Preferences.EventOverlayScale??_meter.Preferences.UiScale:_meter.Preferences.PetOverlayScale;
            int width=(int)Math.Ceiling(baseWidth*scale);
            double desired;
            if(content is LayoutTransformControl layout&&layout.Child is {} root){root.InvalidateMeasure();root.Measure(new Size(baseWidth-6,double.PositiveInfinity));desired=root.DesiredSize.Height*scale;}
            else desired=content.DesiredSize.Height;
            int height=(int)Math.Ceiling(desired+6);
            if(window.Width!=width){window.MinWidth=1;window.MaxWidth=double.PositiveInfinity;window.Width=width;window.MinWidth=width;window.MaxWidth=width;}
            if(window.Height!=height)window.Height=height;
            HyprlandOverlay.Fit(window,width,height);
        },DispatcherPriority.Loaded);
    }
    private void RenderHud(MeterView view)
    {
        var p=_meter.Preferences;var fight=view.Fight;
        _hudFooter.IsVisible=!p.CombatStrip;_hudBody.IsVisible=!p.CombatStrip;
        _summary.Text=p.CombatStrip?$"YOU {OverlayStyle.Number(fight.Players.FirstOrDefault(x=>x.IsSelf)?.Dps??0)}/s · PARTY {OverlayStyle.Number(fight.PartyDps)}/s":(fight.TargetName??"FOOMETER · WAITING FOR COMBAT").ToUpperInvariant();ToolTip.SetTip(_summary,"Drag to move · "+fight.TargetName);
        _captureDot.Fill=Brush.Parse(Program.Demo?"#EEAE67":_meter.Running?view.Diagnostics.SelfKnown?"#45D9B1":"#EEAE67":"#CD7B87");
        _title.Text=OverlayStyle.Clock(fight.DurationSeconds);_modeText.Text=_healingView?"HPS":"DPS";
        _health.IsVisible=!p.CombatStrip;_players.IsVisible=!p.CombatStrip;
        var hp=fight.Boss??fight.TargetHealth;_healthFill.Width=(380-18)*Math.Clamp(hp?.HealthFraction??0,0,1);
        _hpPercent.Text=hp?.HealthFraction is {} fraction?$"{fraction:P0}":"HP unknown";
        _boss.Text=hp is null?"— / —":$"{(hp.CurrentHp is {} current?OverlayStyle.Number(current):"—")} / {(hp.MaxHp is {} maximum?OverlayStyle.Number(maximum):"—")}";
        int hash=HashCode.Combine(fight.TotalDamage,fight.TotalHealing,(int)fight.DurationSeconds,_healingView,p.HideNames,p.VisiblePlayers,p.AccentColor,fight.Players.Count);
        if(hash==_lastRender)return;_lastRender=hash;_players.Children.Clear();
        var players=(_healingView?fight.Players.OrderByDescending(x=>x.Healing):fight.Players.AsEnumerable()).Take(p.VisiblePlayers).ToArray();
        if(players.Length==0){_players.Children.Add(new Border{Height=22,Child=OverlayStyle.Text("Start capture before entering your character",9,OverlayStyle.Muted)});return;}
        double max=Math.Max(1,_healingView?players.Max(x=>x.Healing):players.Max(x=>x.Damage));int rank=0;
        foreach(var player in players){rank++;double amount=_healingView?player.Healing:player.Damage;
            var row=new Grid{Height=22,ClipToBounds=true};
            row.Children.Add(new Border{Background=Brush.Parse("#111B2D"),CornerRadius=new(2)});
            row.Children.Add(new Border{HorizontalAlignment=HorizontalAlignment.Left,Width=(380-18)*amount/max,Background=new LinearGradientBrush{StartPoint=RelativePoint.TopLeft,EndPoint=new RelativePoint(1,0,RelativeUnit.Relative),GradientStops=new(){new(Color.Parse("#2257B3"),0),new(Color.Parse("#153161"),1)}},CornerRadius=new(2)});
            var line=new Grid{ColumnDefinitions=new("16,20,*,94,42"),Margin=new(5,0,4,0)};
            line.Children.Add(OverlayStyle.Text(rank.ToString(),10));
            var icon=Ui.Picture("Classes/"+player.ClassName+".png",15);if(icon is not null){icon.Margin=new(0);Grid.SetColumn(icon,1);line.Children.Add(icon);}
            var name=OverlayStyle.Text(p.HideNames&&!player.IsSelf?player.ClassName??"Player":player.Name,11,player.IsSelf?Brush.Parse("#E8C778"):OverlayStyle.Ink);name.Margin=new(3,0,0,0);Grid.SetColumn(name,2);line.Children.Add(name);
            var rate=OverlayStyle.Text($"{(_healingView?player.Hps(fight.DurationSeconds):player.Dps):N0}/s",12,Brush.Parse("#F6D351"));rate.FontFamily=new FontFamily("DejaVu Sans Mono");rate.FontWeight=FontWeight.SemiBold;rate.TextAlignment=TextAlignment.Right;Grid.SetColumn(rate,3);line.Children.Add(rate);
            var share=OverlayStyle.Text($"{(_healingView?fight.TotalHealing>0?(double)player.Healing/fight.TotalHealing:0:fight.TotalDamage>0?player.Share:0):P1}",8);share.TextAlignment=TextAlignment.Right;Grid.SetColumn(share,4);line.Children.Add(share);
            row.Children.Add(line);row.PointerReleased+=(_,_)=>Open(ReviewWindows.Breakdown(fight,player));
            var rating=view.Ratings.GetValueOrDefault(player.ActorId);ToolTip.SetTip(row,$"{player.Name} · {player.ClassName}\nDamage {player.Damage:N0} · Healing {player.Healing:N0}\nGS {rating?.GearScore?.ToString()??"?"} · CP {rating?.CombatPower?.ToString()??"?"}");_players.Children.Add(row);
        }
    }
    private static MenuItem Item(string label,Action? action,params MenuItem[] children)
    {var item=new MenuItem{Header=label};if(children.Length>0)item.ItemsSource=children;if(action is not null)item.Click+=(_,_)=>action();return item;}
    private void Open(Window window){_children.Add(window);window.Closed+=(_,_)=>_children.Remove(window);window.Show(this);}
    private async Task Safe(Func<Task> action) {try{await action();}catch(Exception e){_status.Text=e.Message;var w=Ui.Window("FooMeter · Action failed",Ui.Scroll(Ui.Text(e.Message)),480,240);Open(w);}}
    private void Save(){_meter.SavePreferences(_meter.Preferences);ApplySettings();}
    private void ApplySettings()
    {
        if(_closing)return;
        var p=_meter.Preferences;Opacity=p.OverlayOpacity;if(_shell is not null)_shell.Background=Brush.Parse(p.PanelColor);_title.Foreground=Brush.Parse(p.LogoFollowsAccent?p.AccentColor:p.LogoColor);
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
        var p=_meter.Preferences;bool pets=name=="Pets";
        var rows=new StackPanel{Spacing=pets?4:2};var status=OverlayStyle.Text("",9,OverlayStyle.Muted);status.TextWrapping=TextWrapping.Wrap;status.MaxHeight=pets?40:15;
        var panel=new StackPanel{Spacing=0};
        var window=new Window{Title="FooMeter · "+name,ShowInTaskbar=false};OverlayStyle.Configure(window,pets?350:300);
        var header=new Grid{Height=pets?36:30,Margin=new(8,0,5,0),ColumnDefinitions=new("Auto,*,Auto")};
        var grip=OverlayStyle.Icon("Grip","Drag to move",()=>{},p,28);
        grip.PointerPressed+=(_,e)=>{var pref=_meter.Preferences;if((!(pets?pref.PetOverlayLocked:pref.EventOverlayLocked)||e.KeyModifiers.HasFlag(KeyModifiers.Alt))&&e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)window.BeginMoveDrag(e);};header.Children.Add(grip);
        var label=OverlayStyle.Text(pets?"Pet souls":"Events & bosses",11);label.Margin=new(7,0,0,0);Grid.SetColumn(label,1);header.Children.Add(label);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};actions.Children.Add(OverlayStyle.Icon("Settings","Overlay settings",Settings,p));actions.Children.Add(OverlayStyle.Icon("Close","Hide overlay",()=>window.Close(),p));Grid.SetColumn(actions,2);header.Children.Add(actions);panel.Children.Add(header);
        if(pets){
            _petSearch=new TextBox{Watermark="Search pets…",Text=_petSearch.Text,FontSize=10,Height=27,MinHeight=0,Padding=new(8,4),Margin=new(10,0,10,6)};panel.Children.Add(_petSearch);
            var filters=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,Margin=new(11,0,10,6)};
            var pinned=new CheckBox{Content="Pinned only",FontSize=9,MinHeight=0,Height=20,IsChecked=p.PetOnlyPinned};pinned.IsCheckedChanged+=(_,_)=>{_meter.Preferences.PetOnlyPinned=pinned.IsChecked==true;Save();};filters.Children.Add(pinned);
            var complete=new CheckBox{Content="Hide maxed",FontSize=9,MinHeight=0,Height=20,IsChecked=p.PetHideCompleted};complete.IsCheckedChanged+=(_,_)=>{_meter.Preferences.PetHideCompleted=complete.IsChecked==true;Save();};filters.Children.Add(complete);panel.Children.Add(filters);
            var counters=Ui.Button("Use counters only · hide this window",()=>{window.Close();if(_loot.Region.Length==0)LinuxMenu();else StartLoot();});counters.FontSize=10;counters.MinHeight=0;counters.Height=23;counters.Padding=new(6,2);counters.Margin=new(10,0,10,7);panel.Children.Add(counters);
        }
        var scroll=new ScrollViewer{Content=rows,MaxHeight=pets?235:300,Margin=new(pets?10:8,0,pets?10:8,3),HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};panel.Children.Add(scroll);
        var footer=new Grid{ColumnDefinitions=new("*,25"),Margin=new(10,4,5,5)};footer.Children.Add(status);
        var locked=OverlayStyle.Icon("Lock","Lock position",()=>{if(pets)_meter.Preferences.PetOverlayLocked=!_meter.Preferences.PetOverlayLocked;else _meter.Preferences.EventOverlayLocked=!_meter.Preferences.EventOverlayLocked;Save();},p,25);Grid.SetColumn(locked,1);footer.Children.Add(locked);
        panel.Children.Add(new Border{BorderBrush=OverlayStyle.Line,BorderThickness=new(0,1,0,0),Child=footer});
        var scaled=new LayoutTransformControl{Child=panel};window.Content=OverlayStyle.Shell(scaled,p);_overlayLayouts[window]=scaled;
        double scale=pets?p.PetOverlayScale:p.EventOverlayScale??p.UiScale;scaled.LayoutTransform=new ScaleTransform(scale,scale);
        double? x=pets?p.PetOverlayLeft:p.EventOverlayLeft,y=pets?p.PetOverlayTop:p.EventOverlayTop;
        if(x is {} px&&y is {} py)window.Position=new((int)px,(int)py);
        window.PositionChanged+=(_,_)=>{var pref=_meter.Preferences;if(pets){pref.PetOverlayLeft=window.Position.X;pref.PetOverlayTop=window.Position.Y;}else{pref.EventOverlayLeft=window.Position.X;pref.EventOverlayTop=window.Position.Y;}};
        window.Closed+=(_,_)=>{_petHash=0;_panels.Remove(name);_panelRows.Remove(name);_panelStatus.Remove(name);_overlayLayouts.Remove(window);HyprlandOverlay.Forget(window);if(!_closing){if(pets)_meter.Preferences.ShowPetOverlay=false;else _meter.Preferences.ShowEventOverlay=false;}};
        window.Opened+=(_,_)=>{Paint();FitOverlay(window,scaled,pets?350:300);};
        _panels[name]=window;_panelRows[name]=rows;_panelStatus[name]=status;if(pets)p.ShowPetOverlay=true;else p.ShowEventOverlay=true;Open(window);
    }
    private Control TimerRow(string icon,string name,string value)
    {
        var line=new Grid{ColumnDefinitions=new("18,*,64"),Margin=new(6,0)};
        line.Children.Add(OverlayStyle.Symbol(icon,11,OverlayStyle.Accent(_meter.Preferences)));
        var title=OverlayStyle.Text(name,10.5);title.Margin=new(2,0,4,0);Grid.SetColumn(title,1);line.Children.Add(title);
        var remaining=OverlayStyle.Text(value,11,OverlayStyle.Accent(_meter.Preferences));remaining.FontWeight=FontWeight.SemiBold;remaining.TextAlignment=TextAlignment.Right;Grid.SetColumn(remaining,2);line.Children.Add(remaining);
        return new Border{Height=24,CornerRadius=new(5),Background=OverlayStyle.Row,Child=line};
    }
    private Control PetRow(AscendAPP.Core.Pets.PetProgress pet)
    {
        var p=_meter.Preferences;var grid=new Grid{ColumnDefinitions=new("42,*,76,25"),RowDefinitions=new("19,16,2")};
        var picture=Ui.Picture("Pets/"+pet.Id+".png",34);if(picture is not null){picture.Margin=new(0);picture.HorizontalAlignment=HorizontalAlignment.Left;Grid.SetRowSpan(picture,2);grid.Children.Add(picture);}
        var name=OverlayStyle.Text(pet.Name,11);name.FontWeight=FontWeight.Medium;Grid.SetColumn(name,1);grid.Children.Add(name);
        var count=OverlayStyle.Text(pet.Level is null?"— / —":pet.Progress,14,OverlayStyle.Accent(p));count.TextAlignment=TextAlignment.Right;count.FontWeight=FontWeight.SemiBold;Grid.SetColumn(count,2);grid.Children.Add(count);
        var caption=OverlayStyle.Text(pet.Level is null?"Total unknown · waiting for sync":$"Lv {pet.Level} · {pet.Left}",9,OverlayStyle.Muted);Grid.SetColumn(caption,1);Grid.SetColumnSpan(caption,2);Grid.SetRow(caption,1);grid.Children.Add(caption);
        var pin=OverlayStyle.Icon("Pin",p.PinnedPets.Contains(pet.Id)?"Unpin pet":"Pin pet",()=>{if(!p.PinnedPets.Remove(pet.Id))p.PinnedPets.Add(pet.Id);_petHash=0;Save();},p,24);pin.Opacity=p.PinnedPets.Contains(pet.Id)?1:.4;Grid.SetColumn(pin,3);Grid.SetRowSpan(pin,2);grid.Children.Add(pin);
        var bar=new Grid{Height=2,Margin=new(0,1,0,0)};bar.Children.Add(new Border{Background=OverlayStyle.Line});bar.Children.Add(new Border{Background=OverlayStyle.Accent(p),Width=pet.Fraction*230,HorizontalAlignment=HorizontalAlignment.Left});Grid.SetColumn(bar,1);Grid.SetColumnSpan(bar,2);Grid.SetRow(bar,2);grid.Children.Add(bar);
        return new Border{Background=OverlayStyle.Row,CornerRadius=new(5),Padding=new(8,6),Child=grid};
    }
    private void Paint()
    {
        var view=_meter.View();_view=view;var f=view.Fight;var p=_meter.Preferences;var now=DateTimeOffset.UtcNow;
        _status.Text=Program.Demo?"DEMO":_meter.Running?view.Diagnostics.SelfKnown?"Monitoring":"Waiting for identity":"Capture stopped";ToolTip.SetTip(_status,view.Status);
        if(_captureStatus is not null)_captureStatus.Text=view.Status+"\n"+view.Diagnostics.Summary;
        RenderHud(view);
        Opacity=p.FadeIdle&&!f.IsActive&&f.Players.Count>0&&(now-f.StartedAt.AddSeconds(f.DurationSeconds)).TotalSeconds>=p.FadeAfterSeconds?p.FadedOpacity:p.OverlayOpacity;
        if(p.GameTimerAlerts)foreach(var e in view.Events)foreach(var lead in new EventReminder{LeadMinutes=p.GameTimerLeadMinutes}.Leads()) {
            var left=e.CountdownAt-now;if(left.TotalSeconds>=lead*60&&left.TotalSeconds<lead*60+2)NotifyOnce(e.Event.Id+e.CountdownAt.UtcTicks+":"+lead,e.Event.Name+" in "+lead+" minutes");
        }
        foreach(var pair in _panels.ToArray()){
            var name=pair.Key;var window=pair.Value;
            if(window.Content is Border shell)shell.Background=Brush.Parse(p.PanelColor);
            if(window.Content is Border border && border.Child is LayoutTransformControl scale) {
                double value=name=="Pets"?p.PetOverlayScale:p.EventOverlayScale??p.UiScale;
                if(scale.LayoutTransform is not ScaleTransform existing||existing.ScaleX!=value)scale.LayoutTransform=new ScaleTransform(value,value);
            }var rows=_panelRows[name];if(name=="Events")rows.Children.Clear();
            if(name=="Events") {
                window.Opacity=p.EventOverlayOpacity??p.OverlayOpacity;
                if(p.EventVisibility=="Outside fights"&&f.IsActive){window.Hide();continue;}if(!window.IsVisible)window.Show();
                _panelStatus[name].Text="UTC schedule · local "+now.ToLocalTime().ToString(p.Clock24?"HH:mm":"hh:mm tt");
                rows.Children.Add(OverlayStyle.Text("EVENT SCHEDULE",9,OverlayStyle.Muted));
                foreach(var e in view.Events.Where(e=>e.Event.Id.Contains("shugo",StringComparison.OrdinalIgnoreCase)?p.ShowShugoTimer:e.Event.Id.Contains("rift",StringComparison.OrdinalIgnoreCase)?p.ShowRiftTimer:p.ShowInvasionTimer)) {
                    var left=e.CountdownAt-now;rows.Children.Add(TimerRow(e.Event.Id.Contains("rift",StringComparison.OrdinalIgnoreCase)?"Portal":"Spark",e.Event.Name+(e.Event.Possible?"?":""),OverlayStyle.Clock(left.TotalSeconds)));
                }
                if(p.ShowFieldBossTimers){var heading=OverlayStyle.Text("FIELD BOSSES · VERTERON",9,OverlayStyle.Muted);heading.Margin=new(2,5,0,2);rows.Children.Add(heading);foreach(var boss in view.Bosses)rows.Children.Add(TimerRow("Combat",boss.Boss.Name,boss.Observation is null?"—":boss.Observation.Alive?"Alive":boss.Due is {} at?OverlayStyle.Clock((at-now).TotalSeconds):"—"));}
                foreach(var reminder in p.Reminders){var next=ReminderScheduler.Next(reminder,now);if(next is {} at)rows.Children.Add(TimerRow("Bell",reminder.Name,OverlayStyle.Clock((at-now).TotalSeconds)));}
            } else {
                window.Opacity=p.PetOverlayOpacity;_panelStatus[name].Text=view.PetStatus;
                int petHash=HashCode.Combine(view.PetStatus,_petSearch.Text,p.PetOnlyPinned,p.PetHideCompleted,string.Join(',',p.PinnedPets),string.Join(';',view.Pets.Select(x=>$"{x.Id}:{x.Level}:{x.Points}:{x.Confirmed}")));
                if(petHash==_petHash)continue;_petHash=petHash;rows.Children.Clear();
                foreach(var pet in view.Pets.Where(x=>(!p.PetOnlyPinned||p.PinnedPets.Contains(x.Id))&&(!p.PetHideCompleted||!x.Complete)&&x.Name.Contains(_petSearch.Text??"",StringComparison.OrdinalIgnoreCase))) {
                    rows.Children.Add(PetRow(pet));
                }
            }
        }
        foreach(var alert in _reminders.Poll(p.Reminders,now))NotifyOnce(alert.Id+":"+alert.Due.UtcTicks,alert.Name+" in "+alert.LeadMinutes+" minutes");
        var pickup=_meter.Pickup;
        if(p.ShowPetPickups&&pickup is {} gain&&now-gain.At<TimeSpan.FromSeconds(8)) {
            ShowPickup($"{gain.Name} +{gain.Amount} souls\n"+(gain.Confirmed?$"Level {gain.Level} · {gain.Points} points":"Total unknown · waiting for collection sync"));
        } else if(_pickupWindow is not null){_pickupWindow.Close();_pickupWindow=null;}
        if(_lootStatus is not null)_lootStatus.Text=_loot.Status;
        if(_lootWindow is not null)UpdateLoot();
        FitOverlay(this,_scaled,380);
        foreach(var pair in _overlayLayouts.ToArray())if(pair.Key.IsVisible)FitOverlay(pair.Key,pair.Value,pair.Key.Title=="FooMeter · Events"?300:350);
    }
    private void ShowPickup(string text)
    {
        if(_pickupWindow is null){
            _pickupText=OverlayStyle.Text(text,11,OverlayStyle.Accent(_meter.Preferences));_pickupText.TextWrapping=TextWrapping.Wrap;_pickupText.MaxLines=2;
            var window=new Window{Title="FooMeter · Soul pickup",ShowInTaskbar=false};OverlayStyle.Configure(window,300);window.Height=54;
            window.Content=OverlayStyle.Shell(new Border{Padding=new(8,5),Child=_pickupText},_meter.Preferences);
            window.Position=new((int)(_meter.Preferences.PetPickupLeft??Position.X),(int)(_meter.Preferences.PetPickupTop??Position.Y+Bounds.Height+8));
            window.PointerPressed+=(_,e)=>{if(e.KeyModifiers.HasFlag(KeyModifiers.Alt)&&e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)window.BeginMoveDrag(e);};
            window.PositionChanged+=(_,_)=>{_meter.Preferences.PetPickupLeft=window.Position.X;_meter.Preferences.PetPickupTop=window.Position.Y;};
            window.Closed+=(_,_)=>{_pickupWindow=null;HyprlandOverlay.Forget(window);};_pickupWindow=window;Open(window);
        }
        _pickupText.Text=text;HyprlandOverlay.Fit(_pickupWindow,300,54);
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
        if(!w.IsVisible)w.Show();_lootInput.Apply(w,true);HyprlandOverlay.Fit(w,160,(int)w.Height);
    }
    private void StartLoot()
    {
        _loot.Start();
        if(_lootWindow is not null)return;
        _lootWindow=new Window{Title="FooMeter · Loot counters",Width=160,Height=500,SystemDecorations=SystemDecorations.None,CanResize=false,Topmost=true,ShowInTaskbar=false,ShowActivated=false,Background=Brushes.Transparent,TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent},Content=_lootCanvas};
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
            FitOverlay(this,_scaled,380);await Task.Delay(120);
            if(Math.Abs(Bounds.Width-380)>1||Bounds.Height>116)throw new Exception($"Combat HUD is oversized: {Bounds.Size}");
            _meter.Preferences.VisiblePlayers=1;_lastRender=0;Paint();await Task.Delay(120);
            double oneRowHeight=Bounds.Height;
            if(oneRowHeight>90)throw new Exception($"One-player HUD is oversized: {Bounds.Size}");
            using(var shot=new RenderTargetBitmap(new(380,(int)Bounds.Height))){shot.Render(this);shot.Save(Path.Combine(Path.GetDirectoryName(path)!,"FooMeterSingleRow.png"));}
            _meter.Preferences.CombatStrip=true;Paint();await Task.Delay(120);if(Bounds.Height>40)throw new Exception($"Strip is oversized: {Bounds.Size}");
            _meter.Preferences.CombatStrip=false;_meter.Preferences.VisiblePlayers=6;_lastRender=0;Paint();await Task.Delay(120);
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
            for(int attempt=0;attempt<20&&(_panels["Events"].Bounds.Height<200||_panels["Pets"].Bounds.Height<300);attempt++){Paint();await Task.Delay(100);}
            var historyList=history.GetVisualDescendants().OfType<ListBox>().First();historyList.SelectedIndex=0;historyList.SelectedItems!.Add(historyList.Items[1]);
            if(!history.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text?.Contains("DPS change:")==true))throw new Exception("History comparison did not update");
            if(_panels["Events"].Bounds.Width>301||_panels["Events"].Bounds.Height is <200 or >280||_panels["Pets"].Bounds.Width>351||_panels["Pets"].Bounds.Height is <300 or >460)throw new Exception($"Detached overlay size exceeded its compact bounds: Events={_panels["Events"].Bounds.Size}/{_panels["Events"].IsVisible}, Pets={_panels["Pets"].Bounds.Size}/{_panels["Pets"].IsVisible}, layouts={_overlayLayouts.Count}");
            foreach(var (key,panelWindow) in _panels){var heading=panelWindow.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text==(key=="Pets"?"Pet souls":"Events & bosses"));var position=heading.TranslatePoint(default,panelWindow);if(position is null||position.Value.Y<0||position.Value.Y+heading.Bounds.Height>panelWindow.Bounds.Height)throw new Exception("Overlay header is clipped");}
            if(settings.EditorCount<40||_panelRows["Pets"].Children.Count!=207||_panelRows["Events"].Children.Count<2)throw new Exception("Menus did not populate");
            _timer.Stop(); // Keep the synthetic pickup fixture independent of the live pickup expiry poll.
            ShowPickup("DEMO soul pickup +1\nTotal unknown");await Task.Delay(100);
            if(_pickupWindow is null||_pickupWindow.Bounds.Height>54||_pickupWindow.ShowActivated)throw new Exception($"Pickup overlay state: {_pickupWindow?.Bounds.Size}, activates={_pickupWindow?.ShowActivated}");
            _pickupWindow.Close();
            ShowPickup("DEMO second pickup +1\nTotal unknown");await Task.Delay(50);_pickupWindow!.Close();
            foreach(var child in _children.Where(c=>c.IsVisible).ToArray()){
                using var shot=new RenderTargetBitmap(new(Math.Max(1,(int)child.Bounds.Width),Math.Max(1,(int)child.Bounds.Height)));shot.Render(child);
                string suffix=string.Concat(child.Title!.Where(char.IsLetterOrDigit));shot.Save(Path.Combine(Path.GetDirectoryName(path)!,suffix+".png"));
            }
            using var image=new RenderTargetBitmap(new((int)Bounds.Width,(int)Bounds.Height));image.Render(this);image.Save(Path.ChangeExtension(path,".png"));
            File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=true,Players=_players.Children.Count,Settings=settings.EditorCount,Pets=_panelRows["Pets"].Children.Count,Windows=_children.Count,CombatSize=Bounds.Size.ToString(),SingleRowHeight=oneRowHeight,EventsSize=_panels["Events"].Bounds.Size.ToString(),PetsSize=_panels["Pets"].Bounds.Size.ToString()}));
        }catch(Exception e){File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=false,Error=e.ToString()}));Environment.ExitCode=1;}Close();
    }
}
