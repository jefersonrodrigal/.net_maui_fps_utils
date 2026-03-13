using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NoRecoilApp.Maui.Services;

public sealed class RecoilEngineService : IRecoilEngineService, IDisposable
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool Beep(uint dwFreq, uint dwDuration);

    private const int IdlePollMs = 10;
    private const int ArmedPollMs = 4;

    private readonly object _sync = new();
    private readonly object _settingsSync = new();
    private readonly Stopwatch _delaySw = new();
    private static readonly Random _random = Random.Shared;

    private RecoilSettings _settings = RecoilSettings.Default;
    private CancellationTokenSource? _cts;
    private Task? _worker;

    private volatile bool _lmbRaw;
    private volatile bool _rmbRaw;
    private volatile bool _rawInputActive;

    private volatile bool _enabled;
    private volatile bool _adsEnabled;
    private volatile int _adsHoldMilliseconds = 35;

    private volatile bool _soundEnabled = true;

    private bool _lastF2Pressed;
    private bool _lastF3Pressed;

    private bool _adsInjectedRmb;
    private long _adsInjectedAtMs;

    private bool _disposed;

    public bool IsPlatformSupported => OperatingSystem.IsWindows();
    public bool Enabled => _enabled;
    public bool AdsEnabled => _adsEnabled;
    public int AdsHoldMilliseconds => _adsHoldMilliseconds;
    public bool SoundEnabled => _soundEnabled;

    public event EventHandler<bool>? EnabledChanged;
    public event EventHandler<bool>? AdsEnabledChanged;

    public RecoilEngineService()
    {
        RawInputListener.OnLeftButtonChanged += v => { _lmbRaw = v; _rawInputActive = true; };
        RawInputListener.OnRightButtonChanged += v => { _rmbRaw = v; _rawInputActive = true; };
    }

    private void SoundOn() => PlaySound(() => Beep(1200, 80));
    private void SoundOff() => PlaySound(() => Beep(500, 80));
    private void SoundAdsOn() => PlaySound(() => Beep(900, 80));
    private void SoundAdsOff() => PlaySound(() => Beep(400, 80));

    private void PlaySound(Action sound)
    {
        if (!_soundEnabled || !IsPlatformSupported) return;
        Task.Run(sound);
    }

    public void SetSoundEnabled(bool enabled) => _soundEnabled = enabled;

    private bool GetLmb() => _rawInputActive
        ? _lmbRaw
        : NativeInput.IsKeyDown(NativeInput.VkLButton);

    private bool GetRmb() => _rawInputActive
        ? _rmbRaw
        : NativeInput.IsKeyDown(NativeInput.VkRButton);

    public void SetProfile(RecoilSettings settings)
    {
        lock (_settingsSync) _settings = settings;
    }

    public void SetAdsHoldMilliseconds(int ms) =>
        _adsHoldMilliseconds = Math.Clamp(ms, 0, 250);

    public void ToggleEnabled()
    {
        if (!IsPlatformSupported) return;
        var v = !_enabled;
        _enabled = v;
        if (v) SoundOn(); else SoundOff();
        EnabledChanged?.Invoke(this, v);
    }

    public void ToggleAds()
    {
        if (!IsPlatformSupported) return;
        var v = !_adsEnabled;
        _adsEnabled = v;
        if (!v) ReleaseInjectedRmb();
        if (v) SoundAdsOn(); else SoundAdsOff();
        AdsEnabledChanged?.Invoke(this, v);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (_worker is not null) return Task.CompletedTask;

            RawInputListener.Start();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _worker = Task.Factory.StartNew(
                () => RunLoop(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Task? worker;
        lock (_sync)
        {
            worker = _worker;
            _cts?.Cancel();
            _worker = null;
        }

        if (worker is not null)
        {
            try { await worker.WaitAsync(ct); }
            catch (OperationCanceledException) { }
        }

        RawInputListener.Stop();
        ReleaseInjectedRmb();
    }

    private void RunLoop(CancellationToken ct)
    {
        if (!IsPlatformSupported) return;

        TimeBeginPeriod(1);

        var burstTime = Stopwatch.StartNew();
        var inBurst = false;
        var lastBulletIndex = -1;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                HandleHotkeys();

                var lmb = GetLmb();
                var rmbPhysical = GetRmb();
                var rmbReady = ProcessAdsAssist(lmb, rmbPhysical);

                RecoilSettings current;
                lock (_settingsSync) current = _settings;

                if (!_enabled)
                {
                    inBurst = false;
                    lastBulletIndex = -1;
                    Thread.Sleep(IdlePollMs);
                    continue;
                }

                if (!lmb || !rmbReady)
                {
                    inBurst = false;
                    lastBulletIndex = -1;
                    Thread.Sleep(ArmedPollMs);
                    continue;
                }

                if (!inBurst)
                {
                    burstTime.Restart();
                    inBurst = true;
                    lastBulletIndex = -1;
                }

                var elapsed = burstTime.ElapsedTicks / (double)Stopwatch.Frequency;

                // ─────────────────────────────────────────────────────────
                // CÁLCULO DE COMPENSAÇÃO
                // ─────────────────────────────────────────────────────────
                double tx, ty;

                if (current.UseSprayPattern && current.SprayPattern.Length > 0)
                {
                    // Nível 3 — Spray Table: índice exato por bala via RPM
                    var secondsPerBullet = 60.0 / current.FireRateRpm;
                    var bulletIndex = (int)(elapsed / secondsPerBullet);
                    bulletIndex = Math.Clamp(bulletIndex, 0, current.SprayPattern.Length - 1);

                    // Aguarda próxima bala sem duplicar compensação
                    if (bulletIndex == lastBulletIndex)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    lastBulletIndex = bulletIndex;

                    var point = current.SprayPattern[bulletIndex];

                    // Ruído Gaussian mínimo para humanizar sem distorcer padrão
                    tx = point.X + NextGaussian() * 0.12;
                    ty = point.Y + NextGaussian() * 0.08;
                }
                else
                {
                    // Nível 1 — Temporal: comportamento original (fallback)
                    var progression = Math.Min(
                        current.MaxProgression,
                        1.0 + Math.Pow(Math.Max(elapsed, 0.0), current.AccelFactor));

                    ty = current.StrengthY * progression;
                    tx = (current.StrengthX + NextGaussian() * 0.3) * (progression * 0.5);
                }
                // ─────────────────────────────────────────────────────────

                ExecuteHumanizedMove(tx, ty, current.Delay, current.Smooth, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            TimeEndPeriod(1);
            ReleaseInjectedRmb();
        }
    }

    private void HandleHotkeys()
    {
        var f2 = NativeInput.IsKeyDown(NativeInput.VkF2);
        if (f2 && !_lastF2Pressed) ToggleEnabled();
        _lastF2Pressed = f2;

        var f3 = NativeInput.IsKeyDown(NativeInput.VkF3);
        if (f3 && !_lastF3Pressed) ToggleAds();
        _lastF3Pressed = f3;
    }

    private bool ProcessAdsAssist(bool lmb, bool rmbPhysical)
    {
        if (!_adsEnabled)
        {
            ReleaseInjectedRmb();
            return rmbPhysical;
        }

        if (_adsInjectedRmb)
        {
            if (!lmb) { ReleaseInjectedRmb(); return rmbPhysical; }
            if (rmbPhysical) return true;
            return (Environment.TickCount64 - _adsInjectedAtMs) >= _adsHoldMilliseconds;
        }

        if (lmb && !rmbPhysical)
        {
            NativeInput.SendRightButtonDown();
            _adsInjectedRmb = true;
            _adsInjectedAtMs = Environment.TickCount64;
            return _adsHoldMilliseconds <= 0;
        }

        return rmbPhysical;
    }

    private void ReleaseInjectedRmb()
    {
        if (!_adsInjectedRmb) return;
        NativeInput.SendRightButtonUp();
        _adsInjectedRmb = false;
        _adsInjectedAtMs = 0;
    }

    private void ExecuteHumanizedMove(double tx, double ty, double durationSeconds, int steps, CancellationToken ct)
    {
        var safeSteps = Math.Max(1, steps);
        var stepDelayMs = (durationSeconds / safeSteps) * 1000.0;
        double lastX = 0, lastY = 0;

        for (var i = 1; i <= safeSteps; i++)
        {
            if (ct.IsCancellationRequested) break;

            var t = (double)i / safeSteps;
            var noise = 0.97 + _random.NextDouble() * 0.06;
            var currentX = tx * t * noise;
            var currentY = ty * t * noise;
            var dx = (int)Math.Round(currentX - lastX);
            var dy = (int)Math.Round(currentY - lastY);

            if (dx != 0 || dy != 0)
                NativeInput.SendRelativeMouseMove(dx, dy);

            lastX = currentX;
            lastY = currentY;

            HighResolutionDelay(stepDelayMs);
        }
    }

    private void HighResolutionDelay(double delayMs)
    {
        if (delayMs <= 0) return;
        var targetTicks = delayMs * TimeSpan.TicksPerMillisecond;
        _delaySw.Restart();

        while (true)
        {
            var remaining = targetTicks - _delaySw.ElapsedTicks;
            if (remaining <= 0) break;
            if (remaining > TimeSpan.TicksPerMillisecond)
                Thread.Sleep(0);
            else
                Thread.SpinWait(20);
        }
    }

    private static double NextGaussian()
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        RawInputListener.Stop();
        ReleaseInjectedRmb();
    }
}
