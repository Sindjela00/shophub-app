using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Swaps out real Grafana provisioning in integration tests — that's already covered by manual
/// end-to-end verification against a real Grafana instance (see the Grafana access-control PR).
/// Records which sites were (de)provisioned and for which owner, so tests can assert the
/// controller called it at the right time without ever making a real HTTP call.
/// </summary>
public class FakeGrafanaProvisioningService : IGrafanaProvisioningService
{
    public List<(Guid SiteId, string OwnerEmail)> Provisioned { get; } = [];
    public List<Guid> Deprovisioned { get; } = [];

    public Exception? ThrowOnProvision { get; set; }
    public Exception? ThrowOnGetDashboardPath { get; set; }
    public Exception? ThrowOnDeprovision { get; set; }

    public Task ProvisionAsync(ShopSite site, string ownerEmail, CancellationToken cancellationToken = default)
    {
        if (ThrowOnProvision is { } ex)
        {
            throw ex;
        }

        Provisioned.Add((site.Id, ownerEmail));
        return Task.CompletedTask;
    }

    public Task<string> GetDashboardPathAsync(ShopSite site, string ownerEmail, CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetDashboardPath is { } ex)
        {
            throw ex;
        }

        return Task.FromResult($"/grafana-proxy/d/{site.K8sName}?orgId=2");
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
