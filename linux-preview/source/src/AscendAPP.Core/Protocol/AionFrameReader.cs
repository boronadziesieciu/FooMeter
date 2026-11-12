// Synchronization/TLS handling adapted with reference to SkeeveAN/Aion-DPS-Meter,
// revision 53033afadf3395dd47708abb308346734d8b1a8a. Copyright (c) 2026 SkeeveTV.
// MIT; see THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt.
using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace AscendAPP.Core.Protocol;

/// <summary>
/// Aion 2 candidate frame reader: unsigned VarInt length, with the game-specific
/// four-byte length offset. Handles incomplete TCP chunks, resynchronization and
/// FF FF / little-endian length / LZ4 bundles. This is *not* a damage decoder.
/// </summary>
public sealed class AionFrameReader(IReadOnlySet<ushort>? syncOpcodes = null)
{
    private const int MaxFrame = 1 * 1024 * 1024;
    private const int MaxPlain = 4 * 1024 * 1024;
    private readonly List<byte> _buffer = new();
    private bool _synced = syncOpcodes is null;
    public long TlsRecords { get; private set; }
    public event Action<AionFrame>? FrameReady;
    public long SkippedBytes { get; private set; }
    public long MalformedBundles { get; private set; }

    public void Reset() { _buffer.Clear(); _synced = syncOpcodes is null; }

    public void Feed(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;
        if (_buffer.Count + data.Length > MaxPlain)
        {
            SkippedBytes += _buffer.Count;
            _buffer.Clear();
            _synced = syncOpcodes is null;
        }
        _buffer.AddRange(data.ToArray());
        int used = 0;
        while (used < _buffer.Count)
        {
            if (_buffer[used] == 0) { used++; continue; }
            if (!_synced)
            {
                int start = FindSync(used);
                if (start < 0)
                {
                    if (_buffer.Count - used > MaxFrame) { SkippedBytes += _buffer.Count - used - 4; used = _buffer.Count - 4; }
                    break;
                }
                SkippedBytes += start - used;
                used = start; _synced = true;
            }
            // TLS records share some game connections; ciphertext is never a game frame.
            if (_buffer.Count - used >= 5 && _buffer[used] is >= 20 and <= 23 &&
                _buffer[used + 1] == 3 && _buffer[used + 2] is >= 1 and <= 4)
            {
                int tls = (_buffer[used + 3] << 8 | _buffer[used + 4]) + 5;
                if (tls > 5 && tls <= 16645)
                {
                    if (_buffer.Count - used < tls) break;
                    used += tls; TlsRecords++; continue;
                }
            }
            var status = Header(CollectionsMarshalSpan(_buffer, used), out int header, out int total);
            if (status == 0) break; // need more bytes
            if (status < 0) { used++; SkippedBytes++; _synced = syncOpcodes is null; continue; }
            var body = _buffer.GetRange(used + header, total - header).ToArray();
            Dispatch(body, false, 0);
            used += total;
        }
        if (used > 0) _buffer.RemoveRange(0, used);
    }

    private int FindSync(int used)
    {
        for (int start = used; start < _buffer.Count; start++)
        {
            int pos = start;
            for (int n = 0; n < 3; n++)
            {
                var b = CollectionsMarshalSpan(_buffer, pos);
                if (Header(b, out int head, out int total) != 1 || syncOpcodes?.Contains(BinaryPrimitives.ReadUInt16BigEndian(b[head..])) != true) break;
                pos += total;
                if (pos == _buffer.Count || n == 2) return start;
                while (pos < _buffer.Count && _buffer[pos] == 0) pos++;
                if (_buffer.Count - pos >= 5 && _buffer[pos] is >= 20 and <= 23 &&
                    _buffer[pos + 1] == 3 && _buffer[pos + 2] is >= 1 and <= 4)
                {
                    int tls = (_buffer[pos + 3] << 8 | _buffer[pos + 4]) + 5;
                    if (tls is > 5 and <= 16645 && pos + tls <= _buffer.Count) pos += tls;
                }
            }
        }
        return -1;
    }

    private static ReadOnlySpan<byte> CollectionsMarshalSpan(List<byte> list, int index) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list)[index..];

    // 1 complete, 0 incomplete, -1 invalid.
    private static int Header(ReadOnlySpan<byte> input, out int prefixBytes, out int totalBytes)
    {
        prefixBytes = totalBytes = 0;
        int value = 0;
        int shift = 0;
        for (int i = 0; i < Math.Min(input.Length, 5); i++)
        {
            var b = input[i];
            if (i == 4 && (b & 0xF0) != 0) return -1;
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                prefixBytes = i + 1;
                if (value < 6 || value > MaxFrame + 4) return -1;
                // v includes a virtual four-byte field, which is absent on wire.
                totalBytes = value + prefixBytes - 4;
                if (totalBytes > MaxFrame || totalBytes < prefixBytes + 2) return -1;
                return input.Length >= totalBytes ? 1 : 0;
            }
            shift += 7;
        }
        return input.Length >= 5 ? -1 : 0;
    }

    private void Dispatch(byte[] body, bool compressed, int depth)
    {
        if (body.Length < 2 || depth > 3) { MalformedBundles++; return; }
        ReadOnlySpan<byte> compressedBody = body;
        if (body.Length >= 6 && body[0] is >= 0xF0 and < 0xFF && body[1] == 0xFF && body[2] == 0xFF)
            compressedBody = body.AsSpan(1);
        if (compressedBody.Length >= 6 && compressedBody[0] == 0xFF && compressedBody[1] == 0xFF)
        {
            int original = BinaryPrimitives.ReadInt32LittleEndian(compressedBody[2..]);
            if (original <= 0 || original > MaxPlain || original > (compressedBody.Length - 6) * 256L)
            { MalformedBundles++; return; }
            var plain = new byte[original];
            int count;
            try
            {
                count = LZ4Codec.Decode(compressedBody[6..].ToArray(), 0, compressedBody.Length - 6,
                                        plain, 0, original);
            }
            catch { MalformedBundles++; return; }
            if (count != original) { MalformedBundles++; return; }
            var nested = plain.AsSpan();
            var children = new List<byte[]>();
            while (!nested.IsEmpty)
            {
                if (nested[0] == 0) { nested = nested[1..]; continue; }
                if (Header(nested, out int head, out int size) != 1) { MalformedBundles++; return; }
                children.Add(nested.Slice(head, size - head).ToArray());
                nested = nested[size..];
            }
            foreach (var child in children) Dispatch(child, true, depth + 1);
            return;
        }
        FrameReady?.Invoke(new AionFrame(BinaryPrimitives.ReadUInt16BigEndian(body),
                                          body.AsSpan(2).ToArray(), compressed));
    }
}
