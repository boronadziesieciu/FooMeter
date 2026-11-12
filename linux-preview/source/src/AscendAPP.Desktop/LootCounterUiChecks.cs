using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AscendAPP.Desktop.Services;
using AscendAPP.Core.Pets;
namespace AscendAPP.Desktop;
internal static class LootCounterUiChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var c=new MeterController(preview:true);var meter=c.CreateOverlay();var pets=c.CreatePetOverlay();var counters=new LootCounterWindow();
        try{
            c.Pets.Observe(new PetCollectionObservation("Fixture",1304,[new(1016,1,14)],DateTimeOffset.UtcNow));c.Preferences.ShowPetOverlay=true;
            c.UsePetCountersOnly();
            check(!c.Preferences.ShowPetOverlay&&c.Preferences.ShowPetPickups&&c.Preferences.PetCounterStyle=="Beside loot rows"&&c.Pets.Trusted,"Counter-only mode hides the collection window while preserving capture and progress");
            var frame=new LootReadFrame(new(0,0,1700,910),new(654,250,500,420),[new(1016,0,50,14,"14 / 25",true,"Fixture"),new(1016,0,90,14,"15 / 25",true,"Fixture")],44);
            counters.Apply(frame,renderOnly:true);var canvas=(Canvas)counters.FindName("Labels");
            check(canvas.Children.OfType<TextBlock>().Count(t=>t.Visibility==Visibility.Visible)==2&&canvas.Children.OfType<TextBlock>().First().Text=="14 / 25","The loot overlay renders only plain row counters with no duplicate name, icon or window chrome");
            check(counters.Width==130&&counters.Background==Brushes.Transparent&&counters.IsHitTestVisible==false,"The loot overlay is a narrow transparent click-through strip");
            var first=canvas.Children.OfType<TextBlock>().First();double y=Canvas.GetTop(first);counters.Apply(frame with{Rows=[new(1016,0,20,14,"14 / 25",true,"Fixture")]},renderOnly:true);
            check(Canvas.GetTop(first)<y&&canvas.Children.OfType<TextBlock>().Count(t=>t.Visibility==Visibility.Visible)==1,"Counters move with observed row positions and disappear with removed rows");
            var crop=GameLootReader.Crop(new(100,200,1700,910),new(true,"AION2",.385,.28,.34,.48,44));
            check(crop.Left>=100&&crop.Top>=200&&crop.Right<=1800&&crop.Bottom<=1110,"Capture reads only the selected game-client region");
        }finally{counters.CloseForExit();pets.CloseForExit();meter.Close();}
    }
}
