using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Swaps out real Discord/Kubernetes calls in integration tests — that's covered separately (see
/// ShopDiscordServiceTests for the real HTTP/Secret-reading behavior, and shophub-shop-operator's
/// own tests for what happens once a DiscordChannel CR is attached).
/// </summary>
public class FakeShopDiscordService : IShopDiscordService
{
    public List<Guid> AttachRequested { get; } = [];
    public List<string> GuildIdsVerified { get; } = [];

    public bool VerifyResult { get; set; } = true;
    public DiscordStatus StatusResult { get; set; } = new(Attached: false, GuildId: null, Ready: false, Message: null);

    public Exception? ThrowOnGetStatus { get; set; }
    public Exception? ThrowOnAttach { get; set; }

    public string BuildInviteUrl() => "https://discord.com/oauth2/authorize?client_id=fake&scope=bot";

    public Task<bool> VerifyGuildMembershipAsync(string guildId, CancellationToken cancellationToken = default)
    {
        GuildIdsVerified.Add(guildId);
        return Task.FromResult(VerifyResult);
    }

    public Task AttachAsync(ShopSite site, string guildId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAttach is { } ex)
        {
            throw ex;
        }

        AttachRequested.Add(site.Id);
        return Task.CompletedTask;
    }

    public Task<DiscordStatus> GetStatusAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetStatus is { } ex)
        {
            throw ex;
        }

        return Task.FromResult(StatusResult);
    }
}
