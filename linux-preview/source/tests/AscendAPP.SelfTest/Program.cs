using System.Buffers.Binary;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Network;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Storage;

int total = 0;
void Check(bool pass, string name)
{
    total++;
    if (!pass) throw new Exception($"FAILED: {name}");
    Console.WriteLine($"PASS {name}");
}

// Independent combat engine: attribution, DPS, crits, healing, lifecycle.
var engine = new CombatEngine();
var t0 = DateTimeOffset.Parse("2026-10-22T12:00:00+00:00");
engine.Add(new(t0, 1, "A", 999, "Boss", 10, 300, CombatKind.Damage, HitTags.Critical));
engine.Add(new(t0.AddSeconds(1), 2, "B", 999, "Boss", 11, 100, CombatKind.Damage));
engine.Add(new(t0.AddSeconds(2), 1, "A", 2, "B", 12, 80, CombatKind.Heal));
var snap = engine.Snapshot(t0.AddSeconds(2));
Check(snap.TotalDamage == 400 && snap.TotalHealing == 80 && snap.Players.Count == 2, "Combat totals");
Check(snap.Players[0].ActorId == 1 && snap.Players[0].Dps == 150 && snap.Players[0].CritRate == 1, "Player DPS and crit");
Check(snap.Players[0].Skills.Count == 2, "Skill breakdown");
Check(snap.TopHit == 300 && snap.TopHitBy == "A", "Verified top-hit aggregation for the overlay");
engine.Reset(t0.AddSeconds(3));
Check(engine.History.Count == 1 && engine.Snapshot(t0.AddSeconds(3)).Players.Count == 0, "Reset and history");
Check(engine.Snapshot(t0.AddSeconds(4)).TopHit == 0, "Top hit is cleared after reset");

// TCP stream sorting, overlap trimming, retransmission immunity.
var tcp = new TcpReassembler();
var output = new List<byte>();
tcp.ChunkReady += (_, data) => output.AddRange(data);
TcpSegment Seg(uint seq, params byte[] data) => new("127.0.0.1", 1111, "127.0.0.1", 2222, seq, data);
tcp.Push(Seg(100, 1, 2));
tcp.Push(Seg(105, 6, 7)); // gap
tcp.Push(Seg(102, 3, 4, 5));
tcp.Push(Seg(101, 2, 3)); // retransmission
tcp.Push(Seg(107, 8));
Check(output.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "TCP reorder + dedup");

// Varint frame with virtual 4-byte size field: v=body.Length+4.
static byte[] Frame(params byte[] body)
{
    uint length = (uint)body.Length + 4;
    var frame = new List<byte>();
    do
    {
        byte b = (byte)(length & 127);
        length >>= 7;
        if (length != 0) b |= 128;
        frame.Add(b);
    } while (length > 0);
    frame.AddRange(body);
    return frame.ToArray();
}
var reader = new AionFrameReader();
var found = new List<AionFrame>();
reader.FrameReady += found.Add;
var pair = Frame(0x04, 0x38, 0x01, 0x02).Concat(Frame(0x05, 0x38, 0x09)).ToArray();
reader.Feed(pair.AsSpan(0, 2));
Check(found.Count == 0, "Wait for incomplete frame");
reader.Feed(pair.AsSpan(2));
Check(found.Count == 2 && found[0].Opcode == 0x0438 && found[1].Opcode == 0x0538, "Frame decoding");

// Test TCP/IP extractor against a minimal valid Ethernet-IPv4-TCP packet.
var datagram = new byte[14 + 20 + 20 + pair.Length];
BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(12), 0x0800);
datagram[14] = 0x45;
BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(16), (ushort)(20 + 20 + pair.Length));
datagram[23] = 6; // TCP
datagram[26] = 192; datagram[27] = 168; datagram[28] = 1; datagram[29] = 2;
datagram[30] = 192; datagram[31] = 168; datagram[32] = 1; datagram[33] = 3;
BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(34), 50001);
BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(36), 5555);
BinaryPrimitives.WriteUInt32BigEndian(datagram.AsSpan(38), 1000);
datagram[46] = 0x50; // TCP header is 5*4 bytes
pair.AsSpan().CopyTo(datagram.AsSpan(54));
Check(TcpPacketExtractor.TryExtract(1, datagram, out var parsed) && parsed!.Payload.SequenceEqual(pair)
      && parsed.SourcePort == 50001 && parsed.DestinationPort == 5555, "Ethernet / IPv4 / TCP extraction");

