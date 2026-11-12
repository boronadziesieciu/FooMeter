namespace AscendAPP.Core.Game;
// Explicit local values supplied by the user, never a packet observation.
public sealed record ManualPlayerRating(string Name,int ServerId,int? GearScore,int? CombatPower,DateTimeOffset EnteredAt)
{
    public bool Valid=>!string.IsNullOrWhiteSpace(Name)&&Name.Length<=48&&!Name.Any(char.IsControl)&&ServerId is >=1000 and <=9999
        &&(GearScore is null or >0 and <100000)&&(CombatPower is null or >0 and <=100000000)&&(GearScore.HasValue||CombatPower.HasValue);
    public PlayerRating? Fill(PlayerRating? observed,uint actor,string name,int? server)
    {
        if(!Valid||name!=Name||server!=ServerId)return observed;
        bool gs=observed?.GearScore is null&&GearScore.HasValue,cp=observed?.CombatPower is null&&CombatPower.HasValue;
        if(!gs&&!cp)return observed;
        var value=observed??new(actor,name,server,null,null,EnteredAt,"");
        return value with{GearScore=gs?GearScore:value.GearScore,CombatPower=cp?CombatPower:value.CombatPower,
            GearScoreAt=gs?EnteredAt:value.GearScoreAt,CombatPowerAt=cp?EnteredAt:value.CombatPowerAt,
            ManualGearScore=gs,ManualCombatPower=cp,Source="* Manual entry: "+(gs&&cp?"GS and CP":gs?"GS":"CP")+
                (observed is null?"":". Other value: "+observed.Source)};
    }
}
