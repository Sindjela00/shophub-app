using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Swaps out real Kubernetes CR provisioning in integration tests — that's already covered by
/// KubernetesShopProvisioningService's own unit tests (against a faked transport) and by manual
/// end-to-end verification against a real cluster. Records which sites were (de)provisioned so
/// tests can assert the controller called it at the right time; ThrowOn* lets a test simulate a
/// cluster failure to verify the DB is never left with an orphaned/inconsistent row.
/// </summary>
public class FakeShopProvisioningService : IShopProvisioningService
{
    public List<Guid> Provisioned { get; } = [];
    public List<Guid> Updated { get; } = [];
    public List<Guid> Deprovisioned { get; } = [];

    public Exception? ThrowOnProvision { get; set; }
    public Exception? ThrowOnUpdate { get; set; }
    public Exception? ThrowOnDeprovision { get; set; }

    public Task ProvisionAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        if (ThrowOnProvision is { } ex)
        {
            throw ex;
        }

        Provisioned.Add(site.Id);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        if (ThrowOnUpdate is { } ex)
        {
            throw ex;
        }

        Updated.Add(site.Id);
        return Task.CompletedTask;
    }

    public Task DeprovisionAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        if (ThrowOnDeprovision is { } ex)
        {
            throw ex;
        }

        Deprovisioned.Add(site.Id);
        return Task.CompletedTask;
    }
}
