using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

/// <summary>Onboards a shop's alert channel onto the owner's own Discord server — invite the
/// shared bot, paste the Guild ID, verify it joined, then attach (create/update the
/// <c>DiscordChannel</c> CR shophub-shop-operator's DiscordChannelReconciler owns from there).</summary>
public interface IShopDiscordService
{
    string BuildInviteUrl();

    /// <summary>True if the shared bot is currently a member of the given guild — checked live
    /// against Discord's API before ever attaching a shop to it.</summary>
    Task<bool> VerifyGuildMembershipAsync(string guildId, CancellationToken cancellationToken = default);

    Task AttachAsync(ShopSite site, string guildId, CancellationToken cancellationToken = default);

    Task<DiscordStatus> GetStatusAsync(ShopSite site, CancellationToken cancellationToken = default);
}

public record DiscordStatus(bool Attached, string? GuildId, bool Ready, string? Message);
