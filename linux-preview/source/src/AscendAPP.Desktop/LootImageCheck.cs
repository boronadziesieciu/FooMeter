using System.Drawing;
using System.IO;
using System.Text.Json;
using AscendAPP.Core.Pets;
using AscendAPP.Desktop.Services;
namespace AscendAPP.Desktop;
internal static class LootImageCheck
{
    public static void Run(string image,string output,string? cropText=null)
    {
        using var original=new Bitmap(image);Rectangle crop=new(0,0,original.Width,original.Height);
        if(cropText is not null){var n=cropText.Split(',').Select(int.Parse).ToArray();crop=Rectangle.Intersect(crop,new Rectangle(n[0],n[1],n[2],n[3]));}
        using var source=original.Clone(crop,System.Drawing.Imaging.PixelFormat.Format24bppRgb);using var engine=GameLootReader.CreateEngine();var watch=System.Diagnostics.Stopwatch.StartNew();
        var lines=GameLootReader.Recognize(engine,source,2);var names=JsonSerializer.Deserialize<Dictionary<uint,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"data","pet_names.json")))!;var matcher=new LootRowMatcher(names);
        var matched=lines.Select(l=>new{Id=matcher.Identify(l.Text),Line=l}).Where(x=>x.Id.HasValue).Select(x=>new{x.Id,Name=names[x.Id!.Value],x.Line.Y,x.Line.Height,x.Line.Confidence}).ToArray();
        File.WriteAllText(output,JsonSerializer.Serialize(new{ElapsedMs=watch.ElapsedMilliseconds,Lines=lines.Count,SoulLines=lines.Where(l=>l.Text.Contains("oul",StringComparison.OrdinalIgnoreCase)).Select(l=>new{l.Text,l.X,l.Y,l.Confidence}),Matched=matched},new JsonSerializerOptions{WriteIndented=true}));
    }
}
