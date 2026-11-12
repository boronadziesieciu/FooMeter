using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AscendAPP.Core.Settings;
namespace AscendAPP.Desktop;
public sealed class HotkeyDialog : Window
{
    public string Chord {get;private set;}="None";
    public HotkeyDialog()
    {
        Foreground=(System.Windows.Media.Brush)Application.Current.Resources["Ink"];Background=(System.Windows.Media.Brush)Application.Current.Resources["Surface"];
        Title="Set global hotkey";Width=380;Height=155;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Content=new TextBlock{Text=@"Press your key combination

Escape cancels · Backspace clears",Margin=new Thickness(22),TextWrapping=TextWrapping.Wrap,FontSize=13};
        PreviewKeyDown+=(_,e)=>{
            e.Handled=true;var key=e.Key==Key.System?e.SystemKey:e.Key;
            if(key==Key.Escape){DialogResult=false;return;}
            if(key==Key.Back&&Keyboard.Modifiers==ModifierKeys.None){Chord="None";DialogResult=true;return;}
            var parts=new List<string>();var m=Keyboard.Modifiers;if(m.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(m.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(m.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(m.HasFlag(ModifierKeys.Windows))parts.Add("Win");parts.Add(key.ToString());
            string chord=string.Join("+",parts);if(HotkeyChord.TryParse(chord,out var result)&&result is not null){Chord=chord;DialogResult=true;}
        };
    }
}
