namespace ShopHub.Api.Models;

/// <summary>
/// A tenant's Shop site as ShopHub tracks it. Availability/DatabaseKind are plain strings
/// ("standard"/"high", "standard"/"light") rather than C# enums so they match the
/// shop-operator CRD's field values exactly with no conversion in between — the same
/// values get validated here and written straight into the Shop custom resource.
/// </summary>
public class ShopSite
{
    public Guid Id { get; set; }

    public required Guid UserId { get; set; }

    public required string Name { get; set; }

    public required string Availability { get; set; }

    public required string WalletAddress { get; set; }

    public required string DatabaseKind { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Stable, RFC 1123-valid Kubernetes object name for this site's Shop/Wallet/
    /// DiscordChannel custom resources — derived from Id rather than the user-supplied
    /// Name, which could contain spaces/invalid characters or collide across users.
    /// </summary>
    public string K8sName => $"shop-{Id:N}";
}
