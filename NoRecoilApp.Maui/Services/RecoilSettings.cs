namespace NoRecoilApp.Maui.Services;

public readonly record struct RecoilSettings(
    double StrengthY,
    double StrengthX,
    double Delay,
    int Smooth,
    double AccelFactor,
    double MaxProgression)
{
    public static RecoilSettings Default => new(4.5, 0.0, 0.012, 5, 1.2, 2.5);
}
