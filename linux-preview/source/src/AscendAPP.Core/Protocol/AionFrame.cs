namespace AscendAPP.Core.Protocol;

/// <summary>Raw, potentially undecoded game message (opcode stored as raw big-endian wire pair).</summary>
public sealed record AionFrame(ushort Opcode, byte[] Payload, bool WasCompressed);
