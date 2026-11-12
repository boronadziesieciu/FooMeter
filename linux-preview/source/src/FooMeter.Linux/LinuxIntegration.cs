using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using AscendAPP.Core.Pets;

namespace FooMeter.Linux;

internal sealed class CommandServer : IDisposable
{
    public static string SocketPath => Path.Combine(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? Path.Combine(Path.GetTempPath(),"foometer-"+Environment.UserName), "foometer.sock");
    private readonly Socket _socket = new(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);
    private readonly CancellationTokenSource _stop = new();
    private readonly string _path;
    public CommandServer(Action<string> command)
    {
        _path=SocketPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"))&&!OperatingSystem.IsWindows())File.SetUnixFileMode(Path.GetDirectoryName(_path)!,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        if(File.Exists(_path))
        {
            using var probe = new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);
            try { probe.Connect(new UnixDomainSocketEndPoint(_path)); throw new InvalidOperationException("FooMeter is already running. Use --command show."); }
            catch(SocketException) { File.Delete(_path); }
        }
        _socket.Bind(new UnixDomainSocketEndPoint(_path)); if(!OperatingSystem.IsWindows())File.SetUnixFileMode(_path,UnixFileMode.UserRead|UnixFileMode.UserWrite); _socket.Listen(8);
        _ = Task.Run(async()=> {
            while(!_stop.IsCancellationRequested)
            try {
                using var client = await _socket.AcceptAsync(_stop.Token);
                var data = new byte[128];
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); timeout.CancelAfter(2000);
                int n = await client.ReceiveAsync(data,SocketFlags.None,timeout.Token);
                string message = System.Text.Encoding.UTF8.GetString(data,0,n).Trim();
                if(new[]{"show","hide","toggle","reset","copy","strip","events","pets","clickthrough","settings","quit"}.Contains(message))
                    Dispatcher.UIThread.Post(()=>command(message));
            } catch(OperationCanceledException) { } catch(ObjectDisposedException) { break; } catch(SocketException) { if(_stop.IsCancellationRequested)break; }
        });
    }
    public static void Send(string message)
    { using var socket = new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified); socket.Connect(new UnixDomainSocketEndPoint(SocketPath)); socket.Send(System.Text.Encoding.UTF8.GetBytes(message+"\n")); }
    public void Dispose() { _stop.Cancel(); _socket.Dispose(); File.Delete(_path); _stop.Dispose(); }
}

