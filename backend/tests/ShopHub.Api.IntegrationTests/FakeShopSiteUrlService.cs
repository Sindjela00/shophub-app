using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Swaps out real Kubernetes Service reads in integration tests — real cluster behavior is
/// covered separately (see shophub-shop-operator's own tests for NodePort allocation, and
/// manual end-to-end verification against a real cluster for this service reading it back).
/// </summary>
public class FakeShopSiteUrlService : IShopSiteUrlService
{
    public List<Guid> Requested { get; } = [];
    public Exception? ThrowOnGetSiteUrl { get; set; }

    public Task<string> GetSiteUrlAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetSiteUrl is { } ex)
        {
            throw ex;
        }

        Requested.Add(site.Id);
        return Task.FromResult($"http://fake-host:30000/{site.K8sName}");
    }
}
