using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using AscendAPP.Core.Pets;
using Tesseract;
namespace AscendAPP.Desktop.Services;
internal sealed record LootReadConfig(bool Enabled,string Process,double Left,double Top,double Width,double Height,int Gap);
internal sealed record LootReadFrame(Rectangle Game,Rectangle Crop,IReadOnlyList<PetLootRow> Rows,int Gap);
internal sealed class GameLootReader : IDisposable
{
    private readonly CancellationTokenSource _stop=new();private readonly Task _task;
    private readonly LootRowMatcher _matcher;private readonly Func<uint,PetProgress> _progress;private readonly Func<IReadOnlyList<PetPickup>> _gains;private readonly Action<LootReadFrame?> _publish;
    private LootReadConfig _config=new(false,"AION2",.385,.28,.20,.48,44);
    private string _status="Loot-row counter idle";private long _frames,_matched;private double _milliseconds;
    public string Status=>_status;
    public object Diagnostics=>new{Status,Frames=Interlocked.Read(ref _frames),MatchedRows=Interlocked.Read(ref _matched),LastReadMs=Volatile.Read(ref _milliseconds),ScreenImagesSaved=false,OnlyForegroundGame=true};
    public GameLootReader(IReadOnlyDictionary<uint,string> names,Func<uint,PetProgress> progress,Func<IReadOnlyList<PetPickup>> gains,Action<LootReadFrame?> publish)
    {_matcher=new(names);_progress=progress;_gains=gains;_publish=publish;_task=Task.Run(Loop);}
    public void Configure(LootReadConfig config)=>Volatile.Write(ref _config,config);
    private async Task Loop()
    {
        TesseractEngine? engine=null;DateTimeOffset lastActive=DateTimeOffset.MinValue;
        try{
            while(!_stop.IsCancellationRequested){
                var config=Volatile.Read(ref _config);
                if(!config.Enabled||!GameClient.TryForeground(config.Process,out var game)){
                    _status=config.Enabled?"Waiting for the game to be in front":"Loot-row counter disabled";_publish(null);
                    if(engine is not null&&DateTimeOffset.UtcNow-lastActive>TimeSpan.FromSeconds(10)){engine.Dispose();engine=null;}
                    await Task.Delay(700,_stop.Token);continue;
                }
                lastActive=DateTimeOffset.UtcNow;var watch=Stopwatch.StartNew();
                try{
                    engine??=CreateEngine();
                    var crop=Crop(game,config);
                    using var image=new Bitmap(crop.Width,crop.Height,PixelFormat.Format24bppRgb);
                    using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(crop.Left,crop.Top,0,0,crop.Size,CopyPixelOperation.SourceCopy);
                    if(!GameClient.TryForeground(config.Process,out var afterCapture)||afterCapture!=game){_publish(null);continue;}
                    var lines=Recognize(engine,image,image.Width*image.Height<=250000?2:1);
                    var rows=_matcher.Resolve(lines,_progress,_gains(),DateTimeOffset.UtcNow);
                    Interlocked.Increment(ref _frames);Interlocked.Add(ref _matched,rows.Count);
                    _status=rows.Count>0?$"Reading loot · {rows.Count} pet row{(rows.Count==1?"":"s")}":"Reading loot area · waiting for a readable Soul: row";
                    if(GameClient.TryForeground(config.Process,out var afterRead)&&afterRead==game)_publish(new(game,crop,rows,config.Gap));else _publish(null);
                }catch(Exception e){_status="Loot counter: "+e.GetBaseException().Message;_publish(null);await Task.Delay(1500,_stop.Token);}
                Volatile.Write(ref _milliseconds,watch.Elapsed.TotalMilliseconds);
                await Task.Delay(Math.Max(80,250-(int)watch.Elapsed.TotalMilliseconds),_stop.Token);
            }
        }catch(OperationCanceledException){}finally{engine?.Dispose();}
    }
    internal static TesseractEngine CreateEngine(){Environment.SetEnvironmentVariable("OMP_THREAD_LIMIT","1");var engine=new TesseractEngine(Path.Combine(AppContext.BaseDirectory,"tessdata"),"eng",EngineMode.LstmOnly,Array.Empty<string>(),new Dictionary<string,object>{{"load_system_dawg",false},{"load_freq_dawg",false},{"user_words_file",Path.Combine(AppContext.BaseDirectory,"tessdata","pet-words.txt")}},false);engine.SetVariable("user_defined_dpi",180);return engine;}
    internal static Rectangle Crop(Rectangle game,LootReadConfig c)
    {
        int x=game.Left+(int)Math.Round(game.Width*c.Left),y=game.Top+(int)Math.Round(game.Height*c.Top);
        int width=Math.Clamp((int)Math.Round(game.Width*c.Width),20,Math.Max(20,game.Right-x));
        int height=Math.Clamp((int)Math.Round(game.Height*c.Height),20,Math.Max(20,game.Bottom-y));
        // Bound OCR work: a large game window still needs only the selected text crop.
        return Rectangle.Intersect(game,new Rectangle(x,y,Math.Min(width,900),Math.Min(height,900)));
    }
    internal static IReadOnlyList<LootTextLine> Recognize(TesseractEngine engine,Bitmap source,int scale,bool mask=true)
    {
        using var prepared=mask?TextMask(source):new Bitmap(source);
        using var resized=new Bitmap(source.Width*scale,source.Height*scale,PixelFormat.Format24bppRgb);
        using(var g=Graphics.FromImage(resized)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(prepared,new Rectangle(0,0,resized.Width,resized.Height));}
        using var stream=new MemoryStream();resized.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
        using var pix=Pix.LoadFromMemory(stream.ToArray());using var page=engine.Process(pix,PageSegMode.SparseText);using var iterator=page.GetIterator();var lines=new List<LootTextLine>();
        iterator.Begin();do{
            string text=iterator.GetText(PageIteratorLevel.TextLine)??"";
            if(iterator.TryGetBoundingBox(PageIteratorLevel.TextLine,out var box)&&text.Length<=512)
                lines.Add(new(text.Trim(),box.X1/(double)scale,box.Y1/(double)scale,box.Width/(double)scale,box.Height/(double)scale,iterator.GetConfidence(PageIteratorLevel.TextLine)));
        }while(iterator.Next(PageIteratorLevel.TextLine)&&lines.Count<150);
        var heights=lines.Where(l=>l.Confidence>=30&&(l.Text.Contains("oul",StringComparison.OrdinalIgnoreCase)||l.Text.Contains("Kina",StringComparison.OrdinalIgnoreCase)||l.Text.Contains("Bound",StringComparison.OrdinalIgnoreCase))).Select(l=>l.Height).Where(h=>h>=6).Order().ToArray();
        if(heights.Length>=3){double typical=heights[heights.Length/2];return lines.Where(l=>l.Height<=typical*1.75).ToArray();}
        return lines;
    }
    private static Bitmap TextMask(Bitmap source)
    {
        var result=new Bitmap(source.Width,source.Height,PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(result))graphics.DrawImageUnscaled(source,0,0);
        var locked=result.LockBits(new Rectangle(0,0,result.Width,result.Height),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
        try{var bytes=new byte[Math.Abs(locked.Stride)*result.Height];Marshal.Copy(locked.Scan0,bytes,0,bytes.Length);
            for(int y=0;y<result.Height;y++)for(int x=0;x<result.Width;x++){int i=y*locked.Stride+x*3;int b=bytes[i],g=bytes[i+1],r=bytes[i+2];int min=Math.Min(r,Math.Min(g,b)),max=Math.Max(r,Math.Max(g,b));byte value=(byte)(min>=150&&max-min<65?0:255);bytes[i]=bytes[i+1]=bytes[i+2]=value;}
            Marshal.Copy(bytes,0,locked.Scan0,bytes.Length);
        }finally{result.UnlockBits(locked);}
        return result;
    }
    public void Dispose(){_stop.Cancel();_task.ContinueWith(_=>_stop.Dispose(),TaskScheduler.Default);}
}
internal static class GameClient
{
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct Point{public int X,Y;}
    internal static bool TryForeground(string process,out Rectangle rect)
    {
        rect=default;var hwnd=GetForegroundWindow();GetWindowThreadProcessId(hwnd,out uint pid);
        try{using var p=Process.GetProcessById((int)pid);if(!p.ProcessName.Equals(Path.GetFileNameWithoutExtension(process),StringComparison.OrdinalIgnoreCase))return false;}
        catch{return false;}
        return Bounds(hwnd,out rect);
    }
    internal static bool Find(string process,out Rectangle rect)
    {
        rect=default;if(TryForeground(process,out rect))return true;
        foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(process))){using(p)if(p.MainWindowHandle!=IntPtr.Zero&&Bounds(p.MainWindowHandle,out rect))return true;}
        return false;
    }
    private static bool Bounds(IntPtr hwnd,out Rectangle rect)
    {
        rect=default;if(hwnd==IntPtr.Zero||IsIconic(hwnd)||!GetClientRect(hwnd,out var r))return false;var point=new Point();if(!ClientToScreen(hwnd,ref point))return false;
        rect=new(point.X,point.Y,r.Right-r.Left,r.Bottom-r.Top);return rect.Width>=320&&rect.Height>=240;
    }
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")]private static extern bool GetClientRect(IntPtr h,out NativeRect r);
    [DllImport("user32.dll")]private static extern bool ClientToScreen(IntPtr h,ref Point p);
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr h);
}