internal sealed class XInput : IDisposable
{
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XFree(IntPtr data);
    [DllImport("libXext.so.6")] private static extern IntPtr XShapeGetRectangles(IntPtr display,UIntPtr window,int kind,out int count,out int ordering);
    [DllImport("libX11.so.6")] private static extern int XFlush(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XQueryKeymap(IntPtr display, byte[] keys);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(IntPtr display, UIntPtr symbol);
    [DllImport("libXext.so.6")] private static extern void XShapeCombineRectangles(IntPtr display, UIntPtr window,int kind,int x,int y,IntPtr rectangles,int count,int operation,int ordering);
    [DllImport("libXext.so.6")] private static extern void XShapeCombineMask(IntPtr display, UIntPtr window,int kind,int x,int y,UIntPtr source,int operation);
    private IntPtr _display;
    private bool? _applied;
    private readonly byte[] _keys = new byte[32];
    public void Apply(Window window,bool passthrough)
    {
        if(_display==IntPtr.Zero) _display=XOpenDisplay(IntPtr.Zero);
        if(_display==IntPtr.Zero) return;
        XQueryKeymap(_display,_keys);
        bool Down(uint symbol) { int code=XKeysymToKeycode(_display,(UIntPtr)symbol); return code>0&&(_keys[code/8]&(1<<(code%8)))!=0; }
        bool enabled=passthrough&&!Down(0xffe9)&&!Down(0xffea);
        if(_applied==enabled||window.TryGetPlatformHandle() is not {} handle)return;
        if(enabled) XShapeCombineRectangles(_display,(UIntPtr)(nuint)handle.Handle,2,0,0,IntPtr.Zero,0,0,0);
        else XShapeCombineMask(_display,(UIntPtr)(nuint)handle.Handle,2,0,0,UIntPtr.Zero,0);
        XFlush(_display); _applied=enabled;
    }
    internal bool HasInput(Window window)
    { var data=XShapeGetRectangles(_display,(UIntPtr)(nuint)window.TryGetPlatformHandle()!.Handle,2,out int count,out _);if(data!=IntPtr.Zero)XFree(data);return count>0; }
    public void Dispose() { if(_display!=IntPtr.Zero)XCloseDisplay(_display); }
}

internal sealed class LootReader : IDisposable
{
    private readonly LinuxMeter _meter;
    public string Region { get; private set; } = "";
    public string Status { get; private set; } = "Loot reader stopped";
    public IReadOnlyList<PetLootRow> Rows { get; private set; } = [];
    private CancellationTokenSource? _stop;
    private Task? _loop;
    public bool Running => _loop is { IsCompleted:false };
    public LootReader(LinuxMeter meter) => _meter=meter;
    public static async Task<string> Run(string executable,CancellationToken token,params string[] args)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;
        try {
            var stdout=process.StandardOutput.ReadToEndAsync(token); var stderr=process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token); string output=await stdout,error=await stderr;
            if(process.ExitCode!=0)throw new InvalidOperationException(executable+": "+error.Trim()); return output;
        } finally { if(!process.HasExited)process.Kill(true); }
    }
    public async Task Choose() { Region=(await Run("slurp",CancellationToken.None)).Trim(); if(!System.Text.RegularExpressions.Regex.IsMatch(Region,@"^-?\d+,-?\d+ \d+x\d+$"))throw new InvalidOperationException("No valid screenshot region selected."); Status="Region selected: "+Region; }
    public void Start()
    {
        if(Running)return; if(Region.Length==0)throw new InvalidOperationException("Choose the loot text region first.");
        _stop=new();var token=_stop.Token;
        _loop=Task.Run(async()=> {
            string path=Path.Combine(Path.GetTempPath(),"foometer-loot-"+Guid.NewGuid().ToString("N")+".png");
            try {
                while(!token.IsCancellationRequested) {
                    if(!OperatingSystem.IsWindows()) { using var empty=new FileStream(path,new FileStreamOptions{Mode=FileMode.Create,Access=FileAccess.Write,UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}); }
                    await Run("grim",token,"-s","1","-g",Region,path);
                    if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
                    string tsv=await Run("tesseract",token,path,"stdout","-l","eng","--psm","6","tsv");
                    Rows=_meter.ResolveLoot(Parse(tsv));Status=$"Reading loot · {Rows.Count} matching rows";
                    await Task.Delay(800,token);
                }
            } catch(OperationCanceledException) { Status="Loot reader stopped"; }
            catch(Exception e) { Status=e.Message; }
            finally { Rows=[]; if(File.Exists(path))File.Delete(path); }
        });
    }
    internal static IReadOnlyList<LootTextLine> Parse(string tsv)
    {
        var rows=new List<(string Key,string Text,int X,int Y,int W,int H,float Confidence)>();
        foreach(var line in tsv.Split('\n').Skip(1)) {
            var c=line.Split('\t'); if(c.Length<12||c[0]!="5"||string.IsNullOrWhiteSpace(c[11]))continue;
            if(!int.TryParse(c[6],out int x)||!int.TryParse(c[7],out int y)||!int.TryParse(c[8],out int w)||!int.TryParse(c[9],out int h)||!float.TryParse(c[10],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float confidence))continue;
            rows.Add((string.Join(":",c.Take(5)),c[11],x,y,w,h,confidence));
        }
        return rows.GroupBy(r=>r.Key).Select(g=>new LootTextLine(string.Join(" ",g.Select(x=>x.Text)),g.Min(x=>x.X),g.Min(x=>x.Y),g.Max(x=>x.X+x.W)-g.Min(x=>x.X),g.Max(x=>x.Y+x.H)-g.Min(x=>x.Y),g.Min(x=>x.Confidence))).ToArray();
    }
    public void Dispose() { _stop?.Cancel(); _loop?.GetAwaiter().GetResult(); _stop?.Dispose(); }
}
