using System.Buffers.Binary;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AscendAPP.Core.Combat;
namespace AscendAPP.Desktop;
internal static class AutomaticScoreUiChecks
{
    static IEnumerable<DependencyObject> Children(DependencyObject root){yield return root;foreach(var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var x in Children(c))yield return x;}
    static byte[] J(params byte[][] pieces)=>pieces.SelectMany(x=>x).ToArray();
    static byte[] U(int n)=>BitConverter.GetBytes(n);
    static byte[] S(int n)=>BitConverter.GetBytes((ushort)n);
    static byte[] Q(ulong n)=>BitConverter.GetBytes(n);
    static byte[] N(string text){var b=Encoding.UTF8.GetBytes(text);return J([(byte)b.Length],b);}
    static byte[] V(uint value){var bytes=new List<byte>();do{byte b=(byte)(value&127);value>>=7;bytes.Add((byte)(b|(value>0?128:0)));}while(value>0);return bytes.ToArray();}
    static byte[] Power(int cp){var b=J(Q((ulong)cp),Q((ulong)cp+25),[5],new byte[15]);for(int i=0;i<5;i++)b[17+i*3]=(byte)(i+1);return b;}
    static byte[] Own(int gs,int cp,uint id=17)=>J(V(id),[0,0],N("AutomaticHero"),S(1318),U(9),[1],U(45),U(gs),U(gs),new byte[11],Power(cp));
    static byte[] Member(string name,int server,int gs,int cp,int slot)=>J([12,(byte)slot],Q(((ulong)server<<48)|123UL),N(name),U(9),U(45),U(gs),S(server),S(server),[4],Q((ulong)cp),[0,0,1]);
    static byte[] Packet(byte[] data,uint seq)
    {
        var b=new byte[54+data.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);b[14]=0x45;BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+data.Length));b[23]=6;b[26]=10;b[29]=1;b[30]=10;b[33]=2;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),50001);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),seq);b[46]=0x50;b[47]=0x18;data.CopyTo(b,54);return b;
    }
    public static void Run(Action<bool,string> check)
    {
        using var c=new MeterController(preview:true);var window=c.CreateOverlay();var settings=new SettingsWindow(c);uint seq=1000;var at=DateTimeOffset.UtcNow;
        void Send(byte[] body){var frame=J(V((uint)body.Length+4),body);c.Pipeline.Ingest(1,Packet(frame,seq),"automatic-score-ui",at);seq+=(uint)frame.Length;}
        try
        {
            Send(J([0x33,0x36],Own(3141,161803)));c.RefreshScreenAt(at);
            check(c.Model.SelfGearScore=="3,141"&&c.Model.SelfCombatPower=="161.8K"&&c.Model.SelfRatingInfo.Contains("161,803")&&c.Model.SelfRatingInfo.Contains("0x3336"),"Actual TCP own record reaches compact footer with exact CP in its tooltip");
            check(!c.Model.HasRows&&!c.CaptureRunning&&!c.NativePositionLookupAllowed,"Automatic idle scores need no damage event, live capture or process-memory lookup in tests");
            Send(J([2,0x97],new byte[24],Member("AutomaticHero",1318,3150,162500,1),Member("AutomaticParty",1319,2901,147123,2)));c.RefreshScreenAt(at);
            check(c.Model.Rows.Any(r=>r.Name=="AutomaticParty"&&r.GearScore=="2,901"&&r.CombatPower=="147.1K"&&!r.HasCombat),"Actual party TCP supplies scores before combat with no invented damage");
            check(c.Model.SelfGearScore=="3,150"&&c.Model.SelfCombatPower=="162.5K"&&c.Model.SelfRatingInfo.Contains("0x0297"),"An observed party update refreshes own scores through normal overlay bindings");
            c.ObserveIdentity(new(18,"AutomaticParty","Templar",false,1319));
            c.OnCombat(new(at,17,"AutomaticHero",900,"Training dummy",12010000,100,CombatKind.Damage,ActorClass:"Templar",IsSelf:true,TargetNpcCode:2400032));
            c.OnCombat(new(at.AddSeconds(1),18,"AutomaticParty",900,"Training dummy",12010000,70,CombatKind.Damage,ActorClass:"Templar",TargetNpcCode:2400032));c.RefreshScreenAt(at.AddSeconds(1));
            check(c.Model.Rows.Any(r=>r.IsSelf&&r.GearScore=="3,150")&&c.Model.Rows.Any(r=>r.Name=="AutomaticParty"&&r.CombatPower=="147.1K"&&r.HasCombat),"Automatic roster values remain bound when placeholder rows become real DPS rows");
            settings.SelectPage("Character");var panel=(FrameworkElement)settings.Content;panel.Measure(new Size(900,660));panel.Arrange(new Rect(0,0,900,660));panel.UpdateLayout();
            check(Children(settings).OfType<TextBlock>().Any(t=>t.Text=="3,150")&&!Children(settings).OfType<TextBox>().Any(),"Automatic Character settings renders observed scores with no manual-entry controls");
            Send(J([0x21,0x36],U(1),U(1010)));Send(J([0x33,0x36],Own(3160,164000,27)));c.RefreshScreenAt(at.AddSeconds(2));
            check(c.Model.SelfGearScore=="3,160"&&c.Model.SelfCombatPower=="164.0K","Teleport and fresh own record update scores after the world actor ID changes");
            check(c.DebugText.Contains("Own records:")&&c.DebugText.Contains("party scores:")&&!c.DebugText.Contains("not decoded"),"Debug exposes automatic own and party source counts");
        }
        finally{settings.Close();window.Close();}
    }
}
