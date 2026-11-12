using System.Buffers.Binary;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Settings;
static class CompactSettingsChecks
{
    public static void Run(Action<bool,string> check)
    {
        var t=DateTimeOffset.Parse("2026-10-23T12:00:00Z");
        var p=new MeterPreferences{UiScale=9,OverlayOpacity=double.NaN,UpdateMs=1,IdleSeconds=9999,VisiblePlayers=-1,EventVolume=-7,Targeting="invalid"};p.Normalize();
        check(p.UiScale==1.5&&p.OverlayOpacity==0.96&&p.UpdateMs==100&&p.IdleSeconds==600&&p.VisiblePlayers==1&&p.EventVolume==0,"Invalid saved display values are bounded and finite");
        check(p.Targeting=="Bosses and dummies"&&p.CaptureScope=="Self and party"&&p.PartyOnly,"Unknown setting choices recover to usable defaults");
        var events=new MeterPreferences{UiScale=1.2,OverlayOpacity=.7,EventOverlayScale=double.NaN,EventOverlayOpacity=double.PositiveInfinity};events.Normalize();
        check(events.EventOverlayScale is null&&events.EventOverlayOpacity is null&&events.UiScale==1.2&&events.OverlayOpacity==.7,"Invalid independent event appearance falls back to existing meter preferences");
        events.EventOverlayScale=9;events.EventOverlayOpacity=.01;events.Normalize();check(events.EventOverlayScale==1.5&&events.EventOverlayOpacity==.2,"Independent event appearance is bounded to supported scale and opacity");
        p.DummyNpcIds="42, 43;bad -1 42";check(p.DummyIds().SetEquals(new[]{42,43}),"Dummy IDs require positive explicit numeric identities");
        string file=Path.Combine(AppContext.BaseDirectory,"AscendAPP-"+Guid.NewGuid()+".json");
        try{
            p=new(){AutoCapture=false,HideNames=true,HideHotkey="Ctrl+Shift+F9",Reminders=[new(){Name="Custom boss",At=t,LeadMinutes="10,5"}]};
            PreferencesStore.Write(file,p);var loaded=PreferencesStore.Read(file);
            check(!loaded.AutoCapture&&loaded.HideNames&&loaded.HideHotkey=="Ctrl+Shift+F9"&&loaded.Reminders.Single().At==t,"Settings, shortcuts and offset-aware reminders round-trip");
            File.WriteAllText(file,"{invalid");check(PreferencesStore.Read(file).AutoCapture,"Corrupt preferences recover without preventing startup");
            File.WriteAllText(file,"""{"Trackers":null,"Reminders":null,"DummyNpcIds":null}""");check(PreferencesStore.Read(file).Reminders.Count==0,"Null optional collections recover without a crash");
        }finally{File.Delete(file);File.Delete(file+".tmp");}
        check(HotkeyChord.TryParse("Ctrl+Alt+O",out var chord)&&chord is {Modifiers:3,VirtualKey:79},"Global hotkey parser preserves Windows modifier and key codes");
        check(HotkeyChord.TryParse("Ctrl+Shift+F12",out chord)&&chord is {Modifiers:6,VirtualKey:123},"Function-key chords parse independently of display text");
        check(HotkeyChord.TryParse("None",out chord)&&chord is null,"A cleared shortcut is actually disabled");
        check(!HotkeyChord.TryParse("Ctrl+Ctrl+X",out _)&&!HotkeyChord.TryParse("X",out _)&&!HotkeyChord.TryParse("Ctrl+Bogus",out _),"Duplicate modifiers, bare letters and unknown keys are rejected");
        var defaults=new MeterPreferences();var defaultKeys=new[]{defaults.HideHotkey,defaults.ResetHotkey,defaults.ClearHotkey,defaults.CopyHotkey,defaults.ClickThroughHotkey};
        check(defaultKeys.All(k=>HotkeyChord.TryParse(k,out var key)&&key is not null)&&defaultKeys.Distinct().Count()==5,"Default recovery and action shortcuts are valid, distinct function-key chords");
        var filter=new CaptureFilter();
        CombatEvent Hit(uint actor,int npc=1,bool boss=true)=>new(t,actor,"Hero",99,"Target",10,100,CombatKind.Damage,TargetNpcCode:npc,TargetIsBoss:boss);
        check(!filter.Accept(Hit(2),"Self and party",new HashSet<int>()),"Strict party mode excludes unknown membership rather than nearby strangers");
        check(filter.Accept(Hit(1)with{IsSelf=true},"Self and party",new HashSet<int>()),"Strict party mode retains explicit self without a roster");
        filter.Identity(new(2,"Ally","Ranger",false,1304));filter.Identity(new(3,"Ally","Ranger",false,2301));filter.Party(new([new("Ally",1304)],t));
        check(filter.Accept(Hit(2),"Self and party",new HashSet<int>())&&!filter.Accept(Hit(3),"Self and party",new HashSet<int>()),"Capture filter distinguishes same-named players by server");
        check(filter.Accept(Hit(2)with{Time=t.AddSeconds(31)},"Self and party",new HashSet<int>()),"An unchanged roster continues granting only verified membership");
        check(!filter.Accept(Hit(1,boss:false)with{IsSelf=true},"Self and party",new HashSet<int>())&&filter.Accept(Hit(1,42,boss:false)with{IsSelf=true},"Self and party",new HashSet<int>{42})&&filter.Accept(Hit(1,boss:true)with{IsSelf=true},"Self and party",new HashSet<int>()),"Boss-only targeting includes explicit dummy IDs and known bosses");
        filter.Reset();check(!filter.Accept(Hit(2),"Self and party",new HashSet<int>()),"Connection changes clear old party membership");
        var e=new CombatEngine{IdleTimeoutSeconds=30};e.Add(Hit(1)with{Time=t});e.Add(Hit(1)with{Time=t.AddSeconds(10),Amount=2100});
        check(e.Snapshot(t.AddSeconds(20)).Players.Single().Dps==110,"Encounter DPS includes time between and after attacks");
        e.ActiveDps=true;check(e.Snapshot(t.AddSeconds(20)).Players.Single().Dps==220,"Active DPS divides by first-to-last damage span");
        check(e.Snapshot(t.AddSeconds(39)).IsActive&&!e.Snapshot(t.AddSeconds(40)).IsActive,"Configured idle timeout changes the actual encounter boundary");
        var finished=new CombatEngine{IdleTimeoutSeconds=5};finished.Add(Hit(1));finished.Add(Hit(1)with{Time=t.AddSeconds(1)});finished.Add(Hit(2)with{Time=t.AddSeconds(4)});
        finished.Snapshot(t.AddSeconds(10));finished.ActiveDps=true;
        check(finished.Snapshot(t.AddSeconds(11)).Players.Single(p=>p.ActorId==1).Dps==200,"DPS mode changes recalculate a completed fight from stored active durations");
        finished.ActiveDps=false;check(finished.Snapshot(t.AddSeconds(11)).Players.Single(p=>p.ActorId==1).Dps==50,"Completed fights can return to encounter DPS without losing raw damage");
        var bossEngine=new CombatEngine{IdleTimeoutSeconds=5,KeepBossOpen=true};bossEngine.Add(Hit(1,boss:true)with{TargetCurrentHp=800,TargetMaxHp=1000});
        check(bossEngine.Snapshot(t.AddSeconds(60)).IsActive,"Boss-reset mode keeps an observed boss segment open across idle mechanics");bossEngine.Finish(t.AddSeconds(61));
        check(!bossEngine.Snapshot(t.AddSeconds(62)).IsActive&&bossEngine.History.Count==1,"Explicit boss finish archives once and closes the segment");bossEngine.ClearHistory();check(bossEngine.History.Count==0,"Clear-meter history action removes archived segments");
        var scheduler=new ReminderScheduler();var r=new EventReminder{Name="My event",At=t.AddMinutes(10),LeadMinutes="5,1,5,99,bad",RepeatMinutes=30};
        check(r.Leads().SequenceEqual(new[]{5,1,0}),"Reminder lead times are bounded, deduplicated and include start");
        scheduler.Poll([r],t.AddMinutes(4).AddSeconds(59));var alerts=scheduler.Poll([r],t.AddMinutes(5));
        check(alerts.Count==1&&alerts[0].LeadMinutes==5&&alerts[0].Due==r.At,"Custom reminder fires at its configured lead time");
        check(scheduler.Poll([r],t.AddMinutes(5)).Count==0,"Repeated display refreshes never repeat a reminder alert");
        check(scheduler.Poll([r],t.AddHours(8).AddSeconds(20)).Count==0,"Wake from a long gap does not replay stale alerts");
        check(ReminderScheduler.Next(r,t.AddMinutes(11))==t.AddMinutes(40),"Repeating reminder computes the next actual occurrence");r.Enabled=false;check(ReminderScheduler.Next(r,t)is null,"Disabled events do not leave visible timer rows");
        var healing=new CombatEngine();healing.Add(new(t,1,"Healer",2,"Ally",17240000,750,CombatKind.Heal,HitTags.Critical));
        var healSkill=healing.Snapshot(t).Players.Single().Skills.Single();
        check(healSkill.HealingHits==1&&healSkill.HealingCriticalHits==1&&healSkill.MaxHeal==750&&healSkill.Damage==0,"Healing details count actual records and critical heals without inventing damage");
        byte[] U(uint n){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,n);return b;}
        byte[] tickets=[2,4,..U(101),3,12,..U(60000001),75,8];
        check(OdyleEnergyReader.TryReadTickets(tickets,t,out var counts)&&counts.Count==2&&counts.Single(x=>x.Id==101).Available==3&&counts.Single(x=>x.Id==60000001).Available==83,"Generic ticket snapshots retain independent observed balances");
        check(!OdyleEnergyReader.TryReadTickets(tickets[..^1],t,out counts)&&counts.Count==0,"A malformed ticket list cannot partially publish other character counts");
    }
}
