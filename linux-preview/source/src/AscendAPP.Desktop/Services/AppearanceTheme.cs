using System.IO;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AscendAPP.Core.Settings;
namespace AscendAPP.Desktop.Services;
internal static class AppearanceTheme
{
    private sealed class ColorValue(Color initial):INotifyPropertyChanged
    {
        private Color _value=initial;public Color Value {get=>_value;set{if(_value==value)return;_value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private static readonly Dictionary<string,ColorValue> Values=new();
    private static readonly Dictionary<string,SolidColorBrush> Brushes=new();
    private static string _signature="";private static BitmapSource? _logo;
    internal static string LogoSignature {get;private set;}="";
    internal static readonly string[] ColorKeys=["0B0A0D","171319","F3EEF2","A59AA7","F28DB8","383039","33212D","1F1921","2D252F","998C9B","918394","958498","B8ABB9","B3A4B7","201A23","423646","362838","100D12","1B141E","48384B","352338","271D2A","59465D","A394A9","FFC0DB","593049","AD5A84","271C2B","57425E","171219","1D1720","211A25","66516E","BC6B97","37233A","302031","F6AAC9","9F5A82","B45C8A","B4A8BD","948397","291B25","E77EAB","796C7A","211821","302530","302830","A59AA7"];
    private static Color Parse(string hex)=>(Color)ColorConverter.ConvertFromString(hex);
    private static Color Mix(Color a,Color b,double f)=>Color.FromRgb((byte)Math.Round(a.R+(b.R-a.R)*f),(byte)Math.Round(a.G+(b.G-a.G)*f),(byte)Math.Round(a.B+(b.B-a.B)*f));
    private static double Lum(Color c){static double F(byte n){double x=n/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);}
    internal static double Contrast(Color a,Color b)=>(Math.Max(Lum(a),Lum(b))+.05)/(Math.Min(Lum(a),Lum(b))+.05);
    private static Color Readable(Color raw,Color panel,Color ink,double ratio)
    {for(int step=0;step<=20;step++){var color=Mix(raw,ink,step/20d);if(Contrast(color,panel)>=ratio)return color;}return ink;}
    internal static Brush Brush(string hex)
    {
        var key="Tone_"+hex.TrimStart('#').ToUpperInvariant();if(Brushes.TryGetValue(key,out var brush))return brush;
        var fallback=new SolidColorBrush(Parse(hex));fallback.Freeze();return fallback;
    }
    private static void Set(string key,Color color)
    {
        if(!Brushes.TryGetValue(key,out var brush)||brush.IsFrozen)
        {
            var source=new ColorValue(color);Values[key]=source;brush=new SolidColorBrush();
            BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(nameof(ColorValue.Value)){Source=source,Mode=BindingMode.OneWay});
            Brushes[key]=brush;Application.Current.Resources[key]=brush;
        }
        else if(Values[key].Value!=color){Values[key].Value=color;BindingOperations.GetBindingExpression(brush,SolidColorBrush.ColorProperty)?.UpdateTarget();}
    }
    internal static void Apply(MeterPreferences p)
    {
        var signature=$"{p.AccentColor}|{p.PanelColor}|{p.LogoColor}|{p.LogoFollowsAccent}";if(signature==_signature)return;_signature=signature;
        var panel=Parse(p.PanelColor);var raw=Parse(p.AccentColor);var ink=Lum(panel)>.36?Parse("#1D1820"):Parse("#F6F1F6");
        var accent=Readable(raw,panel,ink,3);var muted=Readable(Mix(panel,ink,.62),panel,ink,4.5);
        foreach(var hex in ColorKeys.Distinct()){
            Color color=hex switch{
                "0B0A0D"=>panel,
                "F3EEF2"=>ink,
                "F28DB8" or "F6AAC9" or "E77EAB" or "BC6B97"=>accent,
                "FFC0DB"=>Mix(raw,ink,.65),
                "A59AA7" or "998C9B" or "918394" or "958498" or "B8ABB9" or "B3A4B7" or "B4A8BD" or "948397" or "A394A9" or "796C7A"=>muted,
                "383039" or "423646" or "48384B" or "59465D" or "57425E" or "66516E" or "302530" or "302830"=>Mix(panel,ink,.22),
                "9F5A82" or "B45C8A" or "AD5A84"=>Mix(panel,accent,.63),
                "33212D" or "352338" or "593049" or "37233A" or "291B25" or "302031"=>Mix(panel,raw,.19),
                "100D12"=>Mix(panel,ink,.018),
                _=>Mix(Mix(panel,raw,.045),ink,.045)
            };
            Set("Tone_"+hex,color);
        }
        foreach(var (alias,hex) in new[]{("Surface","0B0A0D"),("Surface2","171319"),("Ink","F3EEF2"),("Muted","A59AA7"),("Accent","F28DB8"),("Line","383039"),("AccentSurface","33212D")})
            Application.Current.Resources[alias]=Brushes["Tone_"+hex];
        var logo=Parse(p.LogoFollowsAccent?p.AccentColor:p.LogoColor);var inner=Mix(logo,Lum(logo)>.55?Colors.Black:Colors.White,.7);
        Set("Logo",logo);Set("LogoInner",inner);Set("LogoEdge",Mix(panel,ink,.45));
        string logoKey=logo.ToString()+inner.ToString()+Mix(panel,ink,.45).ToString();if(logoKey!=LogoSignature){LogoSignature=logoKey;_logo=null;}
        Application.Current.Resources["BrandIcon"]=LogoImage;
        foreach(Window window in Application.Current.Windows)window.Icon=LogoImage;
    }
    internal static BitmapSource LogoImage
    {
        get{
            if(_logo is not null)return _logo;
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new ScaleTransform(3.2,3.2));DrawFlame(dc);dc.Pop();}
            var bitmap=new RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return _logo=bitmap;
        }
    }
    internal static readonly Geometry Flame=Freeze("M11,0.7 C12,5.5 17.6,6.3 17.6,12.5 C17.6,17.1 14.1,19.3 10,19.3 C5.8,19.3 2.5,16.8 2.5,12.7 C2.5,9.8 4.3,7.3 6.3,5.5 C6.2,8 7.8,9.5 8.8,10 C7.8,5.7 11.2,4 11,0.7 Z");
    internal static readonly Geometry FlameCore=Freeze("M10.6,10 C11,12 14,13 12.8,16 C11.6,19 7.2,17.8 7.3,15 C7.3,13.1 9.3,12.8 10.6,10 Z");
    private static Geometry Freeze(string value){var g=Geometry.Parse(value);g.Freeze();return g;}
    internal static void DrawFlame(DrawingContext dc)
    {
        dc.DrawGeometry(Brushes.GetValueOrDefault("Logo")??System.Windows.Media.Brushes.HotPink,new Pen(Brushes.GetValueOrDefault("LogoEdge")??System.Windows.Media.Brushes.Transparent,.35),Flame);
        dc.DrawGeometry(Brushes.GetValueOrDefault("LogoInner")??System.Windows.Media.Brushes.MistyRose,null,FlameCore);
    }
    internal static System.Drawing.Icon CreateTrayIcon()
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(LogoImage));using var png=new MemoryStream();encoder.Save(png);var bytes=png.ToArray();
        using var ico=new MemoryStream();using(var writer=new BinaryWriter(ico,System.Text.Encoding.UTF8,true)){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)1);writer.Write((byte)64);writer.Write((byte)64);writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(bytes.Length);writer.Write(22);writer.Write(bytes);}
        ico.Position=0;using var source=new System.Drawing.Icon(ico);return (System.Drawing.Icon)source.Clone();
    }
}
