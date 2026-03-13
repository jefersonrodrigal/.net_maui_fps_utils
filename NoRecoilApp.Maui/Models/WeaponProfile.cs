using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using NoRecoilApp.Maui.Services;

namespace NoRecoilApp.Maui.Models;

[Table("weapon_profiles")]
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

    // ── Modo Temporal ─────────────────────────────────────────────────────
    public double StrengthY { get; set; } = 4.5;
    public double StrengthX { get; set; } = 0.0;
    public double Delay { get; set; } = 0.012;
    public int Smooth { get; set; } = 5;
    public double AccelFactor { get; set; } = 1.2;
    public double MaxProgression { get; set; } = 2.5;

    // ── Spray Table ───────────────────────────────────────────────────────
    public bool UseSprayPattern { get; set; } = false;
    public double FireRateRpm { get; set; } = 750;

    // ── Navegação EF Core ─────────────────────────────────────────────────
    public List<SprayPoint> SprayPoints { get; set; } = [];

    // ── Metadados ─────────────────────────────────────────────────────────
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // ── UI ────────────────────────────────────────────────────────────────
    [NotMapped]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            OnPropertyChanged();
        }
    }

    [NotMapped]
    public string DisplayKey => $"{OperatorName} | {WeaponName}";

    // ── Conversão para RecoilSettings ─────────────────────────────────────
    public RecoilSettings ToRecoilSettings() =>
        UseSprayPattern && SprayPoints.Count > 0
            ? RecoilSettings.WithSpray(
                fireRateRpm: FireRateRpm,
                smooth: Smooth,
                delay: Delay,
                pattern: SprayPoints
                    .OrderBy(p => p.Index)
                    .Select(p => new RecoilPoint(p.X, p.Y))
                    .ToArray())
            : new RecoilSettings(
                StrengthY: StrengthY,
                StrengthX: StrengthX,
                Delay: Delay,
                Smooth: Smooth,
                AccelFactor: AccelFactor,
                MaxProgression: MaxProgression,
                UseSprayPattern: false,
                FireRateRpm: FireRateRpm,
                SprayPattern: []);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
