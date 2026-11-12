// Selected wire layout adapted from SkeeveAN/Aion-DPS-Meter @ 53033afadf3395dd47708abb308346734d8b1a8a.
// Copyright (c) 2026 SkeeveTV. MIT; see THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt.
using System.Buffers.Binary;
namespace AscendAPP.Core.Game;
public sealed record FieldBossSlot(int Number,bool Alive,long TimeMs);
public sealed record FieldBossList(int MapId,IReadOnlyList<FieldBossSlot> Slots,DateTimeOffset ObservedAt,int? ServerId=null);
public static class FieldBossPacketReader
{
    public static bool TryRead(ReadOnlySpan<byte> b,DateTimeOffset at,out FieldBossList? result)
    {
        result=null;
        // Payload excludes opcode: zero u16, map u32, count u8, entries, up to eight zero padding bytes.
        if(b.Length is <7 or >4096||b[0]!=0||b[1]!=0)return false;
        int map=BinaryPrimitives.ReadInt32LittleEndian(b[2..]),count=b[6],offset=7;
        if(map is <1000 or >99999||count is <1 or >64)return false;
        var slots=new List<FieldBossSlot>();var seen=new HashSet<int>();
        for(int i=0;i<count;i++)
        {
            if(!Header(b,offset,map,out bool alive,out int number,out int next)||!seen.Add(number))return false;
            if(alive)
            {
                if(next+12>b.Length)return false;
                for(int p=next;p<next+12;p+=4){float value=BinaryPrimitives.ReadSingleLittleEndian(b[p..]);if(!float.IsFinite(value)||Math.Abs(value)>10_000_000)return false;}
                next+=12;
            }
            bool found=false;
            for(int extra=0;extra<=1;extra++)
            {
                int t=next+extra;if(t+8>b.Length)continue;
                long time=BinaryPrimitives.ReadInt64LittleEndian(b[t..]);
                if(time!=0&&(time<1_600_000_000_000||time>2_600_000_000_000))continue;
                int end=t+8;
                bool fits=i==count-1?b.Length-end<=8&&b[end..].IndexOfAnyExcept((byte)0)<0:Header(b,end,map,out _,out _,out _);
                if(!fits)continue;
                slots.Add(new(number,alive,time));offset=end;found=true;break;
            }
            if(!found)return false;
        }
        result=new(map,slots,at);return true;
    }
    private static bool Header(ReadOnlySpan<byte> b,int offset,int map,out bool alive,out int number,out int next)
    {
        alive=false;number=0;next=offset;
        if(offset>=b.Length||b[offset]>1)return false;
        alive=b[offset++]==1;uint place=0;
        for(int i=0;i<5&&offset<b.Length;i++)
        {
            byte x=b[offset++];if(i==4&&(x&0xf0)!=0)return false;
            place|=(uint)(x&127)<<(7*i);
            if((x&128)!=0)continue;
            long slot=(long)place-map*100L;
            if(slot is <1 or >99)return false;
            number=(int)slot;next=offset;return true;
        }
        return false;
    }
}
