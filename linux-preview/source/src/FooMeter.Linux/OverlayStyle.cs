using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using AscendAPP.Core.Settings;

namespace FooMeter.Linux;

internal static class OverlayStyle
{
    public static readonly SolidColorBrush Ink=new(Color.Parse("#F3EEF2")),Muted=new(Color.Parse("#A59AA7")),Line=new(Color.Parse("#383039")),Row=new(Color.Parse("#171319"));
    public static IBrush Accent(MeterPreferences p)=>Brush.Parse(p.AccentColor);
    public static TextBlock Text(string text,double size=10,IBrush? color=null)=>new(){Text=text,FontSize=size,Foreground=color??Ink,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};
    public static Button Icon(string kind,string hint,Action action,MeterPreferences p,int width=26)
    {var b=new Button{Width=width,Height=26,MinHeight=0,MinWidth=0,Padding=new(3),Background=Brushes.Transparent,BorderThickness=new(0),Content=Symbol(kind,13,kind=="Play"?Brush.Parse("#45D9B1"):Muted),VerticalContentAlignment=VerticalAlignment.Center,HorizontalContentAlignment=HorizontalAlignment.Center};ToolTip.SetTip(b,hint);b.Click+=(_,_)=>action();return b;}
    public static Control Symbol(string kind,int size,IBrush brush)
    {
        if(!Paths.TryGetValue(kind,out string? data))data=Paths["Menu"];
        var path=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(data),Stroke=brush,StrokeThickness=1.35,Width=20,Height=20};
        if(kind=="Flame"){path.Fill=brush;path.StrokeThickness=0;}
        return new Viewbox{Width=size,Height=size,Child=path,Stretch=Stretch.Uniform};
    }
    public static Border Shell(Control child,MeterPreferences p)=>new(){Margin=new(2),Padding=new(0),Background=Brush.Parse(p.PanelColor),BorderBrush=Line,BorderThickness=new(1),CornerRadius=new(10),Child=child,ClipToBounds=true};
    public static void Configure(Window window,int width)
    {window.Width=width;window.MinWidth=width;window.MaxWidth=width;window.MinHeight=1;window.Height=80;window.SizeToContent=SizeToContent.Manual;window.CanResize=false;window.SystemDecorations=SystemDecorations.None;window.ShowActivated=false;window.Background=Brushes.Transparent;window.TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};window.Topmost=true;}
    public static string Number(double value)=>value>=1_000_000?$"{value/1_000_000:0.00}M":value>=1000?$"{value/1000:0.0}K":$"{value:0}";
    public static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,Math.Min(86399,seconds))).ToString(@"mm\:ss");
    public static string Time(double? seconds)=>seconds is {} s?Clock(s):"—";
    private static readonly Dictionary<string,string> Paths=new()
    {
        ["Play"]="M6,3 L17,10 6,17 Z",["History"]="M3,9 A7,7 0 1 1 4,15 M3,3 L3,9 9,9 M10,6 L10,10 13,12",["Reset"]="M16,7 A7,7 0 1 0 17,11 M16,2 L16,7 11,7",
        ["Strip"]="M2,6 L18,6 18,14 2,14 Z M5,9 L8,9 M11,9 L15,9",
        ["Grip"]="M6,4 L7,4 M13,4 L14,4 M6,10 L7,10 M13,10 L14,10 M6,16 L7,16 M13,16 L14,16",
        ["Settings"]="M10,2 L12,2 13,5 16,5 18,8 16,10 18,12 16,15 13,15 12,18 8,18 7,15 4,15 2,12 4,10 2,8 4,5 7,5 8,2 Z M13,10 A3,3 0 1 1 7,10 A3,3 0 1 1 13,10",
        ["Menu"]="M3,4 L17,4 M3,10 L17,10 M3,16 L17,16",
        ["Close"]="M4,4 L16,16 M16,4 L4,16",["Minus"]="M4,10 L16,10",
        ["Lock"]="M4,9 L16,9 16,18 4,18 Z M6,9 L6,6 C6,0 14,0 14,6 L14,9",
        ["Unlock"]="M4,9 L16,9 16,18 4,18 Z M6,9 L6,6 C6,0 14,0 14,6",
        ["Combat"]="M3,3 L16,16 M13,17 L17,13 M4,8 L3,3 8,4 M17,3 L4,16 M3,13 L7,17 M12,4 L17,3 16,8",
        ["Heal"]="M8,3 L12,3 12,8 17,8 17,12 12,12 12,17 8,17 8,12 3,12 3,8 8,8 Z",
        ["Bell"]="M4,14 L6,11 6,7 C6,2 14,2 14,7 L14,11 16,14 Z M8,17 C9,19 11,19 12,17",
        ["Pet"]="M7,11 C2,11 2,17 6,17 C8,17 9,15 10,15 C12,15 13,18 16,16 C19,13 14,8 12,10 C11,11 9,10 7,11 Z M5,5 A1.5,2 0 1 1 4.9,5 M10,2 A1.5,2 0 1 1 9.9,2 M15,5 A1.5,2 0 1 1 14.9,5",
        ["Pin"]="M7,2 L13,2 12,7 16,11 16,12 4,12 4,11 8,7 Z M10,12 L10,19",
        ["Flame"]="M11,0.7 C12,5.5 17.6,6.3 17.6,12.5 C17.6,17.1 14.1,19.3 10,19.3 C5.8,19.3 2.5,16.8 2.5,12.7 C2.5,9.8 4.3,7.3 6.3,5.5 C6.2,8 7.8,9.5 8.8,10 C7.8,5.7 11.2,4 11,0.7 Z",
        ["Debug"]="M6,7 L14,7 14,16 C14,20 6,20 6,16 Z M7,4 C7,1 13,1 13,4 L13,7 M2,8 L6,10 M14,10 L18,8 M2,14 L6,14 M14,14 L18,14",
        ["Portal"]="M13,2 C3,-1 0,18 9,18 C19,19 20,2 13,2 Z M11,5 C6,4 4,15 10,15 C15,15 17,6 11,5 Z",
        ["Spark"]="M10,1 L12,7 19,10 12,12 10,19 8,12 1,10 8,7 Z"
    };
}
