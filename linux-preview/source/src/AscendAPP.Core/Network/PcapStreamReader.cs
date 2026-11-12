using System.Buffers.Binary;

namespace AscendAPP.Core.Network;

/// <summary>Bounded classic pcap input, including pipes with short reads. Never guesses pcapng framing.</summary>
public static class PcapStreamReader
{
    public static async Task ReadAsync(Stream stream, Action<int, byte[], DateTimeOffset> onPacket,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[24];
        await stream.ReadExactlyAsync(header, cancellationToken);
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        bool little = magic is 0xa1b2c3d4 or 0xa1b23c4d;
        bool nano = magic is 0xa1b23c4d or 0x4d3cb2a1;
        if (magic is not (0xa1b2c3d4 or 0xd4c3b2a1 or 0xa1b23c4d or 0x4d3cb2a1))
            throw new InvalidDataException("Expected classic pcap. Use dumpcap -P or convert pcapng with editcap -F pcap.");
        uint U32(ReadOnlySpan<byte> b) => little ? BinaryPrimitives.ReadUInt32LittleEndian(b) : BinaryPrimitives.ReadUInt32BigEndian(b);
        ushort U16(ReadOnlySpan<byte> b) => little ? BinaryPrimitives.ReadUInt16LittleEndian(b) : BinaryPrimitives.ReadUInt16BigEndian(b);
        if (U16(header.AsSpan(4)) != 2 || U16(header.AsSpan(6)) != 4)
            throw new InvalidDataException("Unsupported pcap version.");
        uint snapLength = U32(header.AsSpan(16));
        int linkType = (int)(U32(header.AsSpan(20)) & 0xffff);
        if (snapLength is 0 or > 4 * 1024 * 1024)
            throw new InvalidDataException("Invalid pcap snapshot length.");
        if (linkType is not (0 or 1 or 101 or 113 or 276))
            throw new InvalidDataException($"Unsupported pcap link type {linkType}.");
        var record = new byte[16];
        while (true)
        {
            int first = await stream.ReadAsync(record.AsMemory(0, 1), cancellationToken);
            if (first == 0) return;
            await stream.ReadExactlyAsync(record.AsMemory(1), cancellationToken);
            uint seconds = U32(record), fraction = U32(record.AsSpan(4));
            uint captured = U32(record.AsSpan(8)), original = U32(record.AsSpan(12));
            if (captured > snapLength || captured > original || fraction >= (nano ? 1_000_000_000u : 1_000_000u))
                throw new InvalidDataException("Invalid pcap packet header.");
            var packet = new byte[(int)captured];
            await stream.ReadExactlyAsync(packet, cancellationToken);
            var at = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nano ? fraction / 100 : fraction * 10L);
            onPacket(linkType, packet, at);
        }
    }
}
