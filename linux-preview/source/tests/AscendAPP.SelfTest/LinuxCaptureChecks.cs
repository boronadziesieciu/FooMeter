using System.Buffers.Binary;
using AscendAPP.Core.Network;

internal static class LinuxCaptureChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Independent IPv4/TCP payload wrapped in each Linux cooked-capture header.
        byte[] ip = new byte[43]; ip[0] = 0x45; ip[9] = 6;
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(2), 43);
        ip[12] = 10; ip[15] = 1; ip[16] = 10; ip[19] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(20), 13328);
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(22), 50000);
        ip[32] = 0x50; ip[33] = 0x18; ip[40] = 1; ip[41] = 2; ip[42] = 3;
        foreach (int link in new[] { 113, 276 })
        {
            int offset = link == 113 ? 16 : 20;
            var cooked = new byte[offset + ip.Length];
            BinaryPrimitives.WriteUInt16BigEndian(cooked.AsSpan(link == 113 ? 14 : 0), 0x0800);
            ip.CopyTo(cooked, offset);
            check(TcpPacketExtractor.TryExtract(link, cooked, out var segment) && segment!.SourcePort == 13328 &&
                segment.Payload.SequenceEqual(new byte[] { 1, 2, 3 }), $"Linux cooked capture {link} decodes TCP payload");
            check(!TcpPacketExtractor.TryExtract(link, cooked.AsSpan(0, offset - 1), out _), $"Linux cooked capture {link} rejects truncated header");
        }
        foreach (bool little in new[] { true, false })
        foreach (bool nano in new[] { true, false })
        {
            byte[] pcap = Fixture(little, nano, ip);
            int packets = 0;
            using var stream = new ShortReadStream(pcap);
            PcapStreamReader.ReadAsync(stream, (link, bytes, at) =>
            {
                if (link == 101 && bytes.SequenceEqual(ip) && at == DateTimeOffset.FromUnixTimeSeconds(100).AddMilliseconds(125)) packets++;
            }).GetAwaiter().GetResult();
            check(packets == 1, $"Pcap {(little ? "little" : "big")}-endian {(nano ? "nano" : "micro")}seconds survives short pipe reads");
            bool rejected = false;
            try { PcapStreamReader.ReadAsync(new MemoryStream(pcap[..^1]), (_, _, _) => { }).GetAwaiter().GetResult(); }
            catch (EndOfStreamException) { rejected = true; }
            check(rejected, "Pcap rejects truncated packet rather than reporting successful EOF");
        }
        byte[] oversized = Fixture(true, false, ip);
        BinaryPrimitives.WriteUInt32LittleEndian(oversized.AsSpan(32), uint.MaxValue);
        bool bounded = false;
        try { PcapStreamReader.ReadAsync(new MemoryStream(oversized), (_, _, _) => { }).GetAwaiter().GetResult(); }
        catch (InvalidDataException) { bounded = true; }
        check(bounded, "Pcap rejects oversized allocation request");
        byte[] pcapng = new byte[24]; pcapng[0] = 0x0a; pcapng[1] = 0x0d; pcapng[2] = 0x0d; pcapng[3] = 0x0a;
        bool format = false;
        try { PcapStreamReader.ReadAsync(new MemoryStream(pcapng), (_, _, _) => { }).GetAwaiter().GetResult(); }
        catch (InvalidDataException) { format = true; }
        check(format, "Pcapng fails with an explicit format error");
    }

    private static byte[] Fixture(bool little, bool nano, byte[] packet)
    {
        var data = new byte[40 + packet.Length];
        void U32(int offset, uint value)
        {
            if (little) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), value);
        }
        void U16(int offset, ushort value)
        {
            if (little) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
            else BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), value);
        }
        U32(0, nano ? 0xa1b23c4d : 0xa1b2c3d4); U16(4, 2); U16(6, 4);
        U32(16, 262144); U32(20, 101); U32(24, 100); U32(28, nano ? 125000000u : 125000u);
        U32(32, (uint)packet.Length); U32(36, (uint)packet.Length); packet.CopyTo(data, 40);
        return data;
    }
    private sealed class ShortReadStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(3, buffer.Length)], cancellationToken);
    }
}
