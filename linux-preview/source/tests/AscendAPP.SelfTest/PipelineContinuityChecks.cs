using System.Buffers.Binary;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;

static class PipelineContinuityChecks
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-24T12:00:00Z");

    // Synthetic transport fixtures test sequence handling independently of live opcodes.
    private sealed class RecordingDecoder : ICombatPacketDecoder
    {
        public string Name => "Transport fixture";
        public List<byte> Values { get; } = [];
        public bool TryDecode(AionFrame frame, DateTimeOffset receivedAt, out CombatEvent? combatEvent)
        {
            Values.Add(frame.Payload[0]);
            combatEvent = null;
            return false;
        }
    }

    private static byte[] Frame(byte value) => [7, 0x12, 0x34, value];

    private static byte[] Packet(byte[] payload, uint sequence, byte flags = 0x18, ushort peerPort = 50001)
    {
        var bytes = new byte[54 + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), 0x0800);
        bytes[14] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(16), (ushort)(40 + payload.Length));
        bytes[23] = 6;
        bytes[26] = 10; bytes[29] = 1; bytes[30] = 10; bytes[33] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(34), 13328);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(36), peerPort);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(38), sequence);
        bytes[46] = 0x50; bytes[47] = flags;
        payload.CopyTo(bytes, 54);
        return bytes;
    }

    public static void Run(Action<bool, string> check)
    {
        var decoder = new RecordingDecoder();
        var pipeline = new NetworkPipeline(decoder, _ => { });
        pipeline.Ingest(1, Packet([], 1000, 0x12), "ethernet", At);
        pipeline.Ingest(1, Packet(Frame(2), 1005), "ethernet", At.AddMilliseconds(1));
        check(decoder.Values.Count == 0 && pipeline.Segments == 1 && pipeline.PayloadBytes == 4,
            "Captured SYN holds a later first payload while counting only payload bytes");
        pipeline.Ingest(1, Packet(Frame(1), 1001), "ethernet", At.AddMilliseconds(2));
        check(decoder.Values.SequenceEqual(new byte[] { 1, 2 }) && pipeline.Diagnostics.Errors == 0,
            "First server frames survive out-of-order arrival immediately after SYN");
        pipeline.Ingest(1, Packet(Frame(1), 1001), "ethernet", At.AddMilliseconds(3));
        check(decoder.Values.Count == 2, "Retransmitted first payload is not decoded twice after SYN");

        var synDataDecoder = new RecordingDecoder();
        var synData = new NetworkPipeline(synDataDecoder, _ => { });
        synData.Ingest(1, Packet(Frame(3), 2000, 2), "ethernet", At);
        synData.Ingest(1, Packet(Frame(4), 2005), "ethernet", At.AddMilliseconds(1));
        check(synDataDecoder.Values.SequenceEqual(new byte[] { 3, 4 }),
            "SYN carrying data consumes exactly one TCP sequence number before its payload");

        var wrapDecoder = new RecordingDecoder();
        var wrap = new NetworkPipeline(wrapDecoder, _ => { });
        wrap.Ingest(1, Packet([], uint.MaxValue, 2), "ethernet", At);
        wrap.Ingest(1, Packet(Frame(6), 4), "ethernet", At.AddMilliseconds(1));
        wrap.Ingest(1, Packet(Frame(5), 0), "ethernet", At.AddMilliseconds(2));
        check(wrapDecoder.Values.SequenceEqual(new byte[] { 5, 6 }),
            "SYN sequence wrap preserves reordered initial payloads");

        var reuseDecoder = new RecordingDecoder();
        var reuse = new NetworkPipeline(reuseDecoder, _ => { });
        reuse.Ingest(1, Packet([], 3000, 2), "ethernet", At);
        reuse.Ingest(1, Packet(Frame(8), 3005), "ethernet", At.AddMilliseconds(1));
        reuse.Ingest(1, Packet([], 3001, 4), "ethernet", At.AddMilliseconds(2));
        reuse.Ingest(1, Packet([], 4000, 2), "ethernet", At.AddMilliseconds(3));
        reuse.Ingest(1, Packet(Frame(9), 4001), "ethernet", At.AddMilliseconds(4));
        check(reuseDecoder.Values.SequenceEqual(new byte[] { 9 }),
            "Reset and reused endpoints discard pending bytes from the previous connection");

        var boundedDecoder = new RecordingDecoder();
        var bounded = new NetworkPipeline(boundedDecoder, _ => { });
        bounded.Ingest(1, Packet([], 5000, 2), "ethernet", At);
        for (int i = 1; i < 64; i++)
            bounded.Ingest(1, Packet([], 100, 2, (ushort)(50001 + i)), "ethernet", At.AddMilliseconds(i));
        bounded.Ingest(1, Packet(Frame(11), 5005), "ethernet", At.AddMilliseconds(100));
        bounded.Ingest(1, Packet([], 100, 2, 51000), "ethernet", At.AddMilliseconds(101));
        bounded.Ingest(1, Packet(Frame(10), 5001), "ethernet", At.AddMilliseconds(102));
        check(boundedDecoder.Values.SequenceEqual(new byte[] { 10, 11 }) && bounded.Diagnostics.Errors == 0,
            "SYN-only stream eviction preserves a recently active stream and its queued first frames");

        var surfacesDecoder = new RecordingDecoder();
        var surfaces = new NetworkPipeline(surfacesDecoder, _ => { });
        surfaces.Ingest(1, Packet([], 6000, 2), "ethernet", At);
        surfaces.Ingest(1, Packet(Frame(13), 6005), "ethernet", At.AddMilliseconds(1));
        surfaces.Ingest(1, Packet([], 7000, 2), "loopback", At.AddMilliseconds(2));
        surfaces.Ingest(1, Packet(Frame(14), 7001), "loopback", At.AddMilliseconds(3));
        surfaces.Ingest(1, Packet(Frame(12), 6001), "ethernet", At.AddMilliseconds(4));
        check(surfacesDecoder.Values.SequenceEqual(new byte[] { 14, 12, 13 }),
            "SYN seeding remains isolated between adapters with identical endpoint addresses");
    }
}