using Microsoft.EntityFrameworkCore;
using NoRecoilApp.Maui.Models;

namespace NoRecoilApp.Maui.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WeaponProfile> WeaponProfiles => Set<WeaponProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WeaponProfile>();

        entity.ToTable("weapon_profiles");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.OperatorName, x.WeaponName }).IsUnique();

        entity.Property(x => x.OperatorName).IsRequired();
        entity.Property(x => x.WeaponName).IsRequired();
        entity.Property(x => x.Side).HasDefaultValue("ATK");
        entity.Property(x => x.StrengthY).HasDefaultValue(4.5);
        entity.Property(x => x.StrengthX).HasDefaultValue(0.0);
        entity.Property(x => x.Delay).HasDefaultValue(0.012);
        entity.Property(x => x.Smooth).HasDefaultValue(5);
        entity.Property(x => x.AccelFactor).HasDefaultValue(1.2);
        entity.Property(x => x.MaxProgression).HasDefaultValue(2.5);
        entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
