namespace ShopHub.Api.Contracts;

public record CreateShopSiteRequest(string Name, string Availability, string WalletAddress, string DatabaseKind);

public record UpdateShopSiteRequest(string Availability, string WalletAddress);

public record DashboardLinkDto(string Path);

public record AdminKeyDto(string Key);

public record SiteUrlDto(string Url);

public record DiscordInviteDto(string InviteUrl);

public record DiscordStatusDto(bool Attached, string? GuildId, bool Ready, string? Message)
{
    public static DiscordStatusDto FromStatus(Services.DiscordStatus status) =>
        new(status.Attached, status.GuildId, status.Ready, status.Message);
}

public record AttachDiscordRequest(string GuildId);

public record ShopSiteDto(Guid Id, string Name, string Availability, string WalletAddress, string DatabaseKind, DateTimeOffset CreatedAt)
{
    public static ShopSiteDto FromEntity(Models.ShopSite site) =>
        new(site.Id, site.Name, site.Availability, site.WalletAddress, site.DatabaseKind, site.CreatedAt);
}
