using System.Windows;

namespace NalaApps.Action.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        SplashWindow splash = new();
        splash.Show();
        await splash.PlayAsync();
        splash.Close();

        MainWindow mainWindow = new()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Opacity = 0
        };

        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();

        await FadeInMainWindowAsync(mainWindow);
    }

    private static Task FadeInMainWindowAsync(Window window)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        System.Windows.Media.Animation.DoubleAnimation animation = new(
            0,
            1,
            new Duration(TimeSpan.FromMilliseconds(350)))
        {
            FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd,
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        };

        animation.Completed += (_, _) => completion.TrySetResult();
        window.BeginAnimation(UIElement.OpacityProperty, animation);
        return completion.Task;
    }
}
