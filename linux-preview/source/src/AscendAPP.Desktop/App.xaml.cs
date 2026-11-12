using System.Windows;
namespace AscendAPP.Desktop;
public partial class App : Application
{
    private MeterController? _controller;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length>=2&&(e.Args[0]=="--self-test-ui"||e.Args[0]=="--render-preview"))System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(e.Args[1]))!);
        if(e.Args.Length>=2&&e.Args[0]=="--self-test-ui"){try{UiSelfChecks.Run(e.Args[1]);Shutdown(0);}catch(Exception ex){System.IO.File.WriteAllText(e.Args[1]+".error.txt",ex.ToString());Shutdown(1);}return;}
        if(e.Args.Length>=2&&e.Args[0]=="--render-preview")
        {
            try{string? Arg(string name){int n=Array.IndexOf(e.Args,name);return n>=0&&n+1<e.Args.Length?e.Args[n+1]:null;}string? page=null;int i=Array.IndexOf(e.Args,"--settings");if(i>=0&&i+1<e.Args.Length)page=e.Args[i+1];PreviewRenderer.Render(e.Args[1],e.Args.Contains("--unknown"),page,e.Args.Contains("--healing"),e.Args.Contains("--minimal"),e.Args.Contains("--refreshing"),e.Args.Contains("--events"),e.Args.Contains("--party"),e.Args.Contains("--history"),e.Args.Contains("--strip"),e.Args.Contains("--pets"),e.Args.Contains("--pickup"),Arg("--accent"),Arg("--background"),Arg("--logo"));Shutdown(0);}
            catch(Exception ex){System.IO.File.WriteAllText(e.Args[1]+".error.txt",ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=3&&e.Args[0]=="--check-loot-image"){try{LootImageCheck.Run(e.Args[1],e.Args[2],e.Args.Length>3?e.Args[3]:null);Shutdown(0);}catch(Exception ex){System.IO.File.WriteAllText(e.Args[2]+".error.txt",ex.ToString());Shutdown(1);}return;}
        if(e.Args.Length>=2&&e.Args[0]=="--render-loot-preview"){LootCounterPreview.Render(e.Args[1]);Shutdown(0);return;}
        _controller=new();MainWindow=_controller.CreateOverlay();ShutdownMode=ShutdownMode.OnMainWindowClose;
        MainWindow.ContentRendered+=StartOnce;MainWindow.Show();
        void StartOnce(object? sender,EventArgs args){MainWindow.ContentRendered-=StartOnce;if(_controller.Preferences.AutoCapture)_controller.StartCapture();}
    }
    protected override void OnExit(ExitEventArgs e){_controller?.Dispose();base.OnExit(e);}
}
