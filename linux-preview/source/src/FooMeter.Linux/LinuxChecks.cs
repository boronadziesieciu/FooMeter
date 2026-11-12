using AscendAPP.Core.Settings;
using AscendAPP.Core.Storage;
using System.Text.Json;

namespace FooMeter.Linux;

internal static class LinuxChecks
{
    public static void Run()
    {
        void Check(bool value,string label) {if(!value)throw new Exception("Linux check failed: "+label);Console.WriteLine("PASS "+label);}
        string profile=Path.Combine(Path.GetTempPath(),"foometer-linux-check-"+Guid.NewGuid().ToString("N"));
        try {
            using(var meter=new LinuxMeter(profile:profile)) {
                meter.Preferences.AccentColor="#12ABCD";meter.Preferences.Port="13329";meter.Preferences.PinnedPets.Add(1001);
                meter.Preferences.Reminders.Add(new(){Name="Test reminder",At=DateTimeOffset.Now.AddMinutes(10)});
                meter.SavePreferences(meter.Preferences);
            }
            using(var restored=new LinuxMeter(profile:profile)) {
                Check(restored.Preferences.AccentColor=="#12ABCD"&&restored.Preferences.Port=="13329"&&restored.Preferences.PinnedPets.Contains(1001)&&restored.Preferences.Reminders.Count==1,"Linux profile persists appearance, capture, pins and reminders");
            }
            using var demo=new LinuxMeter(demo:true);
            var view=demo.View();Check(view.History.Count==2,"Demo history provides two completed encounters");
            string csv=Path.Combine(profile,"fight.csv"),json=Path.Combine(profile,"fight.json");
            CombatCsv.Save(csv,view.Fight);EncounterHistoryStore.Export(json,view.History[0]);
            Check(File.ReadAllLines(csv).Length==3&&File.ReadAllText(json).Contains("Demo player"),"CSV and JSON export preserve player records");
            Check(Ui.Share(view.Fight,true).Contains('\n')&&!Ui.Share(view.Fight,false,true).Contains("Demo party member"),"Chat formats and name privacy apply");
            string tsv="level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n5\t1\t1\t1\t1\t1\t0\t10\t40\t20\t90\tSoul:\n5\t1\t1\t1\t1\t2\t45\t10\t50\t20\t91\tBlack\n5\t1\t1\t1\t1\t3\t100\t10\t60\t20\t92\tDragon\n";
            var lines=LootReader.Parse(tsv);var rows=demo.ResolveLoot(lines);
            Check(lines.Count==1&&lines[0].Text=="Soul: Black Dragon"&&rows.Count==1&&rows[0].PetId==1001&&rows[0].Text=="— / —","OCR groups TSV words and locates pets without inventing totals");
            Check(LootReader.Parse("malformed\n5\tbad\n").Count==0,"OCR rejects malformed rows");
        } finally {if(Directory.Exists(profile))Directory.Delete(profile,true);}
    }
}
