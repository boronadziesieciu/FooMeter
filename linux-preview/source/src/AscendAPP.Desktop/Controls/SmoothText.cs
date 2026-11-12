using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
namespace AscendAPP.Desktop.Controls;

/// <summary>Present the actual latest text immediately; briefly soften its opacity.
/// No interpolated DPS values, render-loop subscription or idle animation timer.</summary>
public sealed class SmoothText:TextBlock
{
    public static readonly DependencyProperty TransitionsEnabledProperty=DependencyProperty.Register(
        nameof(TransitionsEnabled),typeof(bool),typeof(SmoothText),new PropertyMetadata(false,EnabledChanged));
    public bool TransitionsEnabled {get=>(bool)GetValue(TransitionsEnabledProperty);set=>SetValue(TransitionsEnabledProperty,value);}
    private static readonly DoubleAnimation Fade=MakeFade();
    private AnimationClock? _clock;
    internal bool TransitionRunning=>_clock is not null;
    private static DoubleAnimation MakeFade()
    {
        var fade=new DoubleAnimation(0.82,1,TimeSpan.FromMilliseconds(120)){FillBehavior=FillBehavior.Stop};
        Timeline.SetDesiredFrameRate(fade,30);fade.Freeze();return fade;
    }
    static SmoothText(){TextProperty.OverrideMetadata(typeof(SmoothText),new FrameworkPropertyMetadata("",TextChanged));}
    public SmoothText(){Unloaded+=(_,_)=>StopTransition();IsVisibleChanged+=(_,_)=>{if(!IsVisible)StopTransition();};}
    private static void EnabledChanged(DependencyObject target,DependencyPropertyChangedEventArgs e){if(!(bool)e.NewValue)((SmoothText)target).StopTransition();}
    internal static bool ShouldAnimate(bool enabled,bool loaded,bool visible,bool systemAnimations)=>enabled&&loaded&&visible&&systemAnimations;
    private static void TextChanged(DependencyObject target,DependencyPropertyChangedEventArgs e)
    {
        var text=(SmoothText)target;
        if(!ShouldAnimate(text.TransitionsEnabled,text.IsLoaded,text.IsVisible,SystemParameters.ClientAreaAnimation))return;
        text.StartTransition();
    }
    internal void StartTransition()
    {
        StopTransition();_clock=Fade.CreateClock();_clock.Completed+=Finished;
        ApplyAnimationClock(OpacityProperty,_clock);
    }
    private void Finished(object? sender,EventArgs e)=>StopTransition();
    internal void StopTransition()
    {
        if(_clock is null)return;
        _clock.Completed-=Finished;ApplyAnimationClock(OpacityProperty,null);_clock=null;
    }
}
