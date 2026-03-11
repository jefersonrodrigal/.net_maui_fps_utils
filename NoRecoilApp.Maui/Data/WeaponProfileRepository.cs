using Microsoft.EntityFrameworkCore;
using NoRecoilApp.Maui.Models;

namespace NoRecoilApp.Maui.Data;

public sealed class WeaponProfileRepository(IDbContextFactory<AppDbContext> contextFactory) : IWeaponProfileRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeaponProfile>> SearchAsync(string query, string sideFilter, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedQuery = query.Trim();
        var normalizedSide = sideFilter.Trim().ToUpperInvariant();

        IQueryable<WeaponProfile> sql = db.WeaponProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var pattern = $"%{normalizedQuery}%";
            sql = sql.Where(x =>
                EF.Functions.Like(x.OperatorName, pattern) ||
                EF.Functions.Like(x.WeaponName, pattern));
        }

        if (normalizedSide is "ATK" or "DEF")
        {
            sql = sql.Where(x => x.Side == normalizedSide);
        }

        return await sql
            .OrderBy(x => x.OperatorName)
            .ThenBy(x => x.WeaponName)
            .ToListAsync(cancellationToken);
    }

    public async Task<WeaponProfile?> FindByIdentityAsync(string operatorName, string weaponName, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.WeaponProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.OperatorName == operatorName && x.WeaponName == weaponName,
                cancellationToken);
    }

    public async Task SaveAsync(WeaponProfile profile, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var op = profile.OperatorName.Trim();
        var wp = profile.WeaponName.Trim();

        var existing = await db.WeaponProfiles
            .FirstOrDefaultAsync(x => x.OperatorName == op && x.WeaponName == wp, cancellationToken);

        if (existing is null)
        {
            profile.OperatorName = op;
            profile.WeaponName = wp;
            profile.Side = profile.Side.ToUpperInvariant();
            profile.UpdatedAtUtc = DateTime.UtcNow;
            await db.WeaponProfiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            existing.Side = profile.Side.ToUpperInvariant();
            existing.StrengthY = profile.StrengthY;
            existing.StrengthX = profile.StrengthX;
            existing.Delay = profile.Delay;
            existing.Smooth = profile.Smooth;
            existing.AccelFactor = profile.AccelFactor;
            existing.MaxProgression = profile.MaxProgression;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string operatorName, string weaponName, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.WeaponProfiles
            .FirstOrDefaultAsync(x => x.OperatorName == operatorName && x.WeaponName == weaponName, cancellationToken);

        if (existing is null)
        {
            return;
        }

        db.WeaponProfiles.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.WeaponProfiles.RemoveRange(db.WeaponProfiles);
        await db.SaveChangesAsync(cancellationToken);
    }
}
