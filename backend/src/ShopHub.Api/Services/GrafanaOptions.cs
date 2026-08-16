namespace ShopHub.Api.Services;

public class GrafanaOptions
{
    public const string SectionName = "Grafana";

    /// <summary>Cluster-internal URL only — Grafana has no public Ingress. This service account
    /// is the sole path in, alongside `kubectl port-forward` for maintainers.</summary>
    public required string BaseUrl { get; set; }

    /// <summary>Grafana global-admin credentials, used only for org/global-user management
    /// (POST /api/admin/users, POST /api/orgs/:orgId/users) — the only Grafana endpoints that
    /// take an org id in the URL/body rather than operating on "whichever org this credential
    /// is currently scoped to".</summary>
    public required string AdminUser { get; set; }

    public required string AdminPassword { get; set; }

    /// <summary>Bearer token for a service account that lives *inside* <see cref="UsersOrgName"/>.
    /// Folder/dashboard/permission endpoints operate on "the org this credential belongs to",
    /// not an org id you pass per-request — so provisioning as this org-scoped service account
    /// (rather than switching the global admin's current org per-request) is what keeps
    /// concurrent shop creations from racing each other onto the wrong org.</summary>
    public required string UsersOrgServiceAccountToken { get; set; }

    /// <summary>Org that holds every ShopHub end user and their shop folders — kept separate
    /// from the maintainers' default org so a ShopHub user's Viewer role can never see
    /// cluster/maintainer dashboards, only the per-shop folders explicitly shared with them.</summary>
    public string UsersOrgName { get; set; } = "ShopHub Users";
}
