using NoRecoilApp.Maui.Models;

namespace NoRecoilApp.Maui.Data;

public interface IWeaponProfileRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeaponProfile>> SearchAsync(string query, string sideFilter, CancellationToken cancellationToken = default);

    Task<WeaponProfile?> FindByIdentityAsync(string operatorName, string weaponName, CancellationToken cancellationToken = default);

    Task SaveAsync(WeaponProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(string operatorName, string weaponName, CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}
