using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

/// <summary>
/// Orchestrates the Shop/Wallet/DiscordChannel custom resources (defined by the
/// shop-operator repo) that back a ShopSite. Reconciling those CRs into an actually
/// running Shop app is the shop-operator's own job — this only ever creates/updates/
/// deletes the CRs themselves.
/// </summary>
public interface IShopProvisioningService
{
    Task ProvisionAsync(ShopSite site, CancellationToken cancellationToken = default);

    Task UpdateAsync(ShopSite site, CancellationToken cancellationToken = default);

    Task DeprovisionAsync(ShopSite site, CancellationToken cancellationToken = default);
}
