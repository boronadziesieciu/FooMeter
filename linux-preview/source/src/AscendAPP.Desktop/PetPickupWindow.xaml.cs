using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AscendAPP.Core.Pets;
namespace AscendAPP.Desktop;
public partial class PetPickupWindow : Window
{
    private readonly MeterController _c;private bool _ready,_allowClose,_demo;private long _sequence=-1,_generation;private DateTimeOffset _until;
    private readonly DispatcherTimer _expire=new(){Interval=TimeSpan.FromSeconds(5.5)};
    internal Services.OverlayInteraction Interaction {get;}
    public PetPickupWindow(MeterController c)
    {
        InitializeComponent();_c=c;
        Interaction=new(this,()=>true,()=>true,c.Touch,!c.Preview);
        Loaded+=(_,_)=>{_ready=true;ApplyPreferences();};
        LocationChanged+=(_,_)=>{if(_ready){c.Preferences.PetPickupLeft=Left;c.Preferences.PetPickupTop=Top;c.QueueSave();}};
        Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;Hide();}};
        _expire.Tick+=(_,_)=>{_expire.Stop();long generation=_generation;var animation=new DoubleAnimation(0,TimeSpan.FromMilliseconds(250));animation.Completed+=(_,_)=>{if(!_allowClose&&generation==_generation)Hide();};BeginAnimation(OpacityProperty,animation);};
        ApplyPreferences();
    }
    public void ApplyPreferences()
    {
        var p=_c.Preferences;Width=310*p.PetOverlayScale;Height=64*p.PetOverlayScale;LayoutRoot.LayoutTransform=new ScaleTransform(p.PetOverlayScale,p.PetOverlayScale);
        Opacity=p.PetOverlayOpacity;Topmost=_c.Overlay?.Topmost??true;Interaction.Refresh();
        if(!_ready)return;var a=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);
        Left=Math.Clamp(p.PetPickupLeft??SystemParameters.WorkArea.Right-Width-40,a.Left,Math.Max(a.Left,a.Right-Width));
        Top=Math.Clamp(p.PetPickupTop??SystemParameters.WorkArea.Bottom-Height-180,a.Top,Math.Max(a.Top,a.Bottom-Height));
    }
    private void Set(PetPickup pickup,bool demo)
    {
        _demo=demo;Portrait.PetId=pickup.Id;PetName.Text=(demo?"DEMO · ":"")+pickup.Name;
        Count.Text=!pickup.Confirmed?"+"+pickup.Amount:pickup.Level==3?"MAX":pickup.Points+"/"+PetProtocol.Required(pickup.Level??0);
        Detail.Text=$"+{pickup.Amount} soul{(pickup.Amount==1?"":"s")} · "+(!pickup.Confirmed?"Total waiting for collection":pickup.Level==3?"Fully collected":$"{PetProtocol.Required(pickup.Level??0)-pickup.Points} left to next level");
    }
    public void Update(bool renderOnly=false)
    {
        Topmost=_c.Overlay?.Topmost??true;
        if(!_c.Preferences.ShowPetPickups){Hide();return;}var now=DateTimeOffset.UtcNow;
        var pickup=_c.Pets.Pickup;if(pickup is null||now-pickup.At>TimeSpan.FromSeconds(6)){if(!_demo||now>_until)Hide();return;}
        if(pickup.Sequence==_sequence)return;_sequence=pickup.Sequence;Set(pickup,false);_until=pickup.At.AddSeconds(6);if(!renderOnly)Reveal();
    }
    public void PreviewCounter(bool renderOnly=false)
    {Set(new(-1,1006,"Sparkie",1,1,14,true,DateTimeOffset.UtcNow),true);_until=DateTimeOffset.UtcNow.AddSeconds(6);if(!renderOnly)Reveal();}
    private void Reveal()
    {
        _generation++;_expire.Stop();BeginAnimation(OpacityProperty,null);ApplyPreferences();Show();
        if(SystemParameters.ClientAreaAnimation&&_c.Preferences.SmoothNumbers)BeginAnimation(OpacityProperty,new DoubleAnimation(0,_c.Preferences.PetOverlayOpacity,TimeSpan.FromMilliseconds(140)){FillBehavior=FillBehavior.Stop});
        _expire.Start();
    }
    private void Drag_Click(object sender,MouseButtonEventArgs e)=>Interaction.TryDrag(e);
    internal void CloseForExit(){if(_allowClose)return;_allowClose=true;_expire.Stop();BeginAnimation(OpacityProperty,null);Close();}
}
