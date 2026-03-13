using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoRecoilApp.Maui.Data;
using NoRecoilApp.Maui.Services;
using NoRecoilApp.Maui.ViewModels;

namespace NoRecoilApp.Maui;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = default!;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // C:\Users\User\AppData\Local\User Name\com.companyname.norecoilapp.maui\Data\
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "recoil_profiles.db");

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Cache=Shared;Pooling=True"));

        builder.Services.AddSingleton<IWeaponProfileRepository, WeaponProfileRepository>();
        builder.Services.AddSingleton<IRecoilEngineService, RecoilEngineService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}