var temp = Path.Combine(Path.GetTempPath(), "ascendapp-selftest-" + Guid.NewGuid().ToString("N") + ".jsonl");
try
{
    var demo = new CombatEvent(t0, 1, "A", 2, "Boss", 42, 12, CombatKind.Damage, HitTags.Critical);
    JsonlCombatSession.Write(temp, [demo]);
    Check(JsonlCombatSession.Read(temp) is [{ SkillId: 42, Amount: 12, Tags: HitTags.Critical }], "JSONL round-trip");
}
finally { File.Delete(temp); }

// Verified metadata in normalized fixtures: no inference from arbitrary names or damage.
var enhanced = new CombatEngine();
enhanced.Add(new CombatEvent(t0, 17, "Hero", 900, "Balhash", 42, 1200,
    CombatKind.Damage, HitTags.Critical, SkillName: "Radiant Strike", ActorClass: "Sorcerer",
    IsSelf: true, TargetIsBoss: true, TargetNpcCode: 42001,
    TargetCurrentHp: 7800, TargetMaxHp: 9000));
var enhancedSnapshot = enhanced.Snapshot(t0.AddSeconds(1));
Check(enhancedSnapshot.Boss is { Name: "Balhash", NpcCode: 42001 } &&
      enhancedSnapshot.Boss.HealthFraction is > 0.86 and < 0.87, "Verified boss name/code/HP fixture");
Check(enhancedSnapshot.Players[0].ClassName == "Sorcerer" && enhancedSnapshot.Players[0].IsSelf &&
      enhancedSnapshot.Players[0].Skills[0].Name == "Radiant Strike", "Class, local player, skill name from fixture");
Check(enhancedSnapshot.Players[0].Timeline is [{ Second: 0, Damage: 1200 }], "Per-second damage timeline");
var unknown = new CombatEngine();
unknown.Add(new CombatEvent(t0, 17, "Hero", 901, "Generic enemy", 42, 1200, CombatKind.Damage));
Check(unknown.Snapshot(t0.AddSeconds(1)).Boss is null, "Ordinary targets never guessed as bosses");
var idle = new CombatEngine();
idle.Add(new CombatEvent(t0, 33, "Tank", 909, "Boss target", 18, 10, CombatKind.Damage));
Check(!idle.Snapshot(t0.AddSeconds(13)).IsActive && idle.History.Count == 1,
      "Idle fight finalises automatically");
Check(idle.Snapshot(t0.AddSeconds(18)).Players.Count == 1 && idle.History.Count == 1,
      "Idle fight stays available without double archiving");
var healOnly = new CombatEngine();
healOnly.Add(new CombatEvent(t0, 3, "Cleric", 7, "Hero", 88, 100, CombatKind.Heal));
Check(healOnly.Snapshot(t0.AddSeconds(1)).Players[0].Healing == 100 &&
    healOnly.Snapshot(t0.AddSeconds(1)).TotalDamage == 0, "Pure healing totals");
var csv = Path.Combine(Path.GetTempPath(), "ascendapp-selftest-" + Guid.NewGuid().ToString("N") + ".csv");
try
{
    CombatCsv.Save(csv, enhancedSnapshot);
    Check(File.ReadAllText(csv).Contains("Hero") && File.ReadAllText(csv).Contains("1200"), "CSV export");
}
finally { File.Delete(csv); }

// Synthetic pipeline fixtures verify plumbing, never the Global wire protocol.
var pipeline = new NetworkPipeline(new UnverifiedGlobalDecoder(), _ => throw new Exception("Stub emitted event"));
pipeline.StartSession();
Check(pipeline.Diagnostics.Stage == "No packets", "Capture starts with no packets");
pipeline.Ingest(999, [1, 2]);
Check(pipeline.Diagnostics.Stage == "Packets without TCP payloads", "Unsupported link counted as packet");
var incomplete = datagram.ToArray();
incomplete[54] = 100; // valid candidate length, not enough bytes
pipeline.Ingest(1, incomplete);
Check(pipeline.Diagnostics.Stage == "Packets without recognized frames", "TCP without candidate frames");
pipeline.StartSession();
pipeline.Ingest(1, datagram); // No explicit Initialize needed.
Check(pipeline.Diagnostics.Frames == 2 && pipeline.Diagnostics.Decoded == 0 &&
    pipeline.Diagnostics.Stage == "Frames without decoded events", "Stub cannot generate live data");
