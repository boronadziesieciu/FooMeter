using AscendAPP.Core.Game;
namespace AscendAPP.Desktop.ViewModels;
public sealed class WorldTimerRow : NotifyModel
{
    public required string Id {get;init;}
    private string _name="",_detail="",_remaining="—",_icon="Timer",_tooltip="";
    public string Name {get=>_name;set=>Set(ref _name,value);}
    public string Detail {get=>_detail;set=>Set(ref _detail,value);}
    public string Remaining {get=>_remaining;set=>Set(ref _remaining,value);}
    public string Icon {get=>_icon;set=>Set(ref _icon,value);}
    public string Tooltip {get=>_tooltip;set=>Set(ref _tooltip,value);}
    public static string Countdown(DateTimeOffset due,DateTimeOffset now)
    {
        var t=TimeSpan.FromSeconds(Math.Max(0,Math.Ceiling((due-now).TotalSeconds)));
        return t.TotalDays>=1?$"{(int)t.TotalDays}d {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}":$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }
}
