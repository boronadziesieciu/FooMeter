using System.Text;
using AscendAPP.Core.Combat;

namespace AscendAPP.Core.Storage;

public static class CombatCsv
{
    public static void Save(string path, EncounterSnapshot fight)
    {
        static string Esc(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("rank,player,class,damage,dps,healing,hps,share_percent,crit_percent,hits,raw_healing,excluded_healing,unverified_healing,legacy_raw_healing");
        int i = 0;
        foreach (var p in fight.Players)
        {
            i++;
            writer.WriteLine(string.Join(",", i.ToString(), Esc(p.Name), Esc(p.ClassName), p.Damage.ToString(),
                p.Dps.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), p.Healing.ToString(),
                p.Hps(fight.DurationSeconds).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                (p.Share * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                (p.CritRate * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), p.Hits.ToString(),p.RawHealing.ToString(),p.ExcludedHealing.ToString(),p.UnverifiedHealing.ToString(),p.LegacyHealing.ToString()));
        }
    }
}
