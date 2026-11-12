namespace AscendAPP.Core.Settings;
public sealed record HotkeyChord(uint Modifiers,uint VirtualKey)
{
    public static bool TryParse(string? text,out HotkeyChord? chord)
    {
        chord=null;if(string.IsNullOrWhiteSpace(text)||text=="None")return true;
        var parts=text.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length<2)return false;uint mods=0;
        foreach(var m in parts[..^1]){uint bit=m.ToLowerInvariant() switch{"ctrl" or "control"=>2,"alt"=>1,"shift"=>4,"win" or "windows"=>8,_=>0};if(bit==0||(mods&bit)!=0)return false;mods|=bit;}
        string key=parts[^1].ToUpperInvariant();uint vk=0;
        if(key.Length==1&&char.IsAsciiLetterOrDigit(key[0]))vk=key[0];
        else if(key.StartsWith('F')&&int.TryParse(key[1..],out int f)&&f is >=1 and <=24)vk=(uint)(111+f);
        else vk=key switch{"BACK" or "BACKSPACE"=>8,"DELETE"=>46,"HOME"=>36,"END"=>35,"INSERT"=>45,"PAGEUP" or "PRIOR"=>33,"PAGEDOWN" or "NEXT"=>34,"SPACE"=>32,"UP"=>38,"DOWN"=>40,"LEFT"=>37,"RIGHT"=>39,_=>0};
        if(vk==0||mods==0)return false;chord=new(mods,vk);return true;
    }
}
