using System.Text.Json;
using AscendAPP.Core.Settings;
using AscendAPP.Core.Storage;
static class UserDataChecks
{
    public static void Run(Action<bool,string> check)
    {
        bool parsed=PreferencesStore.TryParse("{\"CapturePolicyVersion\":1,\"ShowForceMapOverlay\":true,\"ForceMapSharePosition\":true,\"ForceMapHotkey\":\"Ctrl+Shift+F5\",\"ForceMapServer\":\"http://127.0.0.1:5187\",\"ShowEventOverlay\":true,\"TargetSkillsKeepFocus\":true,\"Trackers\":[{\"Name\":\"Old skill timer\"}],\"UiScale\":1.2,\"FuturePreference\":42}",out var oldMapProfile);
        check(parsed&&oldMapProfile.ShowEventOverlay&&oldMapProfile.UiScale==1.2,"Removing retired overlays preserves meter and event settings");
        string upgradedJson=JsonSerializer.Serialize(oldMapProfile);
        check(!upgradedJson.Contains("ForceMap",StringComparison.OrdinalIgnoreCase)&&!upgradedJson.Contains("TargetSkills",StringComparison.OrdinalIgnoreCase)&&!upgradedJson.Contains("Trackers",StringComparison.OrdinalIgnoreCase)&&upgradedJson.Contains("FuturePreference"),"Retired map and skill-tracker keys are discarded while unrelated future settings survive");
        string fixtureBase=Path.Combine(AppContext.BaseDirectory,"profile-fixtures");
        string root=Path.Combine(fixtureBase,"AscendAPP-profile-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string older=Path.Combine(root,"AscendAPP-older"),newer=Path.Combine(root,"AscendAPP-newer"),shared=Path.Combine(root,"shared");
        Directory.CreateDirectory(older);Directory.CreateDirectory(newer);
        try
        {
            var preferences=new MeterPreferences{CapturePolicyVersion=1,CaptureScope="Self only",AutoCapture=false,UiScale=1.2,
                OverlayOpacity=0.72,SmoothNumbers=false,HideNames=true,ShowEventOverlay=true,EventOverlayLeft=85,EventOverlayTop=95,
                HideHotkey="Ctrl+Alt+F10",
                Reminders=[new(){Name="Fixture reminder",At=DateTimeOffset.Parse("2026-10-23T12:00:00Z"),LeadMinutes="10,5"}]};
            PreferencesStore.Write(Path.Combine(older,"settings.json"),preferences);
            string original=File.ReadAllText(Path.Combine(older,"settings.json"));
            File.WriteAllText(Path.Combine(older,"characters.json"),"[]");
            File.WriteAllText(Path.Combine(older,"fieldbosses.json"),"[]");
            var first=UserDataStore.Open(shared,newer,()=>new[]{older});
            check(first.ImportedFrom==older&&File.Exists(first.SettingsPath),"First launch imports a valid older profile into the shared directory");
            var loaded=PreferencesStore.Read(first.SettingsPath);
            check(loaded.CaptureScope=="Self only"&&!loaded.AutoCapture&&loaded.UiScale==1.2&&loaded.OverlayOpacity==0.72&&
                !loaded.SmoothNumbers&&loaded.HideNames,"Migration preserves explicit capture, appearance and animation choices");
            check(loaded.ShowEventOverlay&&loaded.EventOverlayLeft==85&&loaded.HideHotkey=="Ctrl+Alt+F10"&&
                loaded.Reminders.Single().LeadMinutes=="10,5,0","Migration preserves timer visibility/position, hotkeys and reminders");
            check(File.ReadAllText(first.CharactersPath)=="[]"&&File.ReadAllText(first.FieldBossesPath)=="[]","Character-ticket and field-boss snapshots migrate beside settings");
            check(File.ReadAllText(Path.Combine(older,"settings.json"))==original,"Import leaves original settings untouched");
            loaded.UiScale=1.3;PreferencesStore.Write(first.SettingsPath,loaded);
            PreferencesStore.Write(Path.Combine(newer,"settings.json"),new MeterPreferences{UiScale=0.8});
            var second=UserDataStore.Open(shared,newer,()=>throw new Exception("Discovery must not run"));
            check(second.ImportedFrom is null&&PreferencesStore.Read(second.SettingsPath).UiScale==1.3,"A later build uses shared settings without scanning or overwriting from its local defaults");
            check(second.SettingsPath==first.SettingsPath&&second.CharactersPath==first.CharactersPath&&second.FieldBossesPath==first.FieldBossesPath,"Different releases resolve exactly the same three data paths");
            File.WriteAllText(first.CharactersPath,"[{\"Character\":\"KeepShared\"}]");
            UserDataStore.Open(shared,older);
            check(File.ReadAllText(first.CharactersPath).Contains("KeepShared"),"Existing shared ticket snapshots are not replaced by old copies");

            string nearby=Path.Combine(root,"nearby");Directory.CreateDirectory(nearby);
            string nested=Path.Combine(nearby,"AscendAPP-Zip","AscendAPP-Compact-Windows");
            Directory.CreateDirectory(nested);File.WriteAllText(Path.Combine(nested,"AscendAPP.exe"),"fixture");
            PreferencesStore.Write(Path.Combine(nested,"settings.json"),new MeterPreferences{UiScale=1.4});
            string unrelated=Path.Combine(nearby,"unrelated","AscendAPP-hidden-deep");Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated,"AscendAPP.exe"),"fixture");File.Copy(Path.Combine(nested,"settings.json"),Path.Combine(unrelated,"settings.json"));
            var discovered=UserDataStore.DiscoverLegacyFolders([nearby]);
            check(discovered.Contains(nested)&&!discovered.Contains(unrelated),"Discovery finds nested release folders while avoiding unrelated folder trees");
            check(UserDataStore.DiscoverLegacyFolders([nearby],maxDepth:1).Count==0&&
                UserDataStore.DiscoverLegacyFolders([nearby],maxDirectories:1).Count==0,"Legacy discovery respects depth and directory budgets");
            string bad=Path.Combine(nearby,"AscendAPP-invalid");Directory.CreateDirectory(bad);File.WriteAllText(Path.Combine(bad,"AscendAPP.exe"),"fixture");File.WriteAllText(Path.Combine(bad,"settings.json"),"{bad");
            File.SetLastWriteTimeUtc(Path.Combine(nested,"settings.json"),DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(Path.Combine(bad,"settings.json"),DateTime.UtcNow);
            var fallback=UserDataStore.Open(Path.Combine(root,"fallback"),Path.Combine(root,"no-local"),
                ()=>UserDataStore.OrderByRecency([nested,bad]));
            check(fallback.ImportedFrom==nested&&PreferencesStore.Read(fallback.SettingsPath).UiScale==1.4,"A newer invalid profile is skipped in favor of the latest valid one");

            string corrupt=Path.Combine(root,"corrupt");Directory.CreateDirectory(corrupt);
            File.WriteAllText(Path.Combine(corrupt,"settings.json"),"{preserve this corrupt file");
            var recovered=UserDataStore.Open(corrupt,newer);
            string backup=Directory.GetFiles(corrupt,"settings.json.backup-*").Single();
            check(File.ReadAllText(backup)=="{preserve this corrupt file"&&recovered.ImportedFrom==newer,"Invalid shared settings are backed up before recovery");
            string invalidSide=Path.Combine(root,"invalid-side");Directory.CreateDirectory(invalidSide);
            File.Copy(Path.Combine(older,"settings.json"),Path.Combine(invalidSide,"settings.json"));
            File.WriteAllText(Path.Combine(invalidSide,"characters.json"),"{bad");
            File.WriteAllText(Path.Combine(invalidSide,"fieldbosses.json"),"[]");
            var partial=UserDataStore.Open(Path.Combine(root,"partial"),invalidSide);
            check(File.Exists(partial.SettingsPath)&&!File.Exists(partial.CharactersPath)&&File.Exists(partial.FieldBossesPath)&&partial.Messages.Count>1,"Malformed optional snapshots do not block settings import or get silently copied");

            string future=Path.Combine(root,"future.json");
            File.WriteAllText(future,"""{"uiScale":1.1,"FuturePanel":{"Enabled":true},"Trackers":[{"Name":"Future","FutureColor":"mint"}],"Reminders":[{"Name":"Later","FutureSound":7}]}""");
            var futurePreferences=PreferencesStore.Read(future);PreferencesStore.Write(future,futurePreferences);
            using(var json=JsonDocument.Parse(File.ReadAllText(future)))
                check(futurePreferences.UiScale==1.1&&json.RootElement.GetProperty("FuturePanel").GetProperty("Enabled").GetBoolean()&&
                    json.RootElement.GetProperty("Reminders")[0].GetProperty("FutureSound").GetInt32()==7,"Unknown future settings and nested timer options survive a read/save cycle");
            string beforeImport=File.ReadAllText(first.SettingsPath);
            string message=first.ImportSettings(Path.Combine(older,"settings.json"));
            check(PreferencesStore.Read(first.SettingsPath).UiScale==1.2&&Directory.GetFiles(shared,"settings.json.backup-*").Any(p=>File.ReadAllText(p)==beforeImport),"Explicit legacy import backs up current shared preferences before replacement");
            bool rejected=false;try{first.ImportSettings(Path.Combine(bad,"settings.json"));}catch(InvalidDataException){rejected=true;}
            check(rejected&&PreferencesStore.Read(first.SettingsPath).UiScale==1.2,"An invalid manual import cannot overwrite current settings");
            rejected=false;try{first.ImportSettings(first.SettingsPath);}catch(InvalidDataException){rejected=true;}
            check(rejected,"Importing the already-active shared file is rejected without self-overwrite");

            string local=Path.Combine(root,"local");Directory.CreateDirectory(local);
            PreferencesStore.Write(Path.Combine(local,"settings.json"),new MeterPreferences{UiScale=1.05});
            var current=UserDataStore.Open(Path.Combine(root,"current-first"),local,()=>new[]{older});
            check(current.ImportedFrom==local&&PreferencesStore.Read(current.SettingsPath).UiScale==1.05,"Settings beside the launching app have priority on first migration");
            string oversized=Path.Combine(root,"oversized");Directory.CreateDirectory(oversized);
            File.WriteAllText(Path.Combine(oversized,"settings.json"),new string(' ',1_048_577));
            var bounded=UserDataStore.Open(Path.Combine(root,"bounded"),oversized,()=>new[]{older});
            check(bounded.ImportedFrom==older,"Oversized settings are skipped without preventing valid migration");
            bool emptyFallback=true;foreach(string text in new[]{"null","[]","42","{invalid"})
                emptyFallback&=!PreferencesStore.TryParse(text,out _);
            check(emptyFallback,"Non-object and malformed settings cannot masquerade as a valid shared profile");

            string raced=Path.Combine(root,"raced");Directory.CreateDirectory(raced);
            var competing=UserDataStore.Open(raced,Path.Combine(root,"no-local"),()=>
            {
                PreferencesStore.Write(Path.Combine(raced,"settings.json"),new MeterPreferences{UiScale=1.45});
                return new[]{older};
            });
            check(competing.ImportedFrom is null&&PreferencesStore.Read(competing.SettingsPath).UiScale==1.45,"A profile created during legacy discovery takes priority over an old copy");
            string atomic=Path.Combine(root,"atomic.json");PreferencesStore.Write(atomic,new MeterPreferences());
            Parallel.For(0,20,i=>PreferencesStore.Write(atomic,new MeterPreferences{DummySeconds=i}));
            check(PreferencesStore.TryRead(atomic,out var saved)&&saved.DummySeconds is >=0 and <20&&!Directory.GetFiles(root,"*.tmp").Any(),"Concurrent settings writes leave valid JSON and no temporary files");
            string destinationDirectory=Path.Combine(root,"destination-directory");Directory.CreateDirectory(destinationDirectory);
            bool failed=false;try{AtomicFile.WriteAllText(destinationDirectory,"test");}catch(Exception e)when(e is IOException or UnauthorizedAccessException){failed=true;}
            check(failed&&Directory.Exists(destinationDirectory)&&!Directory.GetFiles(root,"*.tmp").Any(),"Failed atomic commits preserve the destination and clean up temporary data");
            string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AscendAPP");
            check(UserDataStore.Preview().DirectoryPath==Path.GetFullPath(expected)&&!UserDataStore.DefaultDirectory.Contains("0.9."),"Default profile path is per-user and independent of release number");
        }
        finally
        {
            string resolved=Path.GetFullPath(root);
            if(!resolved.StartsWith(Path.GetFullPath(fixtureBase)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("AscendAPP-profile-test-"))throw new InvalidOperationException("Unexpected fixture cleanup path");
            Directory.Delete(resolved,true);
        }
    }
}
