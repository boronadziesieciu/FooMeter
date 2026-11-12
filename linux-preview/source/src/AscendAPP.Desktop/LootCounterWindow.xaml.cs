using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AscendAPP.Desktop.Services;
namespace AscendAPP.Desktop;
public partial class LootCounterWindow : Window
{
    private readonly List<TextBlock> _labels=new();private bool _allowClose;
    public LootCounterWindow()
    {
        InitializeComponent();SourceInitialized+=(_,_)=>OverlayInput.SetClickThrough(new WindowInteropHelper(this).Handle,true);
        Closing+=(_,e)=>{if(!_allowClose){e.Cancel=true;Hide();}};
    }
    internal void Apply(LootReadFrame frame,bool renderOnly=false)
    {
        if(frame.Rows.Count==0){Hide();return;}
        if(!renderOnly&&!IsVisible)Show();
        var dpi=VisualTreeHelper.GetDpi(this);double sx=dpi.DpiScaleX,sy=dpi.DpiScaleY;
        double gap=frame.Gap*Math.Clamp(frame.Rows.Average(r=>r.Height)/14,.75,2.5);
        int left=Math.Max(frame.Game.Left,(int)(frame.Crop.Left-gap-130*sx)),top=(int)(frame.Crop.Top-10*sy);
        Width=130;Height=frame.Crop.Height/sy+20;
        if(!renderOnly)SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),left,top,(int)(130*sx),(int)(Height*sy),0x10);
        while(_labels.Count<frame.Rows.Count){var label=new TextBlock{FontFamily=new FontFamily("Segoe UI"),FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Right,Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=3,ShadowDepth=1,Opacity=1}};_labels.Add(label);Labels.Children.Add(label);}
        for(int i=0;i<_labels.Count;i++){
            var label=_labels[i];label.Visibility=i<frame.Rows.Count?Visibility.Visible:Visibility.Collapsed;if(i>=frame.Rows.Count)continue;var row=frame.Rows[i];
            label.Text=row.Text;label.Foreground=row.Fresh?Brushes.White:new SolidColorBrush(Color.FromRgb(226,196,148));label.FontSize=Math.Clamp(row.Height/sy*1.3,13,24);label.Width=110;label.ToolTip=row.Detail;
            // The game already supplies the icon/name. Only draw a number to its left.
            Canvas.SetLeft(label,14);
            Canvas.SetTop(label,10+(row.Y+row.Height/2)/sy-label.FontSize*.65);
        }
    }
    internal void CloseForExit(){if(_allowClose)return;_allowClose=true;Close();}
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
}
