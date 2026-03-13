namespace NoRecoilApp.Maui.Services;

public readonly record struct RecoilPoint(double X, double Y);

public readonly record struct RecoilSettings(
    double StrengthY,
    double StrengthX,
    double Delay,
    int Smooth,
    double AccelFactor,
    double MaxProgression,
    bool UseSprayPattern,
    double FireRateRpm,
    RecoilPoint[] SprayPattern)
{
    public static RecoilSettings Default => new(
        StrengthY: 4.5,
        StrengthX: 0.0,
        Delay: 0.012,
        Smooth: 5,
        AccelFactor: 1.2,
        MaxProgression: 2.5,
        UseSprayPattern: false,
        FireRateRpm: 750,
        SprayPattern: []);

    public static RecoilSettings WithSpray(
        RecoilPoint[] pattern,
        double fireRateRpm = 750,
        int smooth = 5,
        double delay = 0.012) => new(
            StrengthY: 4.5,
            StrengthX: 0.0,
            Delay: delay,
            Smooth: smooth,
            AccelFactor: 1.2,
            MaxProgression: 2.5,
            UseSprayPattern: true,
            FireRateRpm: fireRateRpm,
            SprayPattern: pattern);
}
