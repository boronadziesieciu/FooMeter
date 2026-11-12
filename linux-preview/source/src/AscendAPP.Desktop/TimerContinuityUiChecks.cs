using System.Buffers.Binary;
using System.IO;
using System.Text;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
using AscendAPP.Desktop.ViewModels;
namespace AscendAPP.Desktop;
internal static class TimerContinuityUiChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var c=new MeterController(preview:true);var panel=c.CreateEventOverlay();var t=DateTimeOffset.UtcNow;
        byte[] J(params byte[][] values)=>values.SelectMany(x=>x).ToArray();
        byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
        byte[] U(uint n)=>BitConverter.GetBytes(n);
        byte[] Name(string n){var b=Encoding.UTF8.GetBytes(n);return J([(byte)b.Length],b);}
        AionFrame F(ushort op,params byte[][] p)=>new(op,J(p),false);
        AionFrame Zone(uint counter,uint zone=1010)=>F(0x2136,U(counter),U(zone));
        AionFrame Own(uint id,ushort server=1304)=>F(0x3336,V(id),[0,0],Name("TimerFixture"),BitConverter.GetBytes(server),U(13),[1],U(50));
        AionFrame Bosses(int hours=1)=>F(0x0191,[0,0],U(1010),[4],J(Enumerable.Range(21,4).Select((n,i)=>J([0],V((uint)(101000+n)),BitConverter.GetBytes(t.AddHours(hours+i).ToUnixTimeMilliseconds()))).ToArray()),[0,0]);
        byte[] Packet(byte[] data,uint seq,ushort port,byte flags=0x18)
        {
            var b=new byte[54+data.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);b[14]=0x45;
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+data.Length));b[23]=6;b[26]=10;b[29]=1;b[30]=10;b[33]=2;
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),port);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),seq);b[46]=0x50;b[47]=flags;data.CopyTo(b,54);return b;
        }
        var seqs=new Dictionary<ushort,uint>();
        void Send(ushort port,int second,params AionFrame[] frames)
        {
            byte[] data=J(frames.Select(f=>J(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload)).ToArray());
            uint seq=seqs.GetValueOrDefault(port,1000u);c.Pipeline.Ingest(1,Packet(data,seq,port),"timer-fixture",t.AddSeconds(second));seqs[port]=seq+(uint)data.Length;c.RefreshWorldTimers(t.AddSeconds(second),true);
        }
        string Expected(int seconds)=>WorldTimerRow.Countdown(DateTimeOffset.FromUnixTimeMilliseconds(t.AddHours(1).ToUnixTimeMilliseconds()),t.AddSeconds(seconds));
        try
        {
            Send(50001,0,Zone(1),Own(17),Bosses(),Zone(2,1011));
            var boss=c.Model.FieldBossTimers[0];var events=c.Model.ScheduledEvents.ToArray();string provenance=boss.Tooltip;
            check(boss.Remaining==Expected(0)&&c.Preferences.WorldTimerServerId==1304,"A coalesced character/map/teleport packet retains its verified server before the first UI refresh");
            Send(50001,10,Zone(3,1010));
            check(c.Pipeline.LiveDiagnostics?.SelfName is null&&boss.Remaining==Expected(10)&&boss.Tooltip==provenance&&c.Model.WorldTimerClock.Contains("Saved S1304"),"A zone change with missing character identity keeps the original boss deadline and marks the saved server");
            Send(50001,11,Own(21));Send(50001,20,Zone(4,600001));
            check(boss.Remaining==Expected(20)&&ReferenceEquals(boss,c.Model.FieldBossTimers[0]),"Entering a dungeon retains the same field-boss row and absolute countdown");
            Send(50001,21,Own(31));Send(50001,30,Zone(5,600001));
            check(boss.Remaining==Expected(30)&&c.Pipeline.LiveDiagnostics?.InternalMoves==1,"An internal dungeon teleport does not restart field-boss timers");
            Send(50002,40,Zone(1,600001));
            check(boss.Remaining==Expected(40)&&c.Model.WorldTimerClock.Contains("Saved S1304"),"A new world socket keeps countdowns while combat identity is being rebound");
            c.Pipeline.Ingest(1,Packet([],seqs[50002],50002,0x11),"timer-fixture",t.AddSeconds(41));c.RefreshWorldTimers(t.AddSeconds(41),true);
            check(!c.Pipeline.LiveDiagnostics!.GameStream&&boss.Remaining==Expected(41),"Closing the old world socket cannot clear the selected timer server");
            Send(50003,42,Zone(1,600001));Send(50003,43,Own(41));Send(50003,50,Zone(2,1010));
            check(boss.Remaining==Expected(50)&&c.Model.WorldTimerClock.Contains("Saved S1304"),"Leaving the dungeon preserves world boss observations independently of party-scope cleanup");
            c.RefreshScreenAt(t.AddSeconds(50));c.RefreshWorldTimers(t.AddSeconds(50),true);
            var catalog=WorldTimerCatalog.Read(Path.Combine(AppContext.BaseDirectory,"data","field_bosses.json"),Path.Combine(AppContext.BaseDirectory,"data","recurring_events.json"));
            check(events.All(row=>ReferenceEquals(row,c.Model.ScheduledEvents.Single(r=>r.Id==row.Id)))&&
                events.All(row=>row.Remaining==WorldTimerRow.Countdown(EventTimetable.Evaluate(catalog.Events.Single(e=>e.Id==row.Id),t.AddSeconds(50)).CountdownAt,t.AddSeconds(50))),"Shugo and rift rows retain their UTC schedule phase through all zone/socket changes");
            Send(50003,60,Own(51,2301));
            check(c.Preferences.WorldTimerServerId==2301&&boss.Remaining=="—"&&!boss.Tooltip.Contains("Server 1304"),"A newly verified different server selects its own cache without reusing the previous server's times");
            Send(50003,61,Bosses(4));Send(50003,70,Own(61));
            check(boss.Remaining==Expected(70)&&boss.Tooltip==provenance,"Switching back restores the original server's unchanged deadline rather than restarting it");
            Send(50003,80,Zone(3,1011));Send(50003,81,Bosses(8));
            check(c.Pipeline.LiveDiagnostics?.UnboundFieldBossLists==1&&boss.Remaining==Expected(81)&&boss.Tooltip==provenance,"Unbound map packets during character refresh cannot overwrite a retained server snapshot");
            c.RefreshWorldTimers(t.AddHours(2),true);
            check(boss.Remaining=="Due · refresh"&&boss.Tooltip==provenance,"A retained countdown still expires as due without inventing a new spawn or respawn cycle");
            c.ForgetFieldBosses();check(c.Model.FieldBossTimers.All(r=>r.Remaining=="—"),"The explicit Forget snapshots action still removes saved boss timers");
        }
        finally{panel.CloseForExit();}
    }
}
