// Selected ticket readers adapted from SkeeveAN/Aion-DPS-Meter,
// Aion2Tickets.cs @ 53033afadf3395dd47708abb308346734d8b1a8a.
// Copyright (c) 2026 SkeeveTV. MIT; see THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt.
using System.Buffers.Binary;
namespace AscendAPP.Core.Protocol;

public sealed record OdyleEnergy(long Regular, long Bonus, DateTimeOffset ObservedAt)
{
    public long Available => Regular + Bonus;
}
public sealed record TicketBalance(uint Id,long Regular,long Bonus,DateTimeOffset ObservedAt) { public long Available=>Regular+Bonus; }
public static class OdyleEnergyReader
{
    public const uint TicketId = 60000001;
    public static bool TryReadList(ReadOnlySpan<byte> b,DateTimeOffset at,out OdyleEnergy? energy)
    {
        bool ok=TryReadTickets(b,at,out var tickets);energy=ok&&tickets.FirstOrDefault(t=>t.Id==TicketId) is {} t?new(t.Regular,t.Bonus,at):null;return ok;
    }
    public static bool TryReadUpdate(ReadOnlySpan<byte> b,DateTimeOffset at,out OdyleEnergy? energy)
    {
        bool ok=TryReadTicketUpdate(b,at,out var ticket);energy=ok&&ticket is {Id:TicketId} t?new(t.Regular,t.Bonus,at):null;return ok;
    }
    public static bool TryReadTickets(ReadOnlySpan<byte> b,DateTimeOffset at,out IReadOnlyList<TicketBalance> tickets)
    {
        tickets=[];if(b.IsEmpty)return false;int p=1;var result=new Dictionary<uint,TicketBalance>();
        for(int i=0;i<b[0];i++)
        {
            if(!Entry(b,ref p,out uint id,out uint count,out uint bonus)||result.ContainsKey(id))return false;
            result[id]=new(id,count,bonus,at);
        }
        if(p!=b.Length)return false;tickets=result.Values.ToArray();return true;
    }
    public static bool TryReadTicketUpdate(ReadOnlySpan<byte> b,DateTimeOffset at,out TicketBalance? ticket)
    {
        ticket=null;if(b.IsEmpty||b[0]>1)return false;int p=1;
        if(!Entry(b,ref p,out uint id,out uint count,out uint bonus)||p>=b.Length)return false;
        byte reason=b[p++];if(reason is <1 or >3)return false;if(b[0]==1)p+=4;
        if(p!=b.Length)return false;ticket=new(id,count,bonus,at);return true;
    }
    private static bool Entry(ReadOnlySpan<byte> b, ref int p, out uint id, out uint count, out uint bonus)
    {
        id = count = bonus = 0;
        if (p + 5 > b.Length) return false;
        byte mask = b[p++];
        if ((mask & ~15) != 0) return false;
        id = BinaryPrimitives.ReadUInt32LittleEndian(b[p..]); p += 4;
        if ((mask & 1) != 0) p += 8;
        if ((mask & 2) != 0) p += 8; // documented optional second time field
        if (p > b.Length) return false;
        if ((mask & 4) != 0 && !Var(b, ref p, out count)) return false;
        if ((mask & 8) != 0 && !Var(b, ref p, out bonus)) return false;
        return count <= 10000000 && bonus <= 10000000;
    }
    private static bool Var(ReadOnlySpan<byte> b, ref int p, out uint value)
    {
        value = 0;
        for (int i = 0; i < 5 && p < b.Length; i++)
        {
            byte x = b[p++];
            if (i == 4 && (x & 240) != 0) return false;
            value |= (uint)(x & 127) << (7 * i);
            if ((x & 128) == 0) return true;
        }
        return false;
    }
}
