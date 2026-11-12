using System.Buffers.Binary;
using System.Text.Json;
using AscendAPP.Core.Game;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Settings;
internal static class LiveIssueChecks
{
    public static void Run(Action<bool,string> check)
    {
        var at=DateTimeOffset.Parse("2026-10-26T12:00:00Z");
        var entry=new ManualPlayerRating("Fixture",1304,3021,158416,at);
        var manual=entry.Fill(null,17,"Fixture",1304);
        check(manual is {GearScore:3021,CombatPower:158416,ManualGearScore:true,ManualCombatPower:true},"Explicit own scores remain distinguishable from observed game profiles");
        check(entry.Fill(null,17,"Other",1304)is null&&entry.Fill(null,17,"Fixture",2304)is null&&entry.Fill(null,17,"Fixture",null)is null,"Manual values never cross character or home-server boundaries");
        var observed=new PlayerRating(17,"Fixture",1304,3100,180000,at,"Observed");
        check(ReferenceEquals(observed,entry.Fill(observed,17,"Fixture",1304)),"Observed GS and CP always take priority over manual fallback");
        var partial=entry.Fill(observed with{CombatPower=null},17,"Fixture",1304);
        check(partial is{GearScore:3100,CombatPower:158416,ManualGearScore:false,ManualCombatPower:true},"Only an unavailable score uses the manual fallback");
        var preferences=JsonSerializer.Deserialize<MeterPreferences>(JsonSerializer.Serialize(new MeterPreferences{OwnScoreEntries=[entry,entry with{Name="Other",GearScore=-1}]}))!;
        preferences.Normalize();check(preferences.OwnScoreEntries.Count==1&&preferences.OwnScoreEntries[0]==entry,"Own score fallback survives settings roundtrip and rejects invalid imports");
        var store=new PlayerRatingStore();store.Observe(observed);
        check(store.Snapshot(at).Count==1&&store.Snapshot(at.AddHours(3)).Count==0,"Observed profile browser respects independent cache freshness");

        var decoder=new ExperimentalGlobalDecoder();var pipeline=new NetworkPipeline(decoder,_=>{});int outgoing=0;pipeline.SegmentObserved+=(_,_)=>outgoing++;
        // Deliberately valid plaintext-looking bytes in the outgoing direction must not enter the S2C decoder.
        byte[] body=[7,0x45,0x36,17];var packet=new byte[54+body.Length];
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(12),0x0800);packet[14]=0x45;BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(16),(ushort)(40+body.Length));packet[23]=6;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(34),50001);BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(36),13328);packet[46]=0x50;packet[47]=0x18;body.CopyTo(packet,54);
        pipeline.Ingest(1,packet,"fixture",at);
        check(outgoing==1&&pipeline.Frames==0&&pipeline.Segments==1&&pipeline.PayloadBytes==body.Length,"Outgoing world bytes stay outside plaintext decoding without plaintext frame scans");
        var forecast=new BossForecastTracker(new BossTimingCatalog(new Dictionary<int,int>()));
        CombatEvent Hit(DateTimeOffset t)=>new(t,17,"Fixture",900,"Training dummy",1,100,CombatKind.Damage,IsSelf:true,TargetIsTrainingDummy:true);
        forecast.Health(900,100,100,at);forecast.Add(Hit(at),true);forecast.Health(900,90,100,at);forecast.Health(900,100,100,at.AddSeconds(1));forecast.Add(Hit(at.AddSeconds(4)),true);
        check(forecast.Snapshot(at.AddSeconds(4),"Self only").DamagePerSecond==50,"Dummy regeneration does not discard admitted damage from the forecast");
        check(forecast.Snapshot(at.AddSeconds(60),"Self only",at.AddSeconds(4)).DamagePerSecond==50,"Completed encounter rates stay anchored to the actual combat end");
    }
}
