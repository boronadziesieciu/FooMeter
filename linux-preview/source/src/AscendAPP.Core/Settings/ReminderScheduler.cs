namespace AscendAPP.Core.Settings;
public sealed record ReminderAlert(Guid Id,string Name,int LeadMinutes,DateTimeOffset Due);
/// <summary>Local, user-defined reminders only. Never supplies a fabricated game schedule.</summary>
public sealed class ReminderScheduler
{
    private DateTimeOffset? _previous;
    private readonly HashSet<string> _fired=new();
    public IReadOnlyList<ReminderAlert> Poll(IEnumerable<EventReminder> reminders,DateTimeOffset now)
    {
        var from=_previous??now; _previous=now;
        if(now<from){_fired.Clear();return [];}
        var result=new List<ReminderAlert>();
        foreach(var r in reminders.Where(r=>r.Enabled))
        {
            double period=r.RepeatMinutes*60d;
            foreach(int lead in r.Leads())
            {
                var threshold=r.At.AddMinutes(-lead);
                if(period>0&&now>threshold)threshold=threshold.AddSeconds(Math.Floor((now-threshold).TotalSeconds/period)*period);
                // Skip old alerts after sleep/clock changes. Trigger only within five seconds of the threshold.
                if(threshold<=now&&threshold>=from&&now-threshold<=TimeSpan.FromSeconds(5))
                {
                    string key=$"{r.Id}:{threshold.UtcTicks}:{lead}";
                    if(_fired.Add(key)) result.Add(new(r.Id,r.Name,lead,threshold.AddMinutes(lead)));
                }
            }
        }
        if(_fired.Count>5000)_fired.Clear();
        return result;
    }
    public static DateTimeOffset? Next(EventReminder r,DateTimeOffset now)
    {
        if(!r.Enabled)return null;
        if(r.At>=now)return r.At;
        return r.RepeatMinutes>0?r.At.AddMinutes((Math.Floor((now-r.At).TotalMinutes/r.RepeatMinutes)+1)*r.RepeatMinutes):null;
    }
}
