using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

/// <summary>
/// Provisions the Grafana side of "let a ShopHub user reach only their own Shop dashboard":
/// a per-shop folder + dashboard inside the dedicated <see cref="GrafanaOptions.UsersOrgName"/>
/// org, with folder permissions scoped to just the owning user. Reconciling metrics into that
/// dashboard is Prometheus/kube-prometheus-stack's job — this only manages the Grafana-side
/// objects that grant and shape access to them.
/// </summary>
public interface IGrafanaProvisioningService
{
    Task ProvisionAsync(ShopSite site, string ownerEmail, CancellationToken cancellationToken = default);

    /// <summary>Switches the owner's Grafana account to the dedicated org (see remarks on the
    /// implementation) and returns the relative path (through the /grafana-proxy route) to their
    /// shop's dashboard.</summary>
    Task<string> GetDashboardPathAsync(ShopSite site, string ownerEmail, CancellationToken cancellationToken = default);

    Task DeprovisionAsync(ShopSite site, CancellationToken cancellationToken = default);

    /// <summary>Same idea as <see cref="GetDashboardPathAsync"/>, but for the single shared
    /// platform dashboard rather than a per-shop one: provisions it (idempotently) if this is
    /// the first time anyone's asked for it, ensures the given user has org membership (even if
    /// they've never created a shop), switches their active org, and returns the path. Every
    /// org-2 Viewer can see this dashboard — there's no per-user folder ACL the way
    /// <see cref="ProvisionAsync"/> sets up for a shop's own dashboard.</summary>
    Task<string> GetPlatformDashboardPathAsync(string ownerEmail, CancellationToken cancellationToken = default);
}
