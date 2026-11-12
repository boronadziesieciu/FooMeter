using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace AscendAPP.Desktop;
internal sealed class LootRegionWindow : Window
{
    private readonly Canvas _canvas=new();private readonly Rectangle _box=new(){Stroke=Brushes.HotPink,StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(30,255,130,180))};
    private Point? _start;public Rect? Selection {get;private set;}
    public LootRegionWindow(System.Drawing.Rectangle game)
    {
        Title="FooMeter · Choose loot text area";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(70,0,0,0));Topmost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;
        double scale=VisualTreeHelper.GetDpi(this).DpiScaleX;Left=game.Left/scale;Top=game.Top/scale;Width=game.Width/scale;Height=game.Height/scale;Content=_canvas;
        _canvas.Children.Add(new TextBlock{Text="Draw a box around the loot TEXT, with its icons just outside the left edge.  Esc cancels.",Foreground=Brushes.White,Background=Brushes.Black,FontSize=15,Padding=new Thickness(12),Margin=new Thickness(20)});
        _canvas.Children.Add(_box);
        MouseLeftButtonDown+=(_,e)=>{_start=e.GetPosition(_canvas);CaptureMouse();};
        MouseMove+=(_,e)=>{if(_start is {} start){var r=new Rect(start,e.GetPosition(_canvas));Canvas.SetLeft(_box,r.Left);Canvas.SetTop(_box,r.Top);_box.Width=r.Width;_box.Height=r.Height;}};
        MouseLeftButtonUp+=(_,e)=>{if(_start is not {} start)return;ReleaseMouseCapture();var r=new Rect(start,e.GetPosition(_canvas));if(r.Width<40||r.Height<40)return;Selection=new(r.X/ActualWidth,r.Y/ActualHeight,r.Width/ActualWidth,r.Height/ActualHeight);DialogResult=true;Close();};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){DialogResult=false;Close();}};
    }
}
