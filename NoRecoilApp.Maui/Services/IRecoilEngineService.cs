namespace NoRecoilApp.Maui.Services;

public interface IRecoilEngineService
{
    bool IsPlatformSupported { get; }

    bool Enabled { get; }

    bool AdsEnabled { get; }

    int AdsHoldMilliseconds { get; }

    event EventHandler<bool>? EnabledChanged;

    event EventHandler<bool>? AdsEnabledChanged;

    void SetProfile(RecoilSettings settings);

    void ToggleEnabled();

    void ToggleAds();

    void SetAdsHoldMilliseconds(int milliseconds);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
