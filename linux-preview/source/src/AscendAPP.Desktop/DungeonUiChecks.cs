using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Protocol;
namespace AscendAPP.Desktop;
internal static class DungeonUiChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var c=new MeterController(preview:true);c.Preferences.CaptureScope="Self and party";c.ApplyPreferences();
        var overlay=c.CreateOverlay();var t=DateTimeOffset.UtcNow;
        byte[] Join(params byte[][] values)=>values.SelectMany(x=>x).ToArray();
        byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
        byte[] U(uint n)=>BitConverter.GetBytes(n);
        byte[] Name(string n){var b=Encoding.UTF8.GetBytes(n);return Join([(byte)b.Length],b);}
        AionFrame F(ushort op,params byte[][] p)=>new(op,Join(p),false);
        AionFrame Zone(uint counter,uint zone=600001)=>F(0x2136,U(counter),U(zone));
        AionFrame Own(uint id)=>F(0x3336,V(id),[0,0],Name("FixtureHero"),[0x18,5],U(13),[1],U(50));
        AionFrame Ally(uint id)=>F(0x048d,V(900),U(14010000),V(id),[0x17,5],Name("FixtureAlly"));
        AionFrame Spawn()=>F(0x4136,V(900),[0,0,0],U(2400032),new byte[21],V(50000),V(100000),[100,0,0,0,100,0,0,0]);
        AionFrame Hit(uint id)=>F(0x0438,V(900),[4,0],V(id),U(14010000),[0,0],[1,0,0,0],V(20000),V(100));
        var seqs=new Dictionary<ushort,uint>();
        void Send(ushort port,int second,params AionFrame[] frames)
        {
            byte[] data=Join(frames.Select(f=>Join(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload)).ToArray());
            uint seq=seqs.GetValueOrDefault(port,1000u);var b=new byte[54+data.Length];
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);b[14]=0x45;
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+data.Length));b[23]=6;b[26]=10;b[29]=1;b[30]=10;b[33]=2;
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),port);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),seq);b[46]=0x50;b[47]=0x18;data.CopyTo(b,54);
            c.Pipeline.Ingest(1,b,"fixture-adapter",t.AddSeconds(second));seqs[port]=seq+(uint)data.Length;c.RefreshScreenAt(t.AddSeconds(second));
        }
        try
        {
            Send(50001,0,Zone(1),Own(17),F(0x0297,[0x18,5],Name("FixtureHero"),U(13),U(50),[0x17,5],Name("FixtureAlly"),U(13),U(50)),Ally(18),Spawn(),Hit(17),Hit(18));
            var hero=c.Model.OverlayRows.Single(r=>r.IsSelf);var ally=c.Model.OverlayRows.Single(r=>!r.IsSelf);
            Send(50001,1,Zone(2),Hit(17),Hit(18));
            check(c.Model.Latest?.TotalDamage==400&&c.Model.OverlayRows.Contains(hero)&&c.Model.OverlayRows.Contains(ally),"Actual compact bindings keep player rows and encounter damage through an internal dungeon teleport");
            Send(50002,2,Zone(1));
            check(c.Model.OverlayRows.Count==2&&c.Model.OverlayRows.All(r=>!r.HasCombat)&&c.Model.OverlayRows.Any(r=>r.Name=="FixtureAlly"),"World transfer keeps waiting party rows in the actual compact overlay");
            Send(50002,3,Own(27),Ally(28),Spawn(),Hit(27),Hit(28));
            check(c.Model.Latest?.TotalDamage==200&&c.Model.OverlayRows.Count==2&&c.Model.OverlayRows.All(r=>r.HasCombat),"Fresh dungeon combat IDs resume both players' displayed damage without another roster");
            Send(50002,4,Zone(2,1010),Own(37),Ally(38),Spawn(),Hit(38),Hit(37));
            check(c.Model.Latest?.TotalDamage==100&&c.Model.OverlayRows.Count==1&&c.Model.OverlayRows[0].IsSelf,"Dungeon exit removes carried party rows and excludes unconfirmed former-party damage");
        }
        finally{overlay.Close();}
    }
}
