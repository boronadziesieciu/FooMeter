namespace AscendAPP.Core.Protocol;
public static class PlayerServerFacts
{
    public static int? Faction(int? server){if(server is not {} s)return null;int faction=s/1000,block=s/100%10,slot=s%100;return faction is 1 or 2&&block<=5&&slot>=1&&slot<=(block==0?58:60)?faction:null;}
}
