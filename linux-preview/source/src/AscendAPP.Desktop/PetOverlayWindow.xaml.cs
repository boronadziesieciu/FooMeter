using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AscendAPP.Core.Pets;
namespace AscendAPP.Desktop;
public partial class PetOverlayWindow : Window
{
    private readonly MeterController _c;private bool _ready,_positionReady,_allowClose,_applying;private string _signature="",_query="";private long _viewVersion=-1;private DateTimeOffset? _nextExpiry;
    internal Services.OverlayInteraction Interaction {get;}
    public PetOverlayWindow(MeterController c)
    {
        _c=c;InitializeComponent();
        Interaction=new(this,()=>c.Preferences.PetOverlayLocked||c.Preferences.ClickThrough&&c.CanClickThrough,()=>c.Preferences.PetOverlayLocked,c.Touch,!c.Preview);
        Interaction.Changed+=ApplyPreferences;
        Loaded+=(_,_)=>{_positionReady=true;ApplyPreferences();};
        LocationChanged+=(_,_)=>{if(_positionReady){c.Preferences.PetOverlayLeft=Left;c.Preferences.PetOverlayTop=Top;c.QueueSave();}};
        Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;c.SetPetOverlayVisible(false);}};
        _ready=true;ApplyPreferences();Refresh();
    }
    public void ApplyPreferences()
    {
        if(!_ready)return;var p=_c.Preferences;Width=350*p.PetOverlayScale;
        if(LayoutRoot.LayoutTransform is not ScaleTransform s||s.ScaleX!=p.PetOverlayScale)LayoutRoot.LayoutTransform=new ScaleTransform(p.PetOverlayScale,p.PetOverlayScale);
        Opacity=p.PetOverlayOpacity;Topmost=_c.Overlay?.Topmost??true;
        _applying=true;PinnedOnly.IsChecked=p.PetOnlyPinned;HideCompleted.IsChecked=p.PetHideCompleted;_applying=false;
        LockIcon.Kind=p.PetOverlayLocked?"Lock":"Unlock";Interaction.Refresh();
        PetScroller.MaxHeight=Math.Min(235,Math.Max(70,SystemParameters.WorkArea.Height/p.PetOverlayScale-220));
        if(!_positionReady)return;
        var a=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);
        Left=Math.Clamp(p.PetOverlayLeft??SystemParameters.WorkArea.Left+40,a.Left,Math.Max(a.Left,a.Right-Width));
        Top=Math.Clamp(p.PetOverlayTop??SystemParameters.WorkArea.Top+120,a.Top,Math.Max(a.Top,a.Bottom-Math.Max(90,ActualHeight)));
    }
    internal sealed record DisplayPet(PetProgress Value)
    {
        public uint Id=>Value.Id;public string Name=>Value.Name;public string Progress=>Value.Level is null?"SYNC":Value.Progress;
        public string Caption=>Value.Rank+" · "+Value.Left+(Value.Level.HasValue&&!Value.Confirmed?" · saved":"");
        public double Fraction=>Value.Fraction;public Brush PinColor=>Services.AppearanceTheme.Brush(Value.Pinned?"#F28DB8":"#796C7A");
        public string PinHint=>Value.Pinned?"Unpin pet":"Pin pet";public string Detail=>"Pet #"+Id+" · "+Caption+(Value.Confirmed?"\nUpdated from the captured game stream.":"\nEnter the world with capture running to confirm totals.");
    }
    public void Refresh()
    {
        if(!_ready)return;var p=_c.Preferences;var now=DateTimeOffset.UtcNow;var query=string.Join(",",p.PinnedPets)+"|"+p.PetOnlyPinned+"|"+p.PetHideCompleted+"|"+Search.Text.Trim();
        if(_viewVersion!=_c.Pets.ViewRevision||query!=_query||_nextExpiry is {} expiry&&now>=expiry)
        {
        _viewVersion=_c.Pets.ViewRevision;_query=query;var rows=_c.Pets.Rows(p.PinnedPets.ToHashSet(),p.PetOnlyPinned,p.PetHideCompleted,Search.Text.Trim(),now);
        _nextExpiry=!p.PetHideCompleted?null:rows.Where(r=>r.Complete&&r.Recent.HasValue).Select(r=>(DateTimeOffset?)r.Recent!.Value.AddSeconds(6.01)).Order().FirstOrDefault();
        string signature=string.Join("|",rows.Select(x=>$"{x.Id}:{x.Level}:{x.Points}:{x.Pinned}:{x.Confirmed}"));
        if(signature!=_signature){_signature=signature;PetList.ItemsSource=rows.Select(x=>new DisplayPet(x)).ToArray();}
        Empty.Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;
        }
        var toast=_c.Pets.Toast(DateTimeOffset.UtcNow);PickupText.Text=toast;Pickup.Visibility=toast.Length>0?Visibility.Visible:Visibility.Collapsed;
        LiveLabel.Text=_c.Preview?"DEMO":_c.Pets.Trusted?"SYNCED":"NO TOTALS";
        Status.Text=(_c.Preview?"DEMO · synthetic pet progress\n":"")+_c.Pets.Status;
    }
    private void Search_Changed(object sender,TextChangedEventArgs e){if(_ready)Refresh();}
    private void Filter_Changed(object sender,RoutedEventArgs e){if(!_ready||_applying)return;_c.Preferences.PetOnlyPinned=PinnedOnly.IsChecked==true;_c.Preferences.PetHideCompleted=HideCompleted.IsChecked==true;_c.QueueSave();Refresh();}
    private void Pin_Click(object sender,RoutedEventArgs e){if((sender as FrameworkElement)?.DataContext is not DisplayPet row)return;var pins=_c.Preferences.PinnedPets;if(!pins.Remove(row.Id)){if(pins.Count>=128){_c.Notice("Up to 128 pets can be pinned.");return;}pins.Add(row.Id);}_c.QueueSave();Refresh();}
    private void Drag_Click(object sender,MouseButtonEventArgs e)=>Interaction.TryDrag(e);
    private void CounterOnly_Click(object sender,RoutedEventArgs e)=>_c.UsePetCountersOnly();
    private void Diagnostics_Click(object sender,RoutedEventArgs e)=>_c.CopyPetDiagnostics();
    private void Settings_Click(object sender,RoutedEventArgs e)=>_c.ShowSettings("Pet tracking");
    private void Hide_Click(object sender,RoutedEventArgs e)=>_c.SetPetOverlayVisible(false);
    private void Lock_Click(object sender,RoutedEventArgs e){_c.Preferences.PetOverlayLocked=!_c.Preferences.PetOverlayLocked;_c.ApplyPreferences();}
    internal void CloseForExit(){if(_allowClose)return;_allowClose=true;Close();}
}
