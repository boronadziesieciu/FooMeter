using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using AscendAPP.Core.Settings;

namespace FooMeter.Linux;

public sealed class SettingsWindow : Window
{
    public int EditorCount { get; private set; }
    public SettingsWindow(LinuxMeter meter, Action changed)
    {
        Title = "FooMeter · Settings"; Width = 700; Height = 680;
        var prefs = JsonSerializer.Deserialize<MeterPreferences>(JsonSerializer.Serialize(meter.Preferences))!;
        var status = Ui.Text(""); var tabs = new TabControl(); var items = new List<TabItem>();
        var read = new List<Action>();
        void Group(string title, params (string Key,string Label)[] fields)
        {
            var panel = Ui.Stack();
            foreach(var (key,label) in fields)
            {
                var property = typeof(MeterPreferences).GetProperty(key)!;
                Control editor;
                if(property.PropertyType == typeof(bool))
                { var box = new CheckBox { Content = label, IsChecked = (bool)property.GetValue(prefs)! }; editor = box; read.Add(()=>property.SetValue(prefs,box.IsChecked==true)); }
                else
                {
                    var box = new TextBox { Name=key, Text = Convert.ToString(property.GetValue(prefs),CultureInfo.InvariantCulture) ?? "" };
                    editor = Ui.Stack(Ui.Text(label),box);
                    read.Add(()=> {
                        object? value = property.PropertyType == typeof(string) ? box.Text??"" : Nullable.GetUnderlyingType(property.PropertyType) is {} nullable ? string.IsNullOrWhiteSpace(box.Text) ? null : Convert.ChangeType(box.Text,nullable,CultureInfo.InvariantCulture) : Convert.ChangeType(box.Text,property.PropertyType,CultureInfo.InvariantCulture);
                        property.SetValue(prefs,value);
                    });
                }
                panel.Children.Add(editor); EditorCount++;
            }
            items.Add(new TabItem { Header = title, FontSize = 13, Content = Ui.Scroll(panel) });
        }
        Group("General", ("AutoCapture","Start capture automatically on launch"),("BossResetDetection","End encounters on observed boss death"),("ShowBerserk","Show observed boss berserk countdown"),("UpdateMs","Refresh interval (100–2000 ms)"),("IdleSeconds","Encounter idle timeout (5–600 seconds)"),("DummySeconds","Training dummy duration (0 = unlimited)"),("DpsCalculation","DPS calculation: Encounter or Active"),("CaptureScope","Capture scope: Self only or Self and party"),("DummyNpcIds","Additional training dummy NPC IDs (comma separated)"));
        Group("Capture",("Adapter","Adapter name (for example enp4s0 or any)"),("Port","Game TCP port (blank = 13328)"));
        Group("Appearance",("AccentColor","Accent (#RRGGBB)"),("PanelColor","Background (#RRGGBB)"),("LogoColor","Title color (#RRGGBB)"),("LogoFollowsAccent","Title follows accent"),("OverlayOpacity","Combat opacity (0.2–1)"),("UiScale","Combat scale (0.8–1.5)"),("VisiblePlayers","Visible players (1–12)"),("CombatStrip","One-line combat strip"),("HideNames","Hide other player names"),("HideTotalDamage","Hide total damage"),("ShowPartyDps","Show party DPS"),("FadeIdle","Fade when idle"),("FadedOpacity","Idle opacity (0.1–1)"),("FadeAfterSeconds","Fade after seconds (10–600)"),("PositionLocked","Lock combat window position"),("RememberClickThrough","Restore click-through on launch (configure a recovery binding first)"));
        Group("Events",("ShowEventOverlay","Open event overlay"),("EventOverlayScale","Event scale (blank = combat scale)"),("EventOverlayOpacity","Event opacity (blank = combat opacity)"),("EventOverlayLocked","Lock event window position"),("ShowShugoTimer","Show Shugo timer"),("ShowRiftTimer","Show Rift timer"),("ShowInvasionTimer","Show possible invasion timer"),("ShowFieldBossTimers","Show field bosses"),("WorldTimerServerId","Field boss server ID (blank = current character)"),("RiftShiftMinutes","Rift schedule offset in minutes"),("GameTimerAlerts","Notify for game timers"),("GameTimerLeadMinutes","Game alert lead minutes, e.g. 5,1"),("EventVisibility","Event overlay: Always or Outside fights"));
        Group("Pets",("ShowPetOverlay","Open pet overlay"),("ShowPetPickups","Show soul pickup card"),("PetOnlyPinned","Show pinned pets only"),("PetHideCompleted","Hide completed pets"),("PetOverlayScale","Pet scale (0.8–1.5)"),("PetOverlayOpacity","Pet opacity (0.2–1)"),("PetOverlayLocked","Lock pet window position"));
        Group("Share",("CopyFormat","Chat format: Single line or Multi-line"),("Clock24","24-hour history clock"));
        var name=new TextBox{Watermark="Exact character name"};var server=new TextBox{Watermark="Server ID"};var gs=new TextBox{Watermark="Gear score (optional)"};var cp=new TextBox{Watermark="Combat power (optional)"};var ratingStatus=Ui.Text("");var ratings=new StackPanel();
        void RefreshRatings(){ratings.Children.Clear();foreach(var rating in prefs.OwnScoreEntries.ToArray())ratings.Children.Add(Ui.Button($"Remove {rating.Name} / {rating.ServerId}: GS {rating.GearScore} CP {rating.CombatPower}",()=>{prefs.OwnScoreEntries.Remove(rating);RefreshRatings();}));}
        RefreshRatings();
        items.Add(new TabItem{Header="Scores",FontSize=13,Content=Ui.Scroll(Ui.Stack(Ui.Text("Manual personal scores",20),Ui.Text("Fallback for your own character only. Observed values take precedence. Manual values are marked *. Name and server must match the character received from the game."),name,server,gs,cp,Ui.Button("Add fallback",()=>{
            if(!int.TryParse(server.Text,out int id)){ratingStatus.Text="Enter a valid server ID.";return;}
            int? score=int.TryParse(gs.Text,out int g)?g:null,power=int.TryParse(cp.Text,out int c)?c:null;
            var entry=new AscendAPP.Core.Game.ManualPlayerRating(name.Text??"",id,score,power,DateTimeOffset.UtcNow);
            if(!entry.Valid){ratingStatus.Text="Enter a character, server 1000–9999 and valid GS or CP.";return;}
            prefs.OwnScoreEntries.RemoveAll(x=>x.Name==entry.Name&&x.ServerId==entry.ServerId);prefs.OwnScoreEntries.Add(entry);RefreshRatings();ratingStatus.Text="Added; Save and apply to persist.";
        }),ratingStatus,ratings))});
        items.Add(new TabItem { Header = "Linux", FontSize = 13, Content = Ui.Scroll(Ui.Stack(
            Ui.Text("Hyprland integration",20),Ui.Text("Global keys are provided by Hyprland binds, which work while the game is focused. Use the Linux menu to copy the commands. Click-through uses the X11 input shape; hold Alt over an XWayland window to interact temporarily. Native Wayland game stacking and screenshot permissions are controlled by your compositor."),
            Ui.Text("Loot-row OCR is opt-in. Install grim, slurp, tesseract and English language data. Choose the loot text region from the Linux menu, then enable the reader. OCR locates pet names; collection values always come from game packets."),
            Ui.Text("Updates",20),Ui.Text("Linux updates are downloaded from the Linux preview GitHub page. Windows installers and the Windows automatic updater are never launched on Linux."),
            Ui.Button("Open Linux downloads",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open","https://github.com/boronadziesieciu/FooMeter/tree/linux-preview/linux-preview"){UseShellExecute=false})))) });
        tabs.ItemsSource = items;
        var layout = new DockPanel { Margin = new(14) };
        var footer = Ui.Stack(status,
            Ui.Async("Import settings…",async()=> {
                var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Import FooMeter settings",AllowMultiple=false});
                if(files.FirstOrDefault()?.TryGetLocalPath() is not {} path)return;
                if(!PreferencesStore.TryRead(path,out var imported))throw new InvalidDataException("Invalid settings file.");
                string current=Path.Combine(meter.ProfileDirectory,"settings.json");
                if(!meter.Demo&&File.Exists(current))File.Copy(current,current+".backup-"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"));
                meter.SavePreferences(imported);changed();Close();
            },e=>status.Text=e.Message),
            Ui.Async("Export settings…",async()=> {
                var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Export settings",SuggestedFileName="FooMeter-settings.json"});
                if(file?.TryGetLocalPath() is {} path)PreferencesStore.Write(path,meter.Preferences);
            },e=>status.Text=e.Message),Ui.Button("Save and apply",()=> {
            try { foreach(var action in read) action();
                if(!string.IsNullOrWhiteSpace(prefs.Port)&&(!int.TryParse(prefs.Port,out int port)||port is <1 or >65535)) throw new FormatException("TCP port must be 1–65535.");
                meter.SavePreferences(prefs); changed(); Close();
            } catch(Exception e) { status.Text = "Settings were not saved: " + e.Message; }
        }));
        DockPanel.SetDock(footer,Dock.Bottom); layout.Children.Add(footer); layout.Children.Add(tabs); Content=layout;
    }
}
