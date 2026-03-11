using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace NoRecoilApp.Maui.Models;

public sealed class WeaponProfile : INotifyPropertyChanged
{
    private bool _isActive;

    public int Id { get; set; }

    [MaxLength(64)]
    public string OperatorName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string WeaponName { get; set; } = string.Empty;

    [MaxLength(4)]
    public string Side { get; set; } = "ATK";

    public double StrengthY { get; set; } = 4.5;

    public double StrengthX { get; set; } = 0.0;

    public double Delay { get; set; } = 0.012;

    public int Smooth { get; set; } = 5;

    public double AccelFactor { get; set; } = 1.2;

    public double MaxProgression { get; set; } = 2.5;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public string DisplayKey => $"{OperatorName} | {WeaponName}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
