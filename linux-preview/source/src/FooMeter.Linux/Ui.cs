using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Storage;

namespace FooMeter.Linux;

internal static class Ui
{
    public static TextBlock Text(string text, double size = 13) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, Action action)
    { var b = new Button { Content = text, Margin = new(3), Padding = new(10,6) }; b.Click += (_,_) => action(); return b; }
    public static Button Async(string text, Func<Task> action, Action<Exception> error)
    { var b = new Button { Content = text, Margin = new(3) }; b.Click += async (_,_) => { b.IsEnabled = false; try { await action(); } catch(Exception e) { error(e); } finally { b.IsEnabled = true; } }; return b; }
    public static StackPanel Stack(params Control[] children)
    { var p = new StackPanel { Spacing = 8 }; foreach(var c in children) p.Children.Add(c); return p; }
    public static ScrollViewer Scroll(Control child) => new() { Content = child, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    public static Window Window(string title, Control content, int width = 580, int height = 620) => new()
    { Title = title, Width = width, Height = height, MinWidth = 300, MinHeight = 200, Content = new Border { Padding = new(16), Child = content }, Background = Brush.Parse("#191D26") };
    private static readonly Dictionary<string, Bitmap> Images = new();
    public static Image? Picture(string relative, int size = 32)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", relative);
        if (!File.Exists(path)) return null;
        if (!Images.TryGetValue(path, out var image)) Images[path] = image = new Bitmap(path);
        return new Image { Source = image, Width = size, Height = size, Margin = new(0,0,8,0) };
    }
    public static async Task Export(Window owner, EncounterSnapshot fight, bool json)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new() { Title = "Export encounter", SuggestedFileName = "FooMeter-" + fight.StartedAt.ToString("yyyyMMdd-HHmmss") + (json ? ".json" : ".csv") });
        if(file?.TryGetLocalPath() is not {} path) return;
        if(json) EncounterHistoryStore.Export(path, fight); else CombatCsv.Save(path, fight);
    }
    public static string Share(EncounterSnapshot f, bool multiline, bool hideNames = false) => string.Join(multiline ? "\n" : " | ",
        new[] { $"{f.TargetName ?? "Encounter"}: {f.DurationSeconds:N0}s · {f.PartyDps:N0} DPS" }.Concat(f.Players.Select((p,i)=>$"{i+1}. {(hideNames&&!p.IsSelf ? p.ClassName??"Player" : p.Name)} {p.Dps:N0} DPS")));
}
