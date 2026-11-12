using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AscendAPP.Desktop.Controls;
using AscendAPP.Desktop.ViewModels;
namespace AscendAPP.Desktop;
public partial class OverlayWindow : Window
{
    internal Services.OverlayInteraction Interaction {get;}
    internal void RefreshInput()=>Interaction.Refresh();
    private readonly UiModel _model;private readonly MeterController? _c;private bool _positionReady;
    public OverlayWindow(UiModel model,MeterController? controller=null)
    {
        InitializeComponent();_model=model;_c=controller;DataContext=model;
        Interaction=new(this,()=>model.Preferences.PositionLocked||model.Preferences.ClickThrough&&(controller?.CanClickThrough??true),()=>model.Preferences.PositionLocked,()=>controller?.Touch(),controller is{Preview:false});Interaction.Changed+=ApplyPreferences;
        model.PropertyChanged+=ModelChanged;
        Loaded+=(_,_)=>{_positionReady=true;Position();ApplyPreferences();};
        MouseEnter+=(_,_)=>{_c?.Touch();ApplyPreferences();};MouseLeave+=(_,_)=>ApplyPreferences();
        LocationChanged+=(_,_)=>{if(_positionReady&&_c is not null){model.Preferences.Left=Left;model.Preferences.Top=Top;_c.QueueSave();}};
        Closing+=(_,_)=>{model.PropertyChanged-=ModelChanged;_c?.Dispose();};
        ApplyPreferences();
    }
    private void ModelChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is nameof(UiModel.HasRows) or nameof(UiModel.Notice) or nameof(UiModel.Mode) or nameof(UiModel.HeaderState) or nameof(UiModel.ForecastIsDummy) or nameof(UiModel.IdentityRefreshPending) or nameof(UiModel.SelfIdentified) or nameof(UiModel.ShowHealing))ApplyPreferences();}
    public void ApplyPreferences()
    {
        var p=_model.Preferences;double uiScale=p.CombatStrip?Math.Min(p.UiScale,Math.Max(.5,SystemParameters.WorkArea.Width/680)):p.UiScale;Width=(p.CombatStrip?680:420)*uiScale;if(LayoutRoot.LayoutTransform is not ScaleTransform scale||scale.ScaleX!=uiScale)LayoutRoot.LayoutTransform=new ScaleTransform(uiScale,uiScale);
        Opacity=_c?.VisualOpacity(IsMouseOver||Interaction.TemporaryInteractive)??p.OverlayOpacity;
        bool strip=p.CombatStrip;StripPanel.Visibility=strip?Visibility.Visible:Visibility.Collapsed;
        Header.Visibility=strip?Visibility.Collapsed:p.FocusMode&&_model.Latest?.IsActive==true&&!IsMouseOver&&!Interaction.TemporaryInteractive?Visibility.Collapsed:Visibility.Visible;
        IdlePanel.Visibility=strip||_model.HasRows?Visibility.Collapsed:Visibility.Visible;CombatPanel.Visibility=!strip&&_model.HasRows?Visibility.Visible:Visibility.Collapsed;
        TargetHeader.Visibility=_model.Latest?.Players.Count>0?Visibility.Visible:Visibility.Collapsed;
        ForecastPanel.Visibility=_model.Latest?.Players.Count>0&&p.ShowBerserk&&!_model.ForecastIsDummy?Visibility.Visible:Visibility.Collapsed;PartyPanel.Visibility=p.ShowPartyDps?Visibility.Visible:Visibility.Collapsed;
        HealingInfo.Visibility=_model.ShowHealing?Visibility.Visible:Visibility.Collapsed;
        HpBar.Visibility=_model.HasBossHealth?Visibility.Visible:Visibility.Collapsed;
        TimerHost.Visibility=strip?Visibility.Collapsed:Visibility.Visible;Footer.Visibility=strip?Visibility.Collapsed:Visibility.Visible;
        NoticePanel.Visibility=strip||string.IsNullOrWhiteSpace(_model.Notice)?Visibility.Collapsed:Visibility.Visible;
        PlayerScroller.MaxHeight=Math.Min(29*p.VisiblePlayers,Math.Max(29,SystemParameters.WorkArea.Height/p.UiScale-195-Math.Min(92,_model.Timers.Count*23)-(string.IsNullOrWhiteSpace(_model.Notice)?0:40)));
        PetsButton.Background=p.ShowPetOverlay?(Brush)FindResource("AccentSurface"):Brushes.Transparent;
        EventsButton.Background=p.ShowEventOverlay?(Brush)FindResource("AccentSurface"):Brushes.Transparent;
        StripLockIcon.Kind=p.PositionLocked?"Lock":"Unlock";
        LockIcon.Kind=p.PositionLocked?"Lock":"Unlock";LockButton.ToolTip=p.PositionLocked?"Locked · hold Alt to interact or drag":"Lock overlay and pass clicks through";
        IdleTitle.Text=_model.HeaderState=="Stopped"?"Capture is stopped":_model.IdentityRefreshPending?"Refreshing character after teleport…":_model.SelfIdentified?"Waiting for boss or dummy combat":"Searching for game traffic…";
        IdleHint.Text=_model.HeaderState=="Stopped"?"Open the menu or settings to start capture":_model.IdentityRefreshPending?"Waiting for the game’s own character record":_model.SelfIdentified?_model.Preferences.CaptureScope+" · bosses and dummies only":"Enter the world or teleport to refresh character";
        RefreshInput();if(_positionReady)Position();
    }
    private void Position()
    {
        var a=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);var primary=SystemParameters.WorkArea;var p=_model.Preferences;
        double x=p.Left??primary.Right-Width-24,y=p.Top??primary.Top+80;
        Left=Math.Clamp(x,a.Left,Math.Max(a.Left,a.Right-Width));Top=Math.Clamp(y,a.Top,Math.Max(a.Top,a.Bottom-Math.Max(80,ActualHeight)));
    }
    private void Drag_Click(object sender,MouseButtonEventArgs e)=>Interaction.TryDrag(e);
    private void Player_Click(object sender,MouseButtonEventArgs e){if((sender as FrameworkElement)?.DataContext is not PlayerRow row)return;if(row.HasCombat)_c?.ShowPlayer(row.ActorId);else _c?.Notice("No combat details for "+row.DisplayName+" yet. Waiting for boss or dummy combat.");}
    private void Damage_Click(object sender,RoutedEventArgs e)=>_model.ShowHealing=false;
    private void Healing_Click(object sender,RoutedEventArgs e)=>_model.ShowHealing=true;
    private void CaptureStatus_Click(object sender,RoutedEventArgs e)=>_c?.OpenCaptureDiagnostics();
    private void Strip_Click(object sender,RoutedEventArgs e){if(_c is not null)_c.ToggleCombatStrip();else{_model.Preferences.CombatStrip=!_model.Preferences.CombatStrip;ApplyPreferences();}}
    private void Settings_Click(object sender,RoutedEventArgs e)=>_c?.ShowSettings();
    private void Events_Click(object sender,RoutedEventArgs e)=>_c?.ToggleEventOverlay();
    private void Pets_Click(object sender,RoutedEventArgs e)=>_c?.TogglePetOverlay();
    private void History_Click(object sender,RoutedEventArgs e)=>_c?.History();
    private void Ratings_Click(object sender,RoutedEventArgs e)=>_c?.ShowSettings("Character");
    private void Tickets_Click(object sender,RoutedEventArgs e)=>_c?.ShowSettings("Tickets");
    private void Copy_Click(object sender,RoutedEventArgs e)=>_c?.CopySummary();
    private void Minimize_Click(object sender,RoutedEventArgs e)=>_c?.Minimize();
    private void Close_Click(object sender,RoutedEventArgs e){if(_c is null)Close();else _c.Quit();}
    private void Lock_Click(object sender,RoutedEventArgs e){_model.Preferences.PositionLocked=!_model.Preferences.PositionLocked;_c?.ApplyPreferences();ApplyPreferences();}
    private void Menu_Click(object sender,RoutedEventArgs e)
    {
        if(_c is null)return;var menu=ActionsMenu();menu.PlacementTarget=sender as Button;menu.IsOpen=true;
    }
    internal ContextMenu ActionsMenu()
    {
        var menu=new ContextMenu();if(_c is null)return menu;
        void Add(string title,string icon,Action action){var item=new MenuItem{Header=title,Icon=new Symbol{Kind=icon,Width=14,Height=14}};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add("Start capture","Play",_c.StartCapture);Add("Stop capture","Stop",_c.Stop);menu.Items.Add(new Separator());
        Add("Copy summary","Copy",_c.CopySummary);Add("Encounter history","History",_c.History);menu.Items.Add(new Separator());
        Add("Reset encounter","Reset",_c.ResetEncounter);Add("Clear current meter","Clear",_c.ClearMeter);Add("Skill breakdown","Combat",_c.ShowSkills);
        Add("Click-through · "+(_model.Preferences.ClickThrough?"on":"off"),"ClickThrough",_c.ToggleClickThrough);menu.Items.Add(new Separator());
        Add("Import session","Import",_c.Import);Add("Export session","Export",_c.ExportJson);Add("Export ranking CSV","Export",_c.ExportCsv);Add("Export diagnostics","Debug",_c.ExportReport);menu.Items.Add(new Separator());
        Add("One-line combat strip","Strip",_c.ToggleCombatStrip);
        Add("Events and boss timers","Bell",_c.ToggleEventOverlay);Add("Pet souls","Pet",_c.TogglePetOverlay);Add("Settings","Settings",()=>_c.ShowSettings());return menu;
    }
}
