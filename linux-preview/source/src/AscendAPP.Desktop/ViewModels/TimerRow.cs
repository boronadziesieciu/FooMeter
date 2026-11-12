namespace AscendAPP.Desktop.ViewModels;
public sealed class TimerRow(string name,string remaining,double fraction,string icon="Timer"):NotifyModel
{
    private string _name=name,_remaining=remaining,_icon=icon;private double _fraction=fraction;
    public string Name {get=>_name;set=>Set(ref _name,value);}
    public string Remaining {get=>_remaining;set=>Set(ref _remaining,value);}
    public string Icon {get=>_icon;set=>Set(ref _icon,value);}
    public double Fraction {get=>_fraction;set=>Set(ref _fraction,value);}
}
public sealed record TicketRow(string Character,string Energy,string Detail,string Seen);
