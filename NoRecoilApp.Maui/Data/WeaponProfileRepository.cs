using Microsoft.EntityFrameworkCore;
using NoRecoilApp.Maui.Models;

namespace NoRecoilApp.Maui.Data;

public sealed class WeaponProfileRepository(
    IDbContextFactory<AppDbContext> dbFactory) : IWeaponProfileRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeaponProfile>> SearchAsync(
        string query,
        string sideFilter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var q = db.WeaponProfiles
                  .Include(w => w.SprayPoints)
                  .AsNoTracking()
                  .AsQueryable();

        if (!string.IsNullOrWhiteSpace(sideFilter) && sideFilter != "TODOS")
            q = q.Where(w => w.Side == sideFilter);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(w =>
                w.OperatorName.Contains(query) ||
                w.WeaponName.Contains(query));

        var result = await q
            .OrderBy(w => w.OperatorName)
            .ThenBy(w => w.WeaponName)
            .ToListAsync(cancellationToken);

        foreach (var profile in result)
            profile.SprayPoints = [.. profile.SprayPoints.OrderBy(p => p.Index)];

        return result;
    }

    public async Task<WeaponProfile?> FindByIdentityAsync(
        string operatorName,
        string weaponName,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var profile = await db.WeaponProfiles
            .Include(w => w.SprayPoints)
            .Where(w => w.OperatorName == operatorName &&
                        w.WeaponName == weaponName)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is not null)
            profile.SprayPoints = [.. profile.SprayPoints.OrderBy(p => p.Index)];

        return profile;
    }

    public async Task SaveAsync(
        WeaponProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        profile.UpdatedAtUtc = DateTime.UtcNow;

        var existing = await db.WeaponProfiles
            .Include(w => w.SprayPoints)
            .Where(w => w.OperatorName == profile.OperatorName &&
                        w.WeaponName == profile.WeaponName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            NormalizeSprayIndexes(profile.SprayPoints);
            await db.WeaponProfiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            existing.Side = profile.Side;
            existing.StrengthY = profile.StrengthY;
            existing.StrengthX = profile.StrengthX;
            existing.Delay = profile.Delay;
            existing.Smooth = profile.Smooth;
            existing.AccelFactor = profile.AccelFactor;
            existing.MaxProgression = profile.MaxProgression;
            existing.UseSprayPattern = profile.UseSprayPattern;
            existing.FireRateRpm = profile.FireRateRpm;
            existing.UpdatedAtUtc = profile.UpdatedAtUtc;

            db.SprayPoints.RemoveRange(existing.SprayPoints);

            NormalizeSprayIndexes(profile.SprayPoints);

            foreach (var point in profile.SprayPoints)
            {
                point.Id = 0;
                point.WeaponProfileId = existing.Id;
            }

            await db.SprayPoints.AddRangeAsync(profile.SprayPoints, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string operatorName,
        string weaponName,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.WeaponProfiles
            .Where(w => w.OperatorName == operatorName &&
                        w.WeaponName == weaponName)
            .ExecuteDeleteAsync(cancellationToken);

        if (rows == 0)
            throw new KeyNotFoundException(
                $"Perfil '{operatorName} | {weaponName}' não encontrado.");
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.SprayPoints.ExecuteDeleteAsync(cancellationToken);
        await db.WeaponProfiles.ExecuteDeleteAsync(cancellationToken);
    }

    private static void NormalizeSprayIndexes(IList<SprayPoint> points)
    {
        for (var i = 0; i < points.Count; i++)
            points[i].Index = i;
    }
}
