using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Swaps out real Kubernetes Secret reads in integration tests — real cluster behavior is
/// covered separately (see shophub-shop-operator's own tests for provisioning the Secret, and
/// manual end-to-end verification against a real cluster for this service reading it back).
/// </summary>
public class FakeShopAdminKeyService : IShopAdminKeyService
{
    public List<Guid> Requested { get; } = [];
    public Exception? ThrowOnGetAdminKey { get; set; }

    public Task<string> GetAdminKeyAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetAdminKey is { } ex)
        {
            throw ex;
        }

        Requested.Add(site.Id);
        return Task.FromResult($"fake-admin-key-{site.K8sName}");
    }
}
