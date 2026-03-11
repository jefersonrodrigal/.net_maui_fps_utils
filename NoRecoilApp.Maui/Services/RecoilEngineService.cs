using System.Diagnostics;

namespace NoRecoilApp.Maui.Services;

public sealed class RecoilEngineService : IRecoilEngineService
{
    private const int IdlePollMs = 10;
    private const int ArmedPollMs = 4;
    private const int ActivePollMs = 1;

    private readonly object _sync = new();
    private readonly Random _random = new();

    private RecoilSettings _settings = RecoilSettings.Default;
    private CancellationTokenSource? _cts;
    private Task? _worker;

    private bool _enabled;
    private bool _adsEnabled;
    private int _adsHoldMilliseconds = 35;

    private bool _lastF2Pressed;
    private bool _lastF3Pressed;

    private bool _adsInjectedRmb;
    private long _adsInjectedAtMs;

    public bool IsPlatformSupported => OperatingSystem.IsWindows();

    public bool Enabled
    {
        get
        {
            lock (_sync)
            {
                return _enabled;
            }
        }
    }

    public bool AdsEnabled
    {
        get
        {
            lock (_sync)
            {
                return _adsEnabled;
            }
        }
    }

    public int AdsHoldMilliseconds
    {
        get
        {
            lock (_sync)
            {
                return _adsHoldMilliseconds;
            }
        }
    }

    public event EventHandler<bool>? EnabledChanged;

    public event EventHandler<bool>? AdsEnabledChanged;

    public void SetProfile(RecoilSettings settings)
    {
        lock (_sync)
        {
            _settings = settings;
        }
    }

    public void ToggleEnabled()
    {
        if (!IsPlatformSupported)
        {
            return;
        }

        bool newValue;
        lock (_sync)
        {
            _enabled = !_enabled;
            newValue = _enabled;
        }

        EnabledChanged?.Invoke(this, newValue);
        PlayToggleBeep(newValue);
    }

    public void ToggleAds()
    {
        if (!IsPlatformSupported)
        {
            return;
        }

        bool newValue;
        lock (_sync)
        {
            _adsEnabled = !_adsEnabled;
            newValue = _adsEnabled;

            if (!newValue)
            {
                ReleaseInjectedRmbUnsafe();
            }
        }

        AdsEnabledChanged?.Invoke(this, newValue);
        PlayToggleBeep(newValue);
    }

    public void SetAdsHoldMilliseconds(int milliseconds)
    {
        lock (_sync)
        {
            _adsHoldMilliseconds = Math.Clamp(milliseconds, 0, 250);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_worker is not null)
            {
                return Task.CompletedTask;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _worker = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? worker;

        lock (_sync)
        {
            if (_worker is null)
            {
                return;
            }

            _cts?.Cancel();
            worker = _worker;
            _worker = null;
        }

        if (worker is not null)
        {
            try
            {
                await worker.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        lock (_sync)
        {
            ReleaseInjectedRmbUnsafe();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        if (!IsPlatformSupported)
        {
            return;
        }

        var burstTime = Stopwatch.StartNew();
        var inBurst = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HandleHotkeys();

                var lmb = NativeInput.IsKeyDown(NativeInput.VkLButton);
                var rmbPhysical = NativeInput.IsKeyDown(NativeInput.VkRButton);
                var rmbReady = ProcessAdsAssist(lmb, rmbPhysical);

                RecoilSettings current;
                bool enabled;
                lock (_sync)
                {
                    current = _settings;
                    enabled = _enabled;
                }

                if (!enabled)
                {
                    inBurst = false;
                    await Task.Delay(IdlePollMs, cancellationToken);
                    continue;
                }

                if (!lmb || !rmbReady)
                {
                    inBurst = false;
                    await Task.Delay(ArmedPollMs, cancellationToken);
                    continue;
                }

                if (!inBurst)
                {
                    burstTime.Restart();
                    inBurst = true;
                }

                var elapsed = burstTime.Elapsed.TotalSeconds;
                var progression = 1.0 + Math.Pow(Math.Max(elapsed, 0.0), current.AccelFactor);
                progression = Math.Min(current.MaxProgression, progression);

                var ty = current.StrengthY * progression;
                var tx = (current.StrengthX + NextGaussian() * 0.3) * (progression * 0.5);

                await ExecuteHumanizedMoveAsync(tx, ty, current.Delay, current.Smooth, cancellationToken);
                await Task.Delay(ActivePollMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_sync)
            {
                ReleaseInjectedRmbUnsafe();
            }
        }
    }

    private void HandleHotkeys()
    {
        var f2Down = NativeInput.IsKeyDown(NativeInput.VkF2);
        if (f2Down && !_lastF2Pressed)
        {
            ToggleEnabled();
        }

        _lastF2Pressed = f2Down;

        var f3Down = NativeInput.IsKeyDown(NativeInput.VkF3);
        if (f3Down && !_lastF3Pressed)
        {
            ToggleAds();
        }

        _lastF3Pressed = f3Down;
    }

    private bool ProcessAdsAssist(bool lmb, bool rmbPhysical)
    {
        lock (_sync)
        {
            if (!_adsEnabled)
            {
                ReleaseInjectedRmbUnsafe();
                return rmbPhysical;
            }

            if (_adsInjectedRmb)
            {
                if (!lmb)
                {
                    ReleaseInjectedRmbUnsafe();
                    return rmbPhysical;
                }

                if (rmbPhysical)
                {
                    return true;
                }

                var elapsedMs = Environment.TickCount64 - _adsInjectedAtMs;
                return elapsedMs >= _adsHoldMilliseconds;
            }

            if (lmb && !rmbPhysical)
            {
                NativeInput.SendRightButtonDown();
                _adsInjectedRmb = true;
                _adsInjectedAtMs = Environment.TickCount64;
                return false;
            }

            return rmbPhysical;
        }
    }

    private void ReleaseInjectedRmbUnsafe()
    {
        if (!_adsInjectedRmb)
        {
            return;
        }

        NativeInput.SendRightButtonUp();
        _adsInjectedRmb = false;
        _adsInjectedAtMs = 0;
    }

    private async Task ExecuteHumanizedMoveAsync(double tx, double ty, double durationSeconds, int steps, CancellationToken cancellationToken)
    {
        var safeSteps = Math.Max(1, steps);
        var stepDelay = TimeSpan.FromSeconds(Math.Max(0.001, durationSeconds / safeSteps));

        double lastX = 0;
        double lastY = 0;

        for (var i = 1; i <= safeSteps; i++)
        {
            var t = (double)i / safeSteps;
            var noise = 0.97 + _random.NextDouble() * 0.06;

            var currentX = tx * t * noise;
            var currentY = ty * t * noise;

            var dx = (int)Math.Round(currentX - lastX);
            var dy = (int)Math.Round(currentY - lastY);

            NativeInput.SendRelativeMouseMove(dx, dy);

            lastX = currentX;
            lastY = currentY;

            await Task.Delay(stepDelay, cancellationToken);
        }
    }

    private double NextGaussian()
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static void PlayToggleBeep(bool enabled)
    {
#if WINDOWS
        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(enabled ? 880 : 520, 55);
            }
            catch
            {
            }
        });
#endif
    }
}
