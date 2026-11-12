// Wire facts adapted from elkaida/AION2-pet-tracker, MIT.
// See THIRD_PARTY_NOTICES/PetTracker-MIT.txt and docs/PET_TRACKER_0.10.27.md.
using System.Buffers.Binary;
namespace AscendAPP.Core.Pets;
public sealed record PetCount(uint Id,int Level,int Points);
public sealed record PetCollectionObservation(string Character,int ServerId,IReadOnlyList<PetCount> Counts,DateTimeOffset At);
public sealed record PetGainObservation(string Character,int ServerId,uint PetId,int Amount,DateTimeOffset At);
public static class PetProtocol
{
    public const ushort StateOpcode=0x0090,GainOpcode=0x0d90;
    public const int MaxPets=1024;
    public static int Required(int level)=>level switch{0=>5,1=>25,2=>75,_=>0};
    public static bool Valid(PetCount p)=>p is not null&&p.Id is >0 and <=1000000&&p.Level is >=0 and <=3&&p.Points>=0&&(p.Level==3?p.Points==0:p.Points<Required(p.Level));
    public static bool TryState(ReadOnlySpan<byte> b,out IReadOnlyList<PetCount> counts)=>TryState(b,out counts,out _);
    public static bool TryState(ReadOnlySpan<byte> b,out IReadOnlyList<PetCount> counts,out string reason)
    {
        reason="Unrecognized collection prefix";
        counts=[];if(b.Length<6||b.Length>131072){reason="Collection body size outside known bounds: "+b.Length;return false;}
        int p=4;var levels=new Dictionary<uint,int>();var points=new Dictionary<uint,int>();
        if(!Count(b,ref p,out int n)||b.Length-p<n*12){reason="Missing or truncated level table";return false;}
        for(int i=0;i<n;i++){uint id=U32(b,ref p),again=U32(b,ref p),level=U32(b,ref p);if(id!=again||id is 0 or >1000000||level>3||!levels.TryAdd(id,(int)level)){reason=$"Invalid level record {i}: pet {id}, repeated {again}, rank {level}";return false;}}
        if(!Count(b,ref p,out n)||b.Length-p<n*8){reason="Missing or truncated soul table";return false;}
        for(int i=0;i<n;i++){uint id=U32(b,ref p),value=U32(b,ref p);if(id is 0 or >1000000||value>1000000||!points.TryAdd(id,(int)value))return false;}
        // The documented state has an extension tail; only the bounded, known prefix is interpreted.
        var all=levels.Keys.Union(points.Keys).ToArray();if(all.Length>MaxPets)return false;
        var result=all.Select(id=>new PetCount(id,levels.GetValueOrDefault(id),points.GetValueOrDefault(id))).ToArray();
        if(result.FirstOrDefault(x=>!Valid(x)) is {} bad){reason=$"Inconsistent progress: pet {bad.Id}, rank {bad.Level}, souls {bad.Points}";return false;}counts=result;reason="Supported collection prefix";return true;
    }
    public static bool TryGain(ReadOnlySpan<byte> b,out uint id,out int amount)
    {
        id=0;amount=0;if(b.Length!=9||b[0]!=1)return false;
        id=BinaryPrimitives.ReadUInt32LittleEndian(b[1..]);uint n=BinaryPrimitives.ReadUInt32LittleEndian(b[5..]);
        if(id is 0 or >1000000||n is 0 or >1000000)return false;amount=(int)n;return true;
    }
    private static uint U32(ReadOnlySpan<byte> b,ref int p){uint n=BinaryPrimitives.ReadUInt32LittleEndian(b[p..]);p+=4;return n;}
    private static bool Count(ReadOnlySpan<byte> b,ref int p,out int n)
    {
        n=0;for(int i=0;i<2;i++){if(p>=b.Length)return false;byte v=b[p++];n|=(v&127)<<(7*i);if((v&128)==0)return n<=MaxPets;}return false;
    }
}
