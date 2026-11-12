namespace AscendAPP.Core.Combat;
public sealed record EncounterComparison(bool Comparable,string Reason,double? PartyDpsChange=null,double? DurationChange=null,double? SelfDpsChange=null);
/// <summary>Local recorded-fight comparisons. All rates use the full encounter duration.</summary>
public static class EncounterReview
{
    public static string Target(EncounterSnapshot f)=>f.Boss?.Name??f.TargetHealth?.Name??f.TargetName??"Encounter";
    private static int? Code(EncounterSnapshot f)=>f.Boss?.NpcCode??f.TargetHealth?.NpcCode;
    private static bool Valid(EncounterSnapshot f)=>double.IsFinite(f.DurationSeconds)&&f.DurationSeconds>=1&&f.TotalDamage>=0;
    public static double Rate(EncounterSnapshot f)=>Valid(f)?f.TotalDamage/f.DurationSeconds:0;
    public static IReadOnlyList<EncounterSnapshot> Search(IEnumerable<EncounterSnapshot> fights,string? query,bool hideNames=false)
    {
        string text=(query??"").Trim();
        return fights.Where(f=>!f.IsActive&&(text.Length==0||Target(f).Contains(text,StringComparison.OrdinalIgnoreCase)||f.Players.Any(p=>(!hideNames||p.IsSelf)&&p.Name.Contains(text,StringComparison.OrdinalIgnoreCase)||(p.ClassName?.Contains(text,StringComparison.OrdinalIgnoreCase)??false)))).ToArray();
    }
    public static bool SameTarget(EncounterSnapshot a,EncounterSnapshot b)
    {
        var x=Code(a);var y=Code(b);if(x.HasValue||y.HasValue)return x is >0&&x==y;
        return Target(a)!="Encounter"&&string.Equals(Target(a),Target(b),StringComparison.OrdinalIgnoreCase);
    }
    private static double? Change(double now,double prior)=>prior>0&&double.IsFinite(now)&&double.IsFinite(prior)?now/prior-1:null;
    public static EncounterComparison Compare(EncounterSnapshot selected,EncounterSnapshot baseline)
    {
        if(selected.Id==baseline.Id)return new(false,"Choose a different baseline encounter.");
        if(selected.IsActive||baseline.IsActive||!Valid(selected)||!Valid(baseline))return new(false,"Both encounters must be completed with a valid duration.");
        if(!SameTarget(selected,baseline))return new(false,"Choose encounters with the same target. Known NPC IDs must match.");
        var self=selected.Players.Where(p=>p.IsSelf).ToArray();var prior=baseline.Players.Where(p=>p.IsSelf).ToArray();double? personal=null;
        if(self.Length==1&&prior.Length==1&&self[0].Name==prior[0].Name&&self[0].ClassName==prior[0].ClassName&&self[0].Damage>=0&&prior[0].Damage>0)
            personal=Change(self[0].Damage/selected.DurationSeconds,prior[0].Damage/baseline.DurationSeconds);
        return new(true,"Recorded comparison; party, gear, phase and difficulty may differ. Name-only matches are less certain.",Change(Rate(selected),Rate(baseline)),Change(selected.DurationSeconds,baseline.DurationSeconds),personal);
    }
}
