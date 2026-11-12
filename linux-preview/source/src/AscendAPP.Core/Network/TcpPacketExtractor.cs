using System.Buffers.Binary;
using System.Net;

namespace AscendAPP.Core.Network;

/// <summary>
/// Bounds-checked Ethernet/VLAN, IPv4, IPv6, and Windows Npcap loopback TCP extractor.
/// No IP fragmentation reassembly: fragmented packets are ignored rather than misread.
/// </summary>
public static class TcpPacketExtractor
{
    public static bool TryExtract(int linkType, ReadOnlySpan<byte> data, out TcpSegment? segment)
    {
        segment = null;
        int ipOffset;
        ushort etherType = 0;
        switch (linkType)
        {
            case 1: // DLT_EN10MB
                if (data.Length < 14) return false;
                etherType = BinaryPrimitives.ReadUInt16BigEndian(data[12..]);
                ipOffset = 14;
                for (int i = 0; i < 2 && etherType is 0x8100 or 0x88A8; i++)
                {
                    if (data.Length < ipOffset + 4) return false;
                    etherType = BinaryPrimitives.ReadUInt16BigEndian(data[(ipOffset + 2)..]);
                    ipOffset += 4;
                }
                if (etherType is not (0x0800 or 0x86DD)) return false;
                break;
            case 113: // DLT_LINUX_SLL (dumpcap on Linux's "any" device)
                if (data.Length < 16) return false;
                etherType = BinaryPrimitives.ReadUInt16BigEndian(data[14..]);
                ipOffset = 16;
                break;
            case 276: // DLT_LINUX_SLL2
                if (data.Length < 20) return false;
                etherType = BinaryPrimitives.ReadUInt16BigEndian(data);
                ipOffset = 20;
                break;
            case 0: // DLT_NULL / Npcap loopback: little-endian AF_INET=2, AF_INET6=23
                if (data.Length < 4) return false;
                int family = BinaryPrimitives.ReadInt32LittleEndian(data);
                if (family != 2 && family != 23 && family != 24 && family != 28 && family != 30) return false;
                etherType = family == 2 ? (ushort)0x0800 : (ushort)0x86DD;
                ipOffset = 4;
                break;
            case 101: // DLT_RAW
                ipOffset = 0;
                if (data.Length == 0) return false;
                etherType = (data[0] >> 4) == 4 ? (ushort)0x0800 : (ushort)0x86DD;
                break;
            default: return false;
        }

        ReadOnlySpan<byte> src, dst;
        int tcpOffset, ipEnd;
        if (etherType == 0x0800)
        {
            if (data.Length < ipOffset + 20 || (data[ipOffset] >> 4) != 4) return false;
            var ihl = (data[ipOffset] & 15) * 4;
            var ipLength = BinaryPrimitives.ReadUInt16BigEndian(data[(ipOffset + 2)..]);
            if (ihl < 20 || ipLength < ihl || ipOffset + ipLength > data.Length) return false;
            if (data[ipOffset + 9] != 6) return false;
            var fragments = BinaryPrimitives.ReadUInt16BigEndian(data[(ipOffset + 6)..]);
            if ((fragments & 0x3FFF) != 0) return false;
            src = data.Slice(ipOffset + 12, 4);
            dst = data.Slice(ipOffset + 16, 4);
            tcpOffset = ipOffset + ihl;
            ipEnd = ipOffset + ipLength;
        }
        else if (etherType == 0x86DD)
        {
            if (data.Length < ipOffset + 40 || (data[ipOffset] >> 4) != 6) return false;
            // Extension headers intentionally not interpreted in the initial implementation.
            if (data[ipOffset + 6] != 6) return false;
            var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(data[(ipOffset + 4)..]);
            if (ipOffset + 40 + payloadLength > data.Length) return false;
            src = data.Slice(ipOffset + 8, 16);
            dst = data.Slice(ipOffset + 24, 16);
            tcpOffset = ipOffset + 40;
            ipEnd = ipOffset + 40 + payloadLength;
        }
        else return false;

        if (ipEnd < tcpOffset + 20) return false;
        var tcpHeader = ((data[tcpOffset + 12] >> 4) & 15) * 4;
        if (tcpHeader < 20 || tcpOffset + tcpHeader > ipEnd) return false;
        var payloadStart = tcpOffset + tcpHeader;
        byte flags = data[tcpOffset + 13];
        if (payloadStart == ipEnd && (flags & 7) == 0) return false;
        segment = new TcpSegment(new IPAddress(src).ToString(),
            BinaryPrimitives.ReadUInt16BigEndian(data[tcpOffset..]),
            new IPAddress(dst).ToString(),
            BinaryPrimitives.ReadUInt16BigEndian(data[(tcpOffset + 2)..]),
            BinaryPrimitives.ReadUInt32BigEndian(data[(tcpOffset + 4)..]),
            data[payloadStart..ipEnd].ToArray(), flags);
        return true;
    }
}
