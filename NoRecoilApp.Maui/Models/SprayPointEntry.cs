using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoRecoilApp.Maui.Models;

public sealed class SprayPointEntry : INotifyPropertyChanged
{
    private int _index;
    private double _x;
    private double _y;

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
    }

    public double X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
    }

    public double Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(Label)); }
    }

    public string Label => $"#{Index + 1}   X: {X:F2}   Y: {Y:F2}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
