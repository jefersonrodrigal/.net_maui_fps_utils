#if WINDOWS
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif
using NoRecoilApp.Maui.Services;


namespace NoRecoilApp.Maui;

public partial class App : Application
{
    private const int WindowWidth = 640;
    private const int WindowHeight = 930;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Created += (_, _) => ConfigureWindow(window);
        window.Destroying += async (_, _) =>
        {
            if (MauiProgram.Services.GetService(typeof(IRecoilEngineService)) is IRecoilEngineService engine)
            {
                await engine.StopAsync();
            }
        };

        return window;
    }

    private void ConfigureWindow(Window mauiWindow)
    {
#if WINDOWS
        if (mauiWindow.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winUiWindow)
            return;

        var appWindow = winUiWindow.AppWindow;
        if (appWindow is null) return;

        appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable   = false;
            presenter.IsMaximizable = false;
        }

        var displayArea = DisplayArea.GetFromWindowId(
            appWindow.Id,
            DisplayAreaFallback.Nearest);

        if (displayArea is not null)
        {
            var cx = (displayArea.WorkArea.Width  - WindowWidth)  / 2;
            var cy = (displayArea.WorkArea.Height - WindowHeight) / 2;
            appWindow.Move(new PointInt32 { X = cx, Y = cy });
        }
#endif
    }
}
