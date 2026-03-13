using Microsoft.EntityFrameworkCore;
using NoRecoilApp.Maui.Models;

namespace NoRecoilApp.Maui.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WeaponProfile> WeaponProfiles => Set<WeaponProfile>();
    public DbSet<SprayPoint> SprayPoints => Set<SprayPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var weapon = modelBuilder.Entity<WeaponProfile>();
        weapon.ToTable("weapon_profiles");
        weapon.HasKey(x => x.Id);
        weapon.HasIndex(x => new { x.OperatorName, x.WeaponName }).IsUnique();
        weapon.Property(x => x.OperatorName).IsRequired().HasMaxLength(64);
        weapon.Property(x => x.WeaponName).IsRequired().HasMaxLength(64);
        weapon.Property(x => x.Side).HasDefaultValue("ATK").HasMaxLength(4);
        weapon.Property(x => x.StrengthY).HasDefaultValue(4.5);
        weapon.Property(x => x.StrengthX).HasDefaultValue(0.0);
        weapon.Property(x => x.Delay).HasDefaultValue(0.012);
        weapon.Property(x => x.Smooth).HasDefaultValue(5);
        weapon.Property(x => x.AccelFactor).HasDefaultValue(1.2);
        weapon.Property(x => x.MaxProgression).HasDefaultValue(2.5);
        weapon.Property(x => x.UseSprayPattern).HasDefaultValue(false);
        weapon.Property(x => x.FireRateRpm).HasDefaultValue(750.0);
        weapon.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        weapon.HasMany(x => x.SprayPoints)
              .WithOne(x => x.WeaponProfile)
              .HasForeignKey(x => x.WeaponProfileId)
              .OnDelete(DeleteBehavior.Cascade);

        var spray = modelBuilder.Entity<SprayPoint>();
        spray.ToTable("spray_points");
        spray.HasKey(x => x.Id);
        spray.HasIndex(x => new { x.WeaponProfileId, x.Index }).IsUnique();
        spray.Property(x => x.X).HasDefaultValue(0.0);
        spray.Property(x => x.Y).HasDefaultValue(0.0);
    }
}
