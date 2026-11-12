using System.Text.RegularExpressions;
namespace AscendAPP.Core.Pets;
public sealed record LootTextLine(string Text,double X,double Y,double Width,double Height,float Confidence);
public sealed record PetLootRow(uint PetId,double X,double Y,double Height,string Text,bool Fresh,string Detail);
/// <summary>OCR locates names only. It never increments collection totals.</summary>
public sealed class LootRowMatcher
{
    private readonly (uint Id,string Name,string Key)[] _names;
    private static readonly Regex Soul=new(@"\bSou[lI1]\s*[:;]\s*",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Compiled);
    private static string Key(string s)=>new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    public LootRowMatcher(IReadOnlyDictionary<uint,string> names)=>_names=names.Select(x=>(x.Key,x.Value,Key(x.Value))).OrderByDescending(x=>x.Item3.Length).ToArray();
    public uint? Identify(string text)
    {
        if(text.Length>512||text.Contains('<')||text.Contains('>'))return null;var marker=Soul.Match(text);if(!marker.Success)return null;
        string value=Key(text[(marker.Index+marker.Length)..]);
        foreach(var item in _names)if(value.StartsWith(item.Key,StringComparison.Ordinal)){var rest=value[item.Key.Length..];if(rest.Length==0||rest.StartsWith("bound")||("bound".StartsWith(rest)&&rest.Length>=2)||rest.StartsWith("x")||char.IsDigit(rest[0]))return item.Id;}
        // Only a unique long prefix may survive the game's scrolling/truncated item text.
        var prefix=_names.Where(x=>value.Length>=6&&x.Key.StartsWith(value,StringComparison.Ordinal)).Take(2).ToArray();
        return prefix.Length==1?prefix[0].Id:null;
    }
    public IReadOnlyList<PetLootRow> Resolve(IEnumerable<LootTextLine> lines,Func<uint,PetProgress> progress,IReadOnlyList<PetPickup> gains,DateTimeOffset now)
    {
        var matched=lines.Where(l=>l is not null&&l.Confidence>=30&&double.IsFinite(l.X)&&double.IsFinite(l.Y)&&l.Height is >0 and <200).Select(l=>(Line:l,Id:Identify(l.Text))).Where(x=>x.Id.HasValue).OrderBy(x=>x.Line.Y).Take(12).ToArray();
        var result=new List<PetLootRow>();
        foreach(var group in matched.GroupBy(x=>x.Id!.Value)){
            var rows=group.OrderBy(x=>x.Line.Y).ToArray();
            var observed=gains.Where(g=>g.Id==group.Key&&g.At<=now&&now-g.At<=TimeSpan.FromSeconds(6)).OrderBy(g=>g.Sequence).TakeLast(rows.Length).ToArray();
            int offset=rows.Length-observed.Length;
            for(int i=0;i<rows.Length;i++){
                var line=rows[i].Line;var p=progress(group.Key);PetPickup? gain=i>=offset?observed[i-offset]:null;
                bool fresh=p.Confirmed;int? level=gain is {Confirmed:true}?gain.Level:p.Level,points=gain is {Confirmed:true}?gain.Points:p.Points;
                if(!p.Confirmed&&gain is not {Confirmed:true})fresh=false;
                string text=level is null?gain is not null?"+"+gain.Amount:"— / —":level==3?"MAX":points+" / "+PetProtocol.Required(level.Value);
                result.Add(new(group.Key,line.X,line.Y,line.Height,text,fresh,(gain is not null?"Observed soul gain":"Latest received collection progress")+(fresh?"":" · last known / needs sync")));
            }
        }
        return result.OrderBy(x=>x.Y).ToArray();
    }
}
