using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Settings;
using AscendAPP.Desktop;

static class Program
{
    static object Measure(string name,Action action,int iterations)
    {
        for(int i=0;i<40;i++)action();
        var passes=new List<object>();
        for(int pass=0;pass<3;pass++)
        {
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var watch=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++)action();
            watch.Stop();
            passes.Add(new{MillisecondsPerRefresh=watch.Elapsed.TotalMilliseconds/iterations,
                CpuMillisecondsPerRefresh=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalMilliseconds/iterations,
                BytesPerRefresh=(GC.GetAllocatedBytesForCurrentThread()-allocated)/(double)iterations});
        }
        return new{Name=name,Iterations=iterations,Passes=passes};
    }
    [STAThread]
    static void Main(string[] args)
    {
        var app=new App();app.InitializeComponent();
        var results=new List<object>();long expectedDamage=0,observedDamage=0;
        using(var idle=new MeterController(preview:true))results.Add(Measure("idle",idle.RefreshScreen,500));
        using(var fight=new MeterController(preview:true))
        {
            var ingest=(Action<CombatEvent,bool>)typeof(MeterController).GetMethod("OnCombat",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate(typeof(Action<CombatEvent,bool>),fight);
            var identity=(Action<AscendAPP.Core.Protocol.PlayerIdentity>)typeof(MeterController).GetMethod("ObserveIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate(typeof(Action<AscendAPP.Core.Protocol.PlayerIdentity>),fight);
            var roster=(Action<PartyRoster>)typeof(MeterController).GetMethod("ObserveParty",BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate(typeof(Action<PartyRoster>),fight);
            var start=DateTimeOffset.UtcNow.AddMinutes(-15);
            string[] names=["FixtureSelf","FixtureTwo","FixtureThree","FixtureFour"];
            for(int i=0;i<4;i++)identity(new((uint)(17+i),names[i],"Ranger",i==0,1304));
            roster(new(names.Select(n=>new PartyMember(n,1304,"Ranger",50)).ToArray(),start));
            for(int i=0;i<36000;i++)ingest(new(start.AddSeconds(i/40.0),(uint)(17+i%4),names[i%4],900,"Fixture boss",14010000+(i/4)%12,1000+i%100,CombatKind.Damage,ActorClass:"Ranger",IsSelf:i%4==0,TargetIsBoss:true),false);
            results.Add(Measure("15-minute 4-player fight / 36000 hits",fight.RefreshScreen,200));
            var shell=fight.CreateOverlay();var content=(FrameworkElement)shell.Content;
            void Layout(){fight.RefreshScreen();content.Measure(new Size(420,double.PositiveInfinity));content.Arrange(new Rect(0,0,420,content.DesiredSize.Height));content.UpdateLayout();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);}
            results.Add(Measure("same fight + WPF layout",Layout,100));
            int streamingCalls=0;
            void Streaming()
            {
                var at=DateTimeOffset.UtcNow;streamingCalls++;
                for(int i=0;i<4;i++)ingest(new(at,(uint)(17+i),names[i],900,"Fixture boss",14010000,40000+i,CombatKind.Damage,ActorClass:"Ranger",IsSelf:i==0,TargetIsBoss:true),false);
                Layout();
            }
            results.Add(Measure("same fight + new hits + WPF layout",Streaming,100));
            expectedDamage=37782000L+streamingCalls*160006L;observedDamage=fight.Model.Latest!.TotalDamage;
            if(expectedDamage!=observedDamage)throw new InvalidOperationException("Benchmark lost combat data");
            shell.Close();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
        File.WriteAllText(args[0],JsonSerializer.Serialize(new{Version=typeof(MeterController).Assembly.GetName().Version,Runtime=Environment.Version.ToString(),LogicalCpus=Environment.ProcessorCount,Synthetic=true,ExpectedDamage=expectedDamage,ObservedDamage=observedDamage,Results=results},new JsonSerializerOptions{WriteIndented=true}));
    }
}
