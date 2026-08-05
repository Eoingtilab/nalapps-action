using System.Windows;
using System.Windows.Media.Animation;

namespace NalaApps.Action.App;

public partial class SplashWindow : Window
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(4);

    public SplashWindow()
    {
        InitializeComponent();
    }

    public async Task PlayAsync()
    {
        await AnimateOpacityAsync(0, 1, FadeDuration);
        await Task.Delay(HoldDuration);
        await AnimateOpacityAsync(1, 0, FadeDuration);
    }

    private Task AnimateOpacityAsync(double from, double to, TimeSpan duration)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DoubleAnimation animation = new(from, to, new Duration(duration))
        {
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        animation.Completed += (_, _) => completion.TrySetResult();
        BeginAnimation(OpacityProperty, animation);
        return completion.Task;
    }
}
