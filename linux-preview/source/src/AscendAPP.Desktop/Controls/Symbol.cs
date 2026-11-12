using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace AscendAPP.Desktop.Controls;
public sealed class Symbol : Control
{
    public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(Symbol),new FrameworkPropertyMetadata("Circle",FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind {get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
    static Symbol(){WidthProperty.OverrideMetadata(typeof(Symbol),new FrameworkPropertyMetadata(16d));HeightProperty.OverrideMetadata(typeof(Symbol),new FrameworkPropertyMetadata(16d));ForegroundProperty.OverrideMetadata(typeof(Symbol),new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(150,168,180)),FrameworkPropertyMetadataOptions.AffectsRender));}
    private static readonly Dictionary<string,Geometry> Shapes=new Dictionary<string,string>
    {
        ["Strip"]="M2,6 L18,6 18,14 2,14 Z M5,9 L8,9 M11,9 L15,9",
        ["Pet"]="M7,11 C2,11 2,17 6,17 C8,17 9,15 10,15 C12,15 13,18 16,16 C19,13 14,8 12,10 C11,11 9,10 7,11 Z M5,5 A1.5,2 0 1 1 4.9,5 M10,2 A1.5,2 0 1 1 9.9,2 M15,5 A1.5,2 0 1 1 14.9,5",
        ["Pin"]= "M7,2 L13,2 12,7 16,11 16,12 4,12 4,11 8,7 Z M10,12 L10,19",
        ["Target"]="M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 M10,0 L10,5 M10,15 L10,20 M0,10 L5,10 M15,10 L20,10",
        ["Grip"]="M6,4 L7,4 M13,4 L14,4 M6,10 L7,10 M13,10 L14,10 M6,16 L7,16 M13,16 L14,16",
        ["Copy"]="M6,2 L16,2 16,13 6,13 Z M3,5 L3,17 13,17",
        ["Settings"]="M10,2 L12,2 13,5 16,5 18,8 16,10 18,12 16,15 13,15 12,18 8,18 7,15 4,15 2,12 4,10 2,8 4,5 7,5 8,2 Z M13,10 A3,3 0 1 1 7,10 A3,3 0 1 1 13,10",
        ["History"]="M3,9 A7,7 0 1 1 4,15 M3,3 L3,9 9,9 M10,6 L10,10 13,12",
        ["Menu"]="M3,4 L17,4 M3,10 L17,10 M3,16 L17,16",
        ["Close"]="M4,4 L16,16 M16,4 L4,16", ["Minus"]="M4,10 L16,10",
        ["Lock"]="M4,9 L16,9 16,18 4,18 Z M6,9 L6,6 C6,0 14,0 14,6 L14,9 M10,12 L10,15",
        ["Unlock"]="M4,9 L16,9 16,18 4,18 Z M6,9 L6,6 C6,0 14,0 14,6 M10,12 L10,15",
        ["ClickThrough"]="M5,2 L5,16 9,12 12,18 15,16 12,11 18,10 Z",
        ["Reset"]="M16,7 A7,7 0 1 0 17,11 M16,2 L16,7 11,7",
        ["Clear"]="M3,5 L17,5 M7,5 L7,2 13,2 13,5 M5,5 L6,18 14,18 15,5 M8,8 L8,15 M12,8 L12,15",
        ["Play"]="M6,3 L17,10 6,17 Z", ["Stop"]="M5,5 L15,5 15,15 5,15 Z",
        ["Combat"]="M3,3 L16,16 M13,17 L17,13 M4,8 L3,3 8,4 M17,3 L4,16 M3,13 L7,17 M12,4 L17,3 16,8",
        ["Heal"]="M8,3 L12,3 12,8 17,8 17,12 12,12 12,17 8,17 8,12 3,12 3,8 8,8 Z",
        ["Display"]="M2,3 L18,3 18,14 2,14 Z M10,14 L10,18 M6,18 L14,18",
        ["Overlay"]="M2,3 L18,3 18,17 2,17 Z M2,7 L18,7 M5,5 L6,5 M8,5 L9,5",
        ["Audio"]="M3,8 L6,8 11,4 11,16 6,12 3,12 Z M14,7 C17,9 17,11 14,13 M16,4 C21,8 21,12 16,16",
        ["Timer"]="M10,5 A6,6 0 1 1 9.9,5 M10,8 L10,12 13,12 M7,2 L13,2 M10,2 L10,5",
        ["Bell"]="M4,14 L6,11 6,7 C6,2 14,2 14,7 L14,11 16,14 Z M8,17 C9,19 11,19 12,17",
        ["Keyboard"]="M1,5 L19,5 19,16 1,16 Z M4,8 L5,8 M8,8 L9,8 M12,8 L13,8 M16,8 L17,8 M4,11 L5,11 M8,11 L9,11 M12,11 L13,11 M5,14 L15,14",
        ["Account"]="M13,6 A3,3 0 1 1 7,6 A3,3 0 1 1 13,6 M3,18 C3,10 17,10 17,18",
        ["Plug"]="M6,2 L6,7 M14,2 L14,7 M4,7 L16,7 16,10 C16,16 4,16 4,10 Z M10,15 L10,19",
        ["Notes"]="M4,2 L13,2 17,6 17,18 4,18 Z M13,2 L13,6 17,6 M7,9 L14,9 M7,12 L14,12 M7,15 L12,15",
        ["Debug"]="M6,7 L14,7 14,16 C14,20 6,20 6,16 Z M7,4 C7,1 13,1 13,4 L13,7 M2,8 L6,10 M14,10 L18,8 M2,14 L6,14 M14,14 L18,14 M3,19 L6,17 M14,17 L17,19 M10,8 L10,18",
        ["Sliders"]="M2,5 L18,5 M2,10 L18,10 M2,15 L18,15 M6,2 L6,8 M14,7 L14,13 M8,12 L8,18",
        ["Ticket"]="M2,4 L18,4 18,8 C14,8 14,12 18,12 L18,16 2,16 2,12 C6,12 6,8 2,8 Z M12,6 L12,7 M12,9 L12,11 M12,13 L12,14",
        ["Export"]="M10,2 L10,13 M6,6 L10,2 14,6 M3,12 L3,18 17,18 17,12",
        ["Import"]="M10,2 L10,13 M6,9 L10,13 14,9 M3,12 L3,18 17,18 17,12",
        ["Plus"]="M10,3 L10,17 M3,10 L17,10", ["Check"]="M3,10 L8,15 17,4",
        ["Shield"]="M10,2 L17,5 16,12 C15,16 12,18 10,19 C8,18 5,16 4,12 L3,5 Z",
        ["Bow"]="M4,2 C20,5 20,15 4,18 L9,10 Z M2,10 L18,10 M14,6 L18,10 14,14",
        ["Portal"]="M13,2 C3,-1 0,18 9,18 C19,19 20,2 13,2 Z M11,5 C6,4 4,15 10,15 C15,15 17,6 11,5 Z M2,4 L5,5 M15,16 L18,18",
        ["Spark"]="M10,1 L12,7 19,10 12,12 10,19 8,12 1,10 8,7 Z",
        ["FlameCore"]="M10.6,10 C11,12 14,13 12.8,16 C11.6,19 7.2,17.8 7.3,15 C7.3,13.1 9.3,12.8 10.6,10 Z",
        ["Flame"]="M11,0.7 C12,5.5 17.6,6.3 17.6,12.5 C17.6,17.1 14.1,19.3 10,19.3 C5.8,19.3 2.5,16.8 2.5,12.7 C2.5,9.8 4.3,7.3 6.3,5.5 C6.2,8 7.8,9.5 8.8,10 C7.8,5.7 11.2,4 11,0.7 Z",
        ["Wings"]="M10,18 C4,14 2,8 1,3 C5,7 8,7 10,12 C12,7 15,7 19,3 C18,8 16,14 10,18 Z",
        ["Fist"]="M5,18 L3,12 3,7 6,7 6,3 9,3 9,2 12,2 12,3 15,3 15,5 18,5 18,13 14,18 Z M6,7 L12,7 12,11 7,11",
        ["Circle"]="M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10",
        ["Info"]="M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 M10,9 L10,14 M10,6 L10,6.3"
    }.ToDictionary(x=>x.Key,x=>{var g=Geometry.Parse(x.Value);g.Freeze();return g;});
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); var pen=new Pen(Foreground,1.35){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        dc.PushTransform(new ScaleTransform(ActualWidth/20,ActualHeight/20));
        if(Kind=="Brand"){Services.AppearanceTheme.DrawFlame(dc);}
        else if(Kind=="Flame"){dc.DrawGeometry(Foreground,null,Shapes["Flame"]);dc.DrawGeometry(Brushes.MistyRose,null,Shapes["FlameCore"]);}
        else dc.DrawGeometry(null,pen,Shapes.GetValueOrDefault(Kind,Shapes["Circle"]));dc.Pop();
    }
}
