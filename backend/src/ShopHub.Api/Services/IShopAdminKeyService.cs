using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

/// <summary>
/// Reads the admin API key shophub-shop-operator generates and stores per shop — the same
/// static key shophub-shop's AdminApiKeyFilter checks for catalog-management requests. This
/// service never creates or rotates it; provisioning that Secret is the operator's job, this
/// only reveals it to the shop's owner.
/// </summary>
public interface IShopAdminKeyService
{
    Task<string> GetAdminKeyAsync(ShopSite site, CancellationToken cancellationToken = default);
}
