using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;

namespace FooMeter.Linux;

/// <summary>Only floats/resizes this process's own overlay clients. No global rule or focus changes.</summary>
internal static class HyprlandOverlay
{
    private static readonly Dictionary<Window,(int Width,int Height)> Applied=new();
    private static readonly HashSet<Window> Busy=[];
    private static readonly Dictionary<Window,DateTime> RetryAfter=new();
    public static void Fit(Window window,int width,int height)
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"))||!window.IsVisible||Busy.Contains(window))return;
        height=Math.Clamp(height,24,800);
        if(Applied.GetValueOrDefault(window)==(width,height)||RetryAfter.GetValueOrDefault(window)>DateTime.UtcNow)return;
        RetryAfter[window]=DateTime.UtcNow.AddSeconds(5);
        Busy.Add(window);
        string title=window.Title??"";int pid=Environment.ProcessId;
        _=Task.Run(async()=>{
            try {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
                string json=await LootReader.Run("hyprctl",timeout.Token,"clients","-j");
                using var clients=JsonDocument.Parse(json);
                var matches=clients.RootElement.EnumerateArray().Where(c=>c.TryGetProperty("pid",out var p)&&p.GetInt32()==pid&&c.TryGetProperty("title",out var t)&&t.GetString()==title).ToArray();
                if(matches.Length!=1)return;
                var client=matches[0];string address=client.GetProperty("address").GetString()!;
                if(!System.Text.RegularExpressions.Regex.IsMatch(address,"^0x[0-9a-fA-F]+$"))return;
                if(!client.GetProperty("floating").GetBoolean())await LootReader.Run("hyprctl",timeout.Token,"dispatch","setfloating","address:"+address);
                await LootReader.Run("hyprctl",timeout.Token,"dispatch","resizewindowpixel",$"exact {width} {height},address:{address}");
                Dispatcher.UIThread.Post(()=>{Applied[window]=(width,height);RetryAfter.Remove(window);});
            }catch(Exception e){Console.Error.WriteLine("FooMeter overlay sizing: "+e.Message);}
            finally{Dispatcher.UIThread.Post(()=>Busy.Remove(window));}
        });
    }
    public static void Forget(Window window){Applied.Remove(window);Busy.Remove(window);RetryAfter.Remove(window);}
}
