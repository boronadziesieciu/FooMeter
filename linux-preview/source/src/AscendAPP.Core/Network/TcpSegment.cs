namespace AscendAPP.Core.Network;

/// <summary>Captured TCP payload and stream direction. A zero-length payload is skipped.</summary>
public sealed record TcpSegment(
    string SourceAddress, ushort SourcePort, string DestinationAddress, ushort DestinationPort,
    uint SequenceNumber, byte[] Payload, byte Flags = 0, string CaptureSource = "")
{
    public string StreamId => $"{CaptureSource}|{SourceAddress}:{SourcePort}>{DestinationAddress}:{DestinationPort}";
}
