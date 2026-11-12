using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AscendAPP.Desktop.Controls;
public sealed class ClassBadge : Control
{
    public static readonly DependencyProperty ClassNameProperty=DependencyProperty.Register(nameof(ClassName),typeof(string),typeof(ClassBadge),new FrameworkPropertyMetadata("Unknown",FrameworkPropertyMetadataOptions.AffectsRender));
    public string? ClassName {get=>(string?)GetValue(ClassNameProperty);set=>SetValue(ClassNameProperty,value);}
    private static readonly Dictionary<string,BitmapSource?> Images=new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,string> Names=new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gladiator"]="Gladiator",["Templar"]="Templar",["Assassin"]="Assassin",["Ranger"]="Ranger",["Sorcerer"]="Sorcerer",["Spiritmaster"]="Spiritmaster",["Elementalist"]="Spiritmaster",["Cleric"]="Cleric",["Chanter"]="Chanter",["Brawler"]="Brawler",["Fighter"]="Brawler"
    };
    public ClassBadge(){RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);}
    internal static BitmapSource? Artwork(string? name)
    {
        if(string.IsNullOrWhiteSpace(name))return null;
        if(!Names.TryGetValue(name.Trim(),out name))return null;
        if(Images.TryGetValue(name,out var image))return image;
        try{using var file=typeof(ClassBadge).Assembly.GetManifestResourceStream("AscendAPP.Classes."+name+".png")??throw new FileNotFoundException("Class artwork unavailable.");var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=file;b.DecodePixelWidth=64;b.EndInit();b.Freeze();image=b;}catch(Exception e)when(e is IOException or NotSupportedException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException){image=null;}
        Images[name]=image;return image;
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<=0||ActualHeight<=0)return;
        if(Artwork(ClassName)is {} image)
        {
            double scale=Math.Min(ActualWidth/image.PixelWidth,ActualHeight/image.PixelHeight);
            double w=image.PixelWidth*scale,h=image.PixelHeight*scale;
            dc.DrawImage(image,new Rect((ActualWidth-w)/2,(ActualHeight-h)/2,w,h));
        }
        else {double r=Math.Max(0,Math.Min(ActualWidth,ActualHeight)/2-2);dc.DrawEllipse(null,new Pen(Foreground,1),new(ActualWidth/2,ActualHeight/2),r,r);}
    }
}
