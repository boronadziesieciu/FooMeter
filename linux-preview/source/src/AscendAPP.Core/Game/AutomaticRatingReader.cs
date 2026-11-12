using System.Buffers.Binary;
using AscendAPP.Core.Protocol;

namespace AscendAPP.Core.Game;

/// <summary>Independent implementation of documented Aion 2 wire fields. See docs/AUTOMATIC_SCORES_0.10.16.md.
/// Only called inside the corresponding framed, identity-validated record. No scanning of raw traffic.</summary>
public static class AutomaticRatingReader
{
    public static PlayerRating? Own(ReadOnlySpan<byte> payload,int afterName,PlayerIdentity own,DateTimeOffset at,out string detail)
    {
        detail="Own record has no supported score fields";
        if(!own.IsSelf||!own.PlayerVerified||own.ServerId is null||afterName<0||afterName+11>payload.Length)return null;
        var fields=payload[afterName..];
        if(BinaryPrimitives.ReadUInt16LittleEndian(fields)!=own.ServerId||GlobalProtocolFacts.CharacterClass(BinaryPrimitives.ReadInt32LittleEndian(fields[2..]))!=own.ClassName||fields[6] is not (1 or 2))return null;
        int level=BinaryPrimitives.ReadInt32LittleEndian(fields[7..]);if(level is <1 or >200)return null;
        int? gs=null,cp=null;
        if(fields.Length>=19)
        {
            uint first=BinaryPrimitives.ReadUInt32LittleEndian(fields[11..]);
            uint repeated=BinaryPrimitives.ReadUInt32LittleEndian(fields[15..]);
            if(first==repeated&&first is >0 and <100000)gs=(int)first;
        }
        // Two u64 values precede a five-entry keyed u16 table. Accept a single structurally
        // valid location, not the first plausible integer in variable-length equipment data.
        int locations=0;
        for(int table=35;table+16<=fields.Length;table++)
        {
            if(fields[table]!=5)continue;
            bool keyed=true;for(int entry=0;entry<5;entry++)if(fields[table+1+entry*3]!=entry+1){keyed=false;break;}
            if(!keyed)continue;
            ulong value=BinaryPrimitives.ReadUInt64LittleEndian(fields[(table-16)..]);
            ulong secondary=BinaryPrimitives.ReadUInt64LittleEndian(fields[(table-8)..]);
            if(value is 0 or >=100000000||secondary<value||secondary>=100000000)continue;
            locations++;cp=(int)value;
        }
        if(locations!=1)cp=null;
        detail=$"Own record: GS {(gs.HasValue?"observed":"missing or inconsistent")}; CP {(cp.HasValue?"observed":locations>1?"ambiguous":"supported tail not found")}";
        return gs.HasValue||cp.HasValue?new(own.Id,own.Name,own.ServerId,gs,cp,at,"Automatic own-character record (0x3336)"):null;
    }

    public static PlayerRating? Party(ReadOnlySpan<byte> payload,int nameOffset,int afterName,string name,int homeServer,string? className,DateTimeOffset at)
    {
        // The account ID before the name is not a world actor ID. Its high u16 is the
        // home server. Require the occupied-slot prefix before accepting score fields.
        if(nameOffset<10||afterName+12>payload.Length||payload[nameOffset-10]==0||payload[nameOffset-9] is <1 or >24)return null;
        ulong account=BinaryPrimitives.ReadUInt64LittleEndian(payload[(nameOffset-8)..]);
        if((account&0x0000ffffffffffffUL)==0||(account>>48)!=(ulong)homeServer||homeServer is <1000 or >2999)return null;
        if(GlobalProtocolFacts.CharacterClass(BinaryPrimitives.ReadInt32LittleEndian(payload[afterName..]))!=className)return null;
        int level=BinaryPrimitives.ReadInt32LittleEndian(payload[(afterName+4)..]);if(level is <1 or >200)return null;
        int score=BinaryPrimitives.ReadInt32LittleEndian(payload[(afterName+8)..]);int? gs=score is >0 and <100000?score:null;
        int? cp=null;int matches=0;
        // Roster state can insert bytes here. A repeated world pair + a small tag and
        // a bounded u64 disambiguate the score from shifted bytes or another member.
        for(int start=afterName+12;start<afterName+22&&start+13<=payload.Length;start++)
        {
            int world=BinaryPrimitives.ReadUInt16LittleEndian(payload[start..]);
            int second=BinaryPrimitives.ReadUInt16LittleEndian(payload[(start+2)..]);
            if(world is <1000 or >2999||world%100 is <1 or >30||!(second==world||second==homeServer||(second>>8)==0x10)||payload[start+4] is <1 or >4)continue;
            ulong value=BinaryPrimitives.ReadUInt64LittleEndian(payload[(start+5)..]);
            if(value is 0 or >=100000000)continue;
            matches++;cp=(int)value;
        }
        if(matches!=1)cp=null;
        return gs.HasValue||cp.HasValue?new(0,name,homeServer,gs,cp,at,"Automatic party roster (0x0297)"):null;
    }
}
