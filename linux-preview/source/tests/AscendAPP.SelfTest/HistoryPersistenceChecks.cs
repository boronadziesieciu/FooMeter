using AscendAPP.Core.Combat;
using AscendAPP.Core.Storage;
using System.Text.Json;
static class HistoryPersistenceChecks
{
    public static void Run(Action<bool,string> check)=>RunAsync(check).GetAwaiter().GetResult();
    static async Task RunAsync(Action<bool,string> check)
    {
        string fixtureBase=Path.Combine(AppContext.BaseDirectory,"profile-fixtures"),root=Path.Combine(fixtureBase,"AscendAPP-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string file=Path.Combine(root,"history.json");var t=DateTimeOffset.Parse("2026-11-02T12:00:00Z");var engine=new CombatEngine();
        try
        {
            EncounterSnapshot fight;
            using(var store=new EncounterHistoryStore(file))
            {
                engine.EncounterCompleted+=store.Enqueue;
                engine.Add(new(t,17,"Fixture hero",900,"Fixture boss",42,100,CombatKind.Damage,HitTags.Critical,"Fixture strike","Ranger",true,true,123));
                engine.Add(new(t.AddSeconds(5),17,"Fixture hero",900,"Fixture boss",42,200,CombatKind.Damage,SkillName:"Fixture strike",ActorClass:"Ranger",IsSelf:true,TargetIsBoss:true,TargetNpcCode:123));
                engine.FinishAtLastActivity(interrupted:true);fight=engine.History.Single();
                check(await store.FlushAsync()&&File.Exists(file),"Ending an encounter commits a versioned local archive");
                using var viewer=new EncounterHistoryStore(file);viewer.Enqueue(fight with{Id=Guid.NewGuid()});check(!await viewer.FlushAsync()&&!viewer.Clear()&&viewer.LastError.Contains("read-only"),"A second archive owner is read-only and cannot overwrite the first instance");
            }
            using(var reopened=new EncounterHistoryStore(file))
            {
                var loaded=reopened.Snapshot().Single();var restored=new CombatEngine();restored.RestoreHistory(reopened.Snapshot());
                check(loaded.TotalDamage==300&&loaded.Interrupted&&loaded.Players.Single().Skills.Single().Hits==2&&loaded.Players.Single().Timeline!.Count==2,"Restart restores partial labels, exact damage, skill breakdown and timeline");
                restored.ActiveDps=true;check(restored.History.Single().Players.Single().Dps==60,"Restored details retain active-time metadata for the chosen DPS view");
                reopened.Enqueue(fight);reopened.Enqueue(fight);await reopened.FlushAsync();check(reopened.Snapshot().Count==1,"Repeated completion notifications cannot duplicate a stored encounter ID");
                string export=Path.Combine(root,"export.json");EncounterHistoryStore.Export(export,loaded);using var exported=new EncounterHistoryStore(export);check(exported.Snapshot().Single().TotalDamage==300,"A selected-fight export contains a readable versioned archive with complete details");
                for(int i=0;i<85;i++)reopened.Enqueue(fight with{Id=Guid.NewGuid(),StartedAt=t.AddMinutes(i+1)});
                await reopened.FlushAsync();check(reopened.Snapshot().Count==80&&reopened.Snapshot().Min(x=>x.StartedAt)==t.AddMinutes(6)&&new FileInfo(file).Length<=EncounterHistoryStore.MaxBytes,"Retention keeps the latest 80 fights and respects the file byte limit");
                var gone=reopened.Snapshot()[0].Id;reopened.Remove(gone);await reopened.FlushAsync();
            }
            using(var reopened=new EncounterHistoryStore(file)){check(reopened.Snapshot().Count==79,"Deleted encounters remain deleted after restart");reopened.Clear();await reopened.FlushAsync();}
            using(var empty=new EncounterHistoryStore(file))check(empty.Snapshot().Count==0,"Clearing saved history commits an empty archive without resurrecting old fights");
            File.WriteAllText(file,"{broken history");using(var recovery=new EncounterHistoryStore(file)){check(File.ReadAllText(file+".recovery")=="{broken history"&&recovery.Snapshot().Count==0,"Malformed history is preserved in a recovery copy without blocking startup");recovery.Enqueue(fight);await recovery.FlushAsync();}
            using(var repaired=new EncounterHistoryStore(file))check(repaired.Snapshot().Count==1,"New recording works after a damaged archive is recovered");
            File.WriteAllText(file,"{\"Version\":99,\"Fights\":[]}");string future=File.ReadAllText(file);using(var unknown=new EncounterHistoryStore(file)){unknown.Enqueue(fight);check(!await unknown.FlushAsync()&&File.ReadAllText(file)==future,"A future history schema is kept unchanged instead of being overwritten");}
            var invalid=fight with{Players=[fight.Players.Single() with{Name="Bad\nname"}]};
            check(!EncounterHistoryStore.Valid(invalid)&&!EncounterHistoryStore.Valid(fight with{DurationSeconds=double.NaN})&&!EncounterHistoryStore.Valid(fight with{TotalDamage=301}),"Corrupt labels, nonfinite durations and inconsistent totals are rejected");
            File.WriteAllText(file,JsonSerializer.Serialize(new{Version=1,Fights=new[]{fight,invalid}}));using(var partial=new EncounterHistoryStore(file))check(partial.Snapshot().Count==1&&File.Exists(file+".recovery"),"Valid encounters survive a file containing a corrupt record");
            string huge=Path.Combine(root,"huge.json");using(var bytes=File.Create(huge))bytes.SetLength(EncounterHistoryStore.MaxBytes+1L);using(var bounded=new EncounterHistoryStore(huge)){bounded.Enqueue(fight);check(!await bounded.FlushAsync()&&new FileInfo(huge).Length>EncounterHistoryStore.MaxBytes,"An oversized archive is not loaded into memory or silently replaced");}
            string boundedFile=Path.Combine(root,"bounded-write.json");var points=Enumerable.Range(0,86401).Select(i=>new DamagePoint(i,1)).ToArray();var large=fight with{Id=Guid.NewGuid(),DurationSeconds=86401,TotalDamage=86401,Players=[fight.Players.Single() with{Damage=86401,Timeline=points,Skills=[]}]};
            using(var writer=new EncounterHistoryStore(boundedFile)){writer.Enqueue(fight);writer.Enqueue(large);await writer.FlushAsync();using var json=JsonDocument.Parse(File.ReadAllText(boundedFile));check(new FileInfo(boundedFile).Length<=EncounterHistoryStore.MaxBytes&&json.RootElement.GetProperty("Fights").GetArrayLength()==1&&writer.Status.Contains("limits"),"Oversized fight details cannot inflate the bounded archive or evict a valid smaller fight silently");}
            string blocked=Path.Combine(root,"blocked.json");Directory.CreateDirectory(blocked);using(var failure=new EncounterHistoryStore(blocked)){failure.Enqueue(fight);check(!await failure.FlushAsync()&&failure.LastError.Contains("failed")&&failure.Snapshot().Count==1,"A failed disk commit retains the fight in memory and reports the failure");}
        }
        finally{string full=Path.GetFullPath(root);if(!full.StartsWith(Path.GetFullPath(fixtureBase)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected cleanup path");Directory.Delete(full,true);}
    }
}
