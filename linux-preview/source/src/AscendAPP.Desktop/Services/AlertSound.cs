using System.IO;
using System.Media;
namespace AscendAPP.Desktop.Services;
public static class AlertSound
{
    public static void Play(double volume)
    {
        if(volume<=0)return;volume=Math.Clamp(volume,0,1);
        _=Task.Run(()=>{try{
            const int rate=22050,samples=4410;using var stream=new MemoryStream();using(var w=new BinaryWriter(stream,System.Text.Encoding.ASCII,true))
            {w.Write("RIFF"u8);w.Write(36+samples*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(samples*2);for(int i=0;i<samples;i++){double fade=Math.Min(1,i/220d)*Math.Min(1,(samples-i)/660d);w.Write((short)(Math.Sin(2*Math.PI*740*i/rate)*volume*10000*fade));}}
            stream.Position=0;using var player=new SoundPlayer(stream);player.PlaySync();
        }catch{}});
    }
}
