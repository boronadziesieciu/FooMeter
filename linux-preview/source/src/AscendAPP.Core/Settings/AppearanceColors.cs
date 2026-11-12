namespace AscendAPP.Core.Settings;
public static class AppearanceColors
{
    public const string DefaultAccent="#F28DB8",DefaultPanel="#0B0A0D",DefaultLogo="#F28DB8";
    public static bool TryNormalize(string? input,out string value)
    {
        value="";if(input is null)return false;var s=input.Trim().TrimStart('#');
        if(s.Length==3)s=string.Concat(s.Select(c=>new string(c,2)));
        if(s.Length!=6||s.Any(c=>!Uri.IsHexDigit(c)))return false;value="#"+s.ToUpperInvariant();return true;
    }
    public static string Normalize(string? input,string fallback)=>TryNormalize(input,out var value)?value:fallback;
}
