using System.ComponentModel.DataAnnotations.Schema;

namespace NoRecoilApp.Maui.Models;

[Table("spray_points")]
public sealed class SprayPoint
{
    public int Id { get; set; }
    public int WeaponProfileId { get; set; }
    public int Index { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    public WeaponProfile? WeaponProfile { get; set; }
}
