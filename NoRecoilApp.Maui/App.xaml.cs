using NoRecoilApp.Maui.Services;

namespace NoRecoilApp.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Destroying += async (_, _) =>
        {
            if (MauiProgram.Services.GetService(typeof(IRecoilEngineService)) is IRecoilEngineService engine)
            {
                await engine.StopAsync();
            }
        };

        return window;
    }
}
