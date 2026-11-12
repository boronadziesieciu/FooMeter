using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using AscendAPP.Desktop.Services;
using AscendAPP.Core.Pets;
namespace AscendAPP.Desktop;
internal static class LootCounterPreview
{
    public static void Render(string output)
    {
        using var controller=new MeterController(preview:true);
        var grid=new Grid{Width=480,Height=240,Background=new SolidColorBrush(Color.FromRgb(28,33,39))};
        grid.Children.Add(new TextBlock{Text="DEMO · loot-row placement",Foreground=Brushes.Gray,Margin=new Thickness(12),FontSize=11});
        var canvas=new Canvas();grid.Children.Add(canvas);
        for(int i=0;i<3;i++){var line=new StackPanel{Orientation=Orientation.Horizontal};line.Children.Add(new TextBlock{Text=i==0?"14 / 25":i==1?"15 / 25":"11 / 25",Foreground=Brushes.White,FontSize=18,FontWeight=FontWeights.SemiBold,Width=100,TextAlignment=TextAlignment.Right,Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center});line.Children.Add(new Controls.PetPortrait{PetId=i<2?1016u:1121u,Width=40,Height=40,Margin=new Thickness(0,0,9,0)});line.Children.Add(new TextBlock{Text=i<2?"Soul: Slink (Bound) x1":"Soul: Young Slink (Bound) x1",Foreground=Brushes.LightGray,FontSize=14,VerticalAlignment=VerticalAlignment.Center});Canvas.SetLeft(line,12);Canvas.SetTop(line,45+i*58);canvas.Children.Add(line);}
        grid.Measure(new Size(480,240));grid.Arrange(new Rect(0,0,480,240));grid.UpdateLayout();var bitmap=new RenderTargetBitmap(480,240,96,96,PixelFormats.Pbgra32);bitmap.Render(grid);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(output);encoder.Save(file);
    }
}
