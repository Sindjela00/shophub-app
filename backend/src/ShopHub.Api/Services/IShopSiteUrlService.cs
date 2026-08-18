using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

/// <summary>
/// Builds the directly-reachable URL for a shop's storefront — the NodePort Service
/// shophub-shop-operator provisions (shop_controller.go's reconcileService), read back live
/// rather than cached: the allocated port isn't known until the operator creates the Service,
/// and this app tracks no copy of it itself.
/// </summary>
public interface IShopSiteUrlService
{
    Task<string> GetSiteUrlAsync(ShopSite site, CancellationToken cancellationToken = default);
}
