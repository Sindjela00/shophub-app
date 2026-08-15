namespace ShopHub.Api.Contracts;

public record CreateShopSiteRequest(string Name, string Availability, string WalletAddress, string DatabaseKind);

public record UpdateShopSiteRequest(string Availability, string WalletAddress);

public record DashboardLinkDto(string Path);

public record ShopSiteDto(Guid Id, string Name, string Availability, string WalletAddress, string DatabaseKind, DateTimeOffset CreatedAt)
{
    public static ShopSiteDto FromEntity(Models.ShopSite site) =>
        new(site.Id, site.Name, site.Availability, site.WalletAddress, site.DatabaseKind, site.CreatedAt);
}
