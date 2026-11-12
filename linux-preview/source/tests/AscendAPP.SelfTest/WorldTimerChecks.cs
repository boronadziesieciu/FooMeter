using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Game;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Settings;
using AscendAPP.Core.Combat;

static class WorldTimerChecks
{
    static readonly DateTimeOffset T=DateTimeOffset.Parse("2026-10-25T00:59:59Z");
    static byte[] V(uint n){var b=new List<byte>();do{byte x=(byte)(n&127);n>>=7;b.Add((byte)(x|(n>0?128:0)));}while(n>0);return b.ToArray();}
    static byte[] U(uint n){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,n);return b;}
    static byte[] L(long n){var b=new byte[8];BinaryPrimitives.WriteInt64LittleEndian(b,n);return b;}
    static byte[] Float(float n){var b=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(b,n);return b;}
    static byte[] J(params byte[][] a)=>a.SelectMany(x=>x).ToArray();
    static byte[] Slot(int number,bool alive,long time,int extra=-1)=>J([(byte)(alive?1:0)],V((uint)(1010*100+number)),alive?J(Float(100),Float(-200),Float(0)):[],extra>=0?[(byte)extra]:[],L(time));
    static byte[] List(params byte[][] slots)=>J([0,0],U(1010),[(byte)slots.Length],J(slots),[0,0]);
    static AionFrame F(ushort op,byte[] p)=>new(op,p,false);
    static byte[] Character(){var name=Encoding.UTF8.GetBytes("FixtureHero");return J(V(17),[0,0,(byte)name.Length],name,[0x18,5],U(13),[1],U(50));}
    static byte[] Wire(AionFrame f)=>J(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload);
    static byte[] Packet(byte[] payload,uint sequence)
    {
        var b=new byte[54+payload.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);b[14]=0x45;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+payload.Length));b[23]=6;b[26]=10;b[29]=1;b[30]=10;b[33]=2;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),50001);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),sequence);b[46]=0x50;b[47]=0x18;payload.CopyTo(b,54);return b;
    }
    public static void Run(Action<bool,string> check)
    {
        var c=WorldTimerCatalog.Read(Path.Combine(AppContext.BaseDirectory,"data","field_bosses.json"),Path.Combine(AppContext.BaseDirectory,"data","recurring_events.json"));
        check(c.MapId==1010&&c.SlotCount==24&&c.Bosses.Select(x=>x.Name).SequenceEqual(new[]{"Eternal Gartua","Silent Dartan","Soul Ruler Kashapa","High Commander Lagta"}),"Packaged four-boss catalogue matches the screenshot's names and explicit Verteron map");
        check(c.Bosses.Select(x=>x.Slot).SequenceEqual(new[]{21,22,23,24})&&c.Bosses.Select(x=>x.Npc).SequenceEqual(new[]{2101074,2101120,2101122,2101131}),"Boss mapping uses pinned NPC/slot identities rather than HP, distance or screenshot countdowns");
        var shugo=c.Events.Single(x=>x.Id=="shugo_festival");var rift=c.Events.Single(x=>x.Id=="spacetime_rift");
        check(EventTimetable.Evaluate(shugo,T).Start==T.AddSeconds(1),"Shugo countdown follows the next hourly UTC slot");
        var start=DateTimeOffset.Parse("2026-10-25T03:00:00Z");
        check(EventTimetable.Evaluate(rift,start.AddTicks(-1))is{InWindow:false,Start:var next}&&next==start,"A tick before a rift start cannot round into an early active window");
        check(EventTimetable.Evaluate(rift,start).InWindow&&EventTimetable.Evaluate(rift,start).End==start.AddMinutes(10),"Exact start enters the table's ten-minute window");
        check(!EventTimetable.Evaluate(rift,start.AddMinutes(10)).InWindow&&EventTimetable.Evaluate(rift,start.AddMinutes(10)).Start==start.AddHours(3),"Window end is exclusive and advances to the next slot");
        check(EventTimetable.Evaluate(rift,T,60).Start==T.AddSeconds(1)&&EventTimetable.Evaluate(rift,T,-60).Start==T.AddHours(1).AddSeconds(1),"Rift calibration shifts its UTC phase in either direction");
        check(EventTimetable.Evaluate(rift,T).Start==EventTimetable.Evaluate(rift,T.ToOffset(TimeSpan.FromHours(2))).Start,"Device timezone and daylight-saving representation do not shift UTC schedule instants");
        check(c.Events.Single(x=>x.Id=="dimensional_invasion").Possible,"Invasion slots remain explicitly possible instead of guaranteed events");
        check(WorldTimerCatalog.Read("missing-boss-table","missing-event-table")is{Bosses.Count:0,Events.Count:0},"Missing world-timer data fails closed without invented schedules");

        long due=T.AddHours(6).ToUnixTimeMilliseconds();
        byte[] data=List(Slot(21,false,due),Slot(22,true,T.ToUnixTimeMilliseconds(),7),Slot(23,false,0),Slot(24,false,due+1000));
        check(FieldBossPacketReader.TryRead(data,T,out var list)&&list is{MapId:1010,Slots.Count:4}&&list.Slots[1]is{Number:22,Alive:true},"Field-boss list parses dead/alive records and the documented optional extra byte");
        check(list!.Slots[0].TimeMs==due&&list.Slots[2].TimeMs==0,"Respawn deadline is the actual UTC wire timestamp; zero remains unknown");
        check(!FieldBossPacketReader.TryRead(data[..^5],T,out var bad)&&bad is null,"A truncated later boss cannot partially publish earlier countdowns");
        check(!FieldBossPacketReader.TryRead(J(data,[1]),T,out _),"Non-zero trailing data cannot masquerade as map-list padding");
        check(!FieldBossPacketReader.TryRead(List(Slot(21,false,due),Slot(21,false,due)),T,out _),"Duplicate place numbers cannot overwrite a different field boss");
        check(!FieldBossPacketReader.TryRead(List(Slot(0,false,due)),T,out _)&&!FieldBossPacketReader.TryRead(List(Slot(21,false,-1)),T,out _),"Out-of-map place and invalid timestamp are rejected");
        byte[] nan=List(Slot(22,true,due));BinaryPrimitives.WriteSingleLittleEndian(nan.AsSpan(11),float.NaN);
        check(!FieldBossPacketReader.TryRead(nan,T,out _),"Invalid living-boss coordinates cannot validate a corrupted list");
        var tracker=new FieldBossTracker(c);
        check(!tracker.Update(list)&&tracker.Snapshot(1304).All(x=>x.Observation is null),"An unbound map list cannot be attached to an unknown character server");
        check(tracker.Update(list with{ServerId=1304})&&tracker.Snapshot(1304)[0].Due==T.AddHours(6),"Exact server and map mapping creates a measured boss countdown");
        check(tracker.Snapshot(1304)[1]is{Observation.Alive:true,Due:null}&&tracker.Snapshot(1304)[2].Due is null,"Seen-alive and missing respawn time stay distinct from countdowns");
        check(tracker.Snapshot(2301).All(x=>x.Observation is null),"One server's boss times cannot appear on another server");
        check(!tracker.Update(list with{ServerId=1304,MapId=1110}),"Altgard slot order cannot populate the four Verteron bosses");
        check(!tracker.Update(list with{ServerId=1304,Slots=[new(25,false,due)]})&&tracker.Snapshot(1304)[0].Due==T.AddHours(6),"Invalid map slots cannot corrupt a previous valid snapshot");
        tracker.Update(list with{ServerId=1304,ObservedAt=T.AddSeconds(-1),Slots=[new(21,false,due+10000)]});
        check(tracker.Snapshot(1304)[0].Due==T.AddHours(6),"Delayed map observations cannot overwrite a newer deadline");
        check(tracker.Snapshot(1304)[0].IsDueAt(T.AddHours(7))&&tracker.Snapshot(1304)[0].Observation!.Alive==false,"Passed deadlines mean due, without fabricating a newly alive boss");

        string file=Path.Combine(AppContext.BaseDirectory,"world-"+Guid.NewGuid()+".json");
        try
        {
            tracker.Save(file);var loaded=new FieldBossTracker(c);loaded.Load(file);
            check(loaded.LastServer==1304&&loaded.Snapshot(1304)[0].Due==T.AddHours(6),"Observed map states persist with server ownership and absolute UTC deadlines");
            loaded.Clear();check(loaded.LastServer is null&&loaded.Snapshot(1304).All(x=>x.Observation is null),"Forget snapshots removes cached server boss observations");
            File.WriteAllText(file,"[null,{}]");loaded.Load(file);check(loaded.Snapshot(1304).All(x=>x.Observation is null),"Malformed cached rows cannot create named boss countdowns");
            var prefs=new MeterPreferences{ShowEventOverlay=true,EventOverlayLeft=44,EventOverlayTop=55,EventOverlayLocked=true,Left=99,RiftShiftMinutes=999,GameTimerLeadMinutes="5,1,99,-1,bad"};prefs.Normalize();PreferencesStore.Write(file,prefs);var restored=PreferencesStore.Read(file);
            check(restored.ShowEventOverlay&&restored.EventOverlayLocked&&restored.EventOverlayLeft==44&&restored.Left==99&&restored.RiftShiftMinutes==180&&restored.GameTimerLeadMinutes=="5,1,0","Timer visibility/position persist independently and calibration/alert inputs are bounded");
        }finally{File.Delete(file);File.Delete(file+".tmp");}
        var savedServer=new MeterPreferences{WorldTimerServerId=1304};PreferencesStore.Write(file,savedServer);
        try{check(PreferencesStore.Read(file).WorldTimerServerId==1304,"The last confirmed timer server survives preference save and reload");}
        finally{File.Delete(file);}
        savedServer.WorldTimerServerId=999;savedServer.Normalize();check(savedServer.WorldTimerServerId is null,"Invalid saved timer servers cannot select another server's observations");
        var keys=new MeterPreferences();check(HotkeyChord.TryParse(keys.EventsHotkey,out var key)&&key is not null&&new[]{keys.EventsHotkey,keys.HideHotkey,keys.ResetHotkey,keys.ClearHotkey,keys.CopyHotkey,keys.ClickThroughHotkey}.Distinct().Count()==6,"Timer toggle hotkey is valid and distinct from all meter actions");

        var d=new ExperimentalGlobalDecoder();int published=0;FieldBossList? observed=null;d.FieldBossesUpdated+=x=>{published++;observed=x;};
        d.Decode("game",F(0x2136,J(U(1),U(1010))),T);
        d.Decode("game",F(0x0191,data),T);
        check(published==0&&d.FieldBossLists==1&&d.UnboundFieldBossLists==1,"Decoder diagnoses a valid map list seen before character/server detection");
        d.Decode("game",F(0x3336,Character()),T);d.Decode("game",F(0x0191,data),T);
        check(published==1&&observed is{ServerId:1304,MapId:1010}&&d.FieldBossLists==2,"Own-character server is attached to accepted field-boss metadata");
        d.Decode("game",F(0x0191,data[..^5]),T);
        check(published==1,"Malformed map packets do not publish partial timer updates");
        var metadata=new List<FieldBossList>();var combat=new List<CombatEvent>();d=new();d.FieldBossesUpdated+=metadata.Add;
        var network=new NetworkPipeline(d,combat.Add);
        byte[] wire=J(Wire(F(0x3336,Character())),Wire(F(0x0191,data)));
        network.Ingest(1,Packet(wire[..9],1000),"adapter",T);network.Ingest(1,Packet(wire[9..],1009),"adapter",T);network.Ingest(1,Packet(wire[9..],1009),"adapter",T);
        check(metadata.Count==1&&combat.Count==0&&network.LiveDiagnostics is{FieldBossLists:1,UnboundFieldBossLists:0}&&network.Diagnostics.Errors==0,"Ethernet/split TCP/decoder metadata path produces one timer list and no fabricated combat events");
        var rng=new Random(83);for(int i=0;i<3000;i++){var bytes=new byte[rng.Next(0,150)];rng.NextBytes(bytes);FieldBossPacketReader.TryRead(bytes,T,out _);}
        check(true,"Malformed field-boss payload stress completes within bounded parser limits");
    }
}