pipeline.RecordCaptureError();
Check(pipeline.Diagnostics.Errors == 1, "Callback errors counted");
pipeline.StartSession();
Check(pipeline.Diagnostics.Packets == 0 && pipeline.Diagnostics.Frames == 0 && pipeline.Diagnostics.Errors == 0,
    "Capture counters reset with streams");
var ingested = new List<CombatEvent>();
var unknownPipeline = new NetworkPipeline(new FixtureDecoder(false), ingested.Add);
unknownPipeline.Ingest(1, datagram);
Check(ingested.Count == 2 && unknownPipeline.Diagnostics.UnknownActors == 2 &&
    unknownPipeline.Diagnostics.Stage == "Events with unknown player", "Unknown identity never silently assigned to self");
var knownPipeline = new NetworkPipeline(new FixtureDecoder(true), ingested.Add);
knownPipeline.Ingest(1, datagram);
Check(knownPipeline.Diagnostics.SelfKnown && knownPipeline.Diagnostics.UnknownActors == 0,
    "Verified fixture identity reaches ingestion");
var throwingPipeline = new NetworkPipeline(new ThrowingDecoder(), ingested.Add);
throwingPipeline.Ingest(1, datagram);
Check(throwingPipeline.Diagnostics.Errors == 2 && throwingPipeline.Frames == 2,
    "Decoder exception counted without losing next frame");

var nestedWire = Frame(0x04, 0x38, 1, 2);
var packed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(nestedWire.Length)];
int packedSize = K4os.Compression.LZ4.LZ4Codec.Encode(nestedWire, 0, nestedWire.Length, packed, 0, packed.Length);
var bundle = new byte[6 + packedSize];
bundle[0] = bundle[1] = 255;
BinaryPrimitives.WriteInt32LittleEndian(bundle.AsSpan(2), nestedWire.Length);
packed.AsSpan(0, packedSize).CopyTo(bundle.AsSpan(6));
var bundleReader = new AionFrameReader();
var bundleFrames = new List<AionFrame>();
bundleReader.FrameReady += bundleFrames.Add;
bundleReader.Feed(Frame(bundle));
Check(bundleFrames is [{ WasCompressed: true, Opcode: 0x0438 }], "Synthetic LZ4 bundle round-trip");
BinaryPrimitives.WriteInt32LittleEndian(bundle.AsSpan(2), int.MaxValue);
bundleReader.Feed(Frame(bundle));
Check(bundleReader.MalformedBundles == 1 && bundleFrames.Count == 1, "Oversize decompression rejected");
var overflowReader = new AionFrameReader();
overflowReader.Feed([0xff, 0xff, 0xff, 0xff, 0x07]);
Check(overflowReader.SkippedBytes > 0, "Length addition cannot wrap into negative frame size");

LiveIssueChecks.Run(Check);
AutomaticScoreChecks.Run(Check);
PipelineContinuityChecks.Run(Check);
PlayerRatingChecks.Run(Check);
RatingReviewChecks.Run(Check);
GlobalDecoderChecks.Run(Check);
EnergyForecastChecks.Run(Check);
CompactSettingsChecks.Run(Check);
StrictCaptureChecks.Run(Check);
TeleportIdentityChecks.Run(Check);
DungeonContinuityChecks.Run(Check);
WorldTimerChecks.Run(Check);
PartyHealingChecks.Run(Check);
PerformanceChecks.Run(Check);
UserDataChecks.Run(Check);
EncounterReviewChecks.Run(Check);
HistoryPersistenceChecks.Run(Check);
CaptureHealthChecks.Run(Check);
PetChecks.Run(Check);
AppearanceChecks.Run(Check);
LootCounterChecks.Run(Check);

LinuxCaptureChecks.Run(Check);
Console.WriteLine($"\nAll {total} checks passed. Protocol tests use synthetic bytes, not real Global captures.");

sealed class FixtureDecoder(bool known) : ICombatPacketDecoder
{
    public string Name => "TEST ONLY";
    public bool TryDecode(AionFrame frame, DateTimeOffset receivedAt, out CombatEvent? combatEvent)
    {
        combatEvent = new(receivedAt, 17, known ? "FixtureHero" : "#17", 900, "FixtureTarget", 42, 12,
            CombatKind.Damage, IsSelf: known);
        return true;
    }
}
sealed class ThrowingDecoder : ICombatPacketDecoder
{
    public string Name => "TEST ONLY throwing decoder";
    public bool TryDecode(AionFrame frame, DateTimeOffset receivedAt, out CombatEvent? combatEvent) =>
        throw new FormatException("Synthetic malformed payload");
}
