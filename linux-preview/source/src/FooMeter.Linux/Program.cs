using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace FooMeter.Linux;

public static class Program
{
    public static bool Demo { get; private set; }
    public static string? SmokePath { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["--command", var command]) { CommandServer.Send(command); return 0; }
            if (args.Contains("--devices"))
            {
                foreach (string device in LinuxMeter.Devices().GetAwaiter().GetResult()) Console.WriteLine(device);
                return 0;
            }
            if(args is ["--ocr-image",var image]) {
                using var meter=new LinuxMeter(demo:true,seedDemo:false);
                string tsv=LootReader.Run("tesseract",CancellationToken.None,image,"stdout","-l","eng","--psm","6","tsv").GetAwaiter().GetResult();
                Console.WriteLine(JsonSerializer.Serialize(meter.ResolveLoot(LootReader.Parse(tsv))));return 0;
            }
            if (args is ["--replay", var path])
            {
                using var meter = new LinuxMeter(demo: true, seedDemo:false);
                meter.Replay(path).GetAwaiter().GetResult();
                Console.WriteLine(JsonSerializer.Serialize(meter.View(), new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            if (args.Contains("--self-test-linux"))
            {
                using var meter = new LinuxMeter(demo: true);
                var view = meter.View();
                if (view.Fight.TotalDamage != 20000 || view.Fight.Players.Count != 2 || view.Events.Count == 0 || view.Pets.Count == 0)
                    throw new InvalidOperationException("Linux model smoke check failed.");
                LinuxChecks.Run();
                Console.WriteLine("PASS Linux model: synthetic combat, bundled timers/pets, temporary demo profile.");
                return 0;
            }
            if (args is ["--self-test-capture-failure"])
            {
                string profile = Path.Combine(Path.GetTempPath(), "foometer-capture-check-" + Guid.NewGuid().ToString("N"));
                try
                {
                    using var meter = new LinuxMeter(profile: profile);
                    meter.Start("foometer_missing_interface").GetAwaiter().GetResult();
                    meter.CaptureCompletion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                    var view = meter.View();
                    if (meter.Running || !view.Status.Contains("Capture ended (exit ") || !view.Status.Contains("foometer_missing_interface"))
                        throw new InvalidOperationException("Capture failure diagnostics were lost: " + view.Status);
                    Console.WriteLine("PASS real dumpcap failure: helper exited, diagnostic retained, UI model responsive, clean shutdown.");
                }
                finally { if (Directory.Exists(profile)) Directory.Delete(profile, recursive: true); }
                return 0;
            }
            if (args.Contains("--help"))
            {
                Console.WriteLine("FooMeter Linux preview: --demo | --devices | --replay file.pcap | --self-test-linux | --smoke-ui result.json");
                return 0;
            }
            if (args is ["--smoke-ui", var result]) SmokePath = result;
            else if (args.Any(a => a != "--demo")) throw new ArgumentException("Unknown argument. Use --help.");
            Demo = args.Contains("--demo") || SmokePath is not null;
            if(!Demo) {try {CommandServer.Send("show");return 0;}catch(System.Net.Sockets.SocketException){} }
            // Avalonia 11 uses X11 here. Hyprland supplies this through XWayland.
            return AppBuilder.Configure<LinuxApp>().UsePlatformDetect().WithInterFont().LogToTrace()
                .StartWithClassicDesktopLifetime([]);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FooMeter: " + e.Message);
            return 1;
        }
    }
}

public sealed class LinuxApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MeterWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
