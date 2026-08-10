using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

public class KubernetesShopProvisioningService(
    IKubernetes client,
    IOptions<KubernetesOptions> options,
    ILogger<KubernetesShopProvisioningService> logger) : IShopProvisioningService
{
    private const string Group = "apps.shophub.io";
    private const string Version = "v1";
    private const string ApiVersion = "apps.shophub.io/v1";

    private string Namespace => options.Value.Namespace;

    public async Task ProvisionAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        var name = site.K8sName;
        var created = new List<string>();

        try
        {
            await CreateAsync("shops", "Shop", name, new
            {
                name = site.Name,
                availability = site.Availability,
                walletAddress = site.WalletAddress,
                databaseKind = site.DatabaseKind,
            }, cancellationToken);
            created.Add("shops");

            await CreateAsync("wallets", "Wallet", name, new
            {
                shopRef = name,
                address = site.WalletAddress,
            }, cancellationToken);
            created.Add("wallets");

            await CreateAsync("discordchannels", "DiscordChannel", name, new
            {
                shopRef = name,
                channelName = name,
            }, cancellationToken);
            created.Add("discordchannels");
        }
        catch
        {
            // Don't leave a partial set of custom resources behind if a later step in the
            // sequence fails — clean up whatever did get created before re-throwing.
            foreach (var plural in created)
            {
                await TryDeleteAsync(plural, name, cancellationToken);
            }

            throw;
        }
    }

    public async Task UpdateAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        var name = site.K8sName;

        // Merge patches: only the fields present here change, everything else (including
        // whatever the not-yet-implemented Shop controller has written to status) is left alone.
        await PatchAsync("shops", name, new { spec = new { availability = site.Availability, walletAddress = site.WalletAddress } }, cancellationToken);
        await PatchAsync("wallets", name, new { spec = new { address = site.WalletAddress } }, cancellationToken);
    }

    public async Task DeprovisionAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        var name = site.K8sName;

        // Best-effort, in dependency order roughly reversed from creation. A 404 here means
        // it's already gone (or was never fully created) — nothing left to clean up, not an
        // error. Anything else propagates, since silently losing track of a real resource is
        // worse than failing loudly.
        await TryDeleteAsync("discordchannels", name, cancellationToken);
        await TryDeleteAsync("wallets", name, cancellationToken);
        await TryDeleteAsync("shops", name, cancellationToken);
    }

    private async Task CreateAsync(string plural, string kind, string name, object spec, CancellationToken cancellationToken)
    {
        var body = new
        {
            apiVersion = ApiVersion,
            kind,
            metadata = new V1ObjectMeta { Name = name },
            spec,
        };

        await client.CustomObjects.CreateNamespacedCustomObjectAsync<object>(body, Group, Version, Namespace, plural, cancellationToken: cancellationToken);
    }

    private async Task PatchAsync(string plural, string name, object mergePatchBody, CancellationToken cancellationToken)
    {
        var patch = new V1Patch(mergePatchBody, V1Patch.PatchType.MergePatch);
        await client.CustomObjects.PatchNamespacedCustomObjectAsync<object>(
            patch, Group, Version, Namespace, plural, name,
            dryRun: null, fieldManager: null, fieldValidation: null, force: null, cancellationToken: cancellationToken);
    }

    private async Task TryDeleteAsync(string plural, string name, CancellationToken cancellationToken)
    {
        try
        {
            await client.CustomObjects.DeleteNamespacedCustomObjectAsync<object>(Group, Version, Namespace, plural, name, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — fine.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete {Plural}/{Name} in namespace {Namespace}", plural, name, Namespace);
            throw;
        }
    }
}
