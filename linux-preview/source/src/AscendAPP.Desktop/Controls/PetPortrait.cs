using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AscendAPP.Desktop.Controls;
public sealed class PetPortrait : Control
{
    public static readonly DependencyProperty PetIdProperty=DependencyProperty.Register(nameof(PetId),typeof(uint),typeof(PetPortrait),new FrameworkPropertyMetadata(0u,FrameworkPropertyMetadataOptions.AffectsRender));
    public uint PetId {get=>(uint)GetValue(PetIdProperty);set=>SetValue(PetIdProperty,value);}
    private static readonly Dictionary<uint,BitmapSource?> Images=new();
    public PetPortrait(){RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);}
    internal static BitmapSource? Artwork(uint id)
    {
        if(Images.TryGetValue(id,out var image))return image;
        try{using var stream=typeof(PetPortrait).Assembly.GetManifestResourceStream("FooMeter.Pets."+id+".png")??throw new FileNotFoundException();
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.DecodePixelWidth=64;bitmap.EndInit();bitmap.Freeze();image=bitmap;}
        catch(Exception e)when(e is IOException or NotSupportedException or System.Runtime.InteropServices.COMException){image=null;}
        if(Images.Count<1024)Images[id]=image;return image;
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<=0||ActualHeight<=0)return;
        var rect=new Rect(0,0,ActualWidth,ActualHeight);var clip=new RectangleGeometry(rect,5,5);dc.PushClip(clip);
        if(Artwork(PetId) is {} image)dc.DrawImage(image,rect);
        else{dc.DrawRectangle(Services.AppearanceTheme.Brush("#171319"),null,rect);dc.DrawEllipse(null,new Pen(Services.AppearanceTheme.Brush("#A59AA7"),1),new(ActualWidth/2,ActualHeight/2),ActualWidth/4,ActualHeight/4);}
        dc.Pop();
    }
}
