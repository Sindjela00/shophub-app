using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

public class ShopDiscordService(
    IKubernetes client,
    HttpClient discordClient,
    IOptions<KubernetesOptions> kubernetesOptions,
    IOptions<DiscordOptions> discordOptions) : IShopDiscordService
{
    private const string Group = "apps.shophub.io";
    private const string Version = "v1";
    private const string Plural = "discordchannels";

    // View Channel (1024) + Manage Channels (16) + Send Messages (2048) + Manage Webhooks
    // (536870912) — everything the operator's DiscordChannelReconciler actually does once it
    // joins a shop owner's server.
    private const long InvitePermissions = 536874000;

    private string Namespace => kubernetesOptions.Value.Namespace;
    private DiscordOptions Options => discordOptions.Value;

    public string BuildInviteUrl() =>
        $"https://discord.com/oauth2/authorize?client_id={Uri.EscapeDataString(Options.ClientId)}&scope=bot&permissions={InvitePermissions}";

    public async Task<bool> VerifyGuildMembershipAsync(string guildId, CancellationToken cancellationToken = default)
    {
        var botToken = await ReadBotTokenAsync(cancellationToken);

        // No leading slash: HttpClient.BaseAddress ("https://discord.com/api/v10/", trailing
        // slash) only keeps its path when the relative URI is *not* absolute-path-rooted — a
        // leading "/" here would discard "/api/v10" entirely per standard URI-combining rules.
        var request = new HttpRequestMessage(HttpMethod.Get, $"guilds/{Uri.EscapeDataString(guildId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        var response = await discordClient.SendAsync(request, cancellationToken);

        // Discord returns the guild for a member bot, 403/404 otherwise — anything else
        // (rate limit, outage) is a real failure, not "not a member", so it propagates.
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            return false;
        }
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Discord API call to check guild membership failed: {response.StatusCode} {body}");
        }
        return true;
    }

    public async Task AttachAsync(ShopSite site, string guildId, CancellationToken cancellationToken = default)
    {
        var name = site.K8sName;
        // Not sanitized client-side — shophub-shop-operator's DiscordChannelReconciler already
        // runs every ChannelName through discord.SanitizeChannelName before it ever reaches the
        // Discord API, same as the eagerly-created CR this replaces used to rely on.
        var channelName = $"{site.Name}-alerts";
        var spec = new { shopRef = name, channelName, guildId };

        var exists = true;
        try
        {
            await client.CustomObjects.GetNamespacedCustomObjectAsync<object>(Group, Version, Namespace, Plural, name, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            exists = false;
        }

        if (!exists)
        {
            var body = new { apiVersion = $"{Group}/{Version}", kind = "DiscordChannel", metadata = new V1ObjectMeta { Name = name }, spec };
            await client.CustomObjects.CreateNamespacedCustomObjectAsync<object>(body, Group, Version, Namespace, Plural, cancellationToken: cancellationToken);
            return;
        }

        var patch = new V1Patch(new { spec }, V1Patch.PatchType.MergePatch);
        await client.CustomObjects.PatchNamespacedCustomObjectAsync<object>(
            patch, Group, Version, Namespace, Plural, name,
            dryRun: null, fieldManager: null, fieldValidation: null, force: null, cancellationToken: cancellationToken);
    }

    public async Task<DiscordStatus> GetStatusAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        object raw;
        try
        {
            raw = await client.CustomObjects.GetNamespacedCustomObjectAsync<object>(Group, Version, Namespace, Plural, site.K8sName, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return new DiscordStatus(Attached: false, GuildId: null, Ready: false, Message: null);
        }

        var element = (JsonElement)raw;
        string? guildId = element.TryGetProperty("spec", out var spec) && spec.TryGetProperty("guildId", out var guildIdEl)
            ? guildIdEl.GetString()
            : null;

        var ready = false;
        string? message = null;
        if (element.TryGetProperty("status", out var status) && status.TryGetProperty("conditions", out var conditions))
        {
            foreach (var condition in conditions.EnumerateArray())
            {
                if (condition.TryGetProperty("type", out var type) && type.GetString() == "Ready")
                {
                    ready = condition.TryGetProperty("status", out var condStatus) && condStatus.GetString() == "True";
                    message = condition.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                    break;
                }
            }
        }

        return new DiscordStatus(Attached: true, GuildId: guildId, Ready: ready, Message: message);
    }

    private async Task<string> ReadBotTokenAsync(CancellationToken cancellationToken)
    {
        V1Secret secret;
        try
        {
            secret = await client.CoreV1.ReadNamespacedSecretAsync(Options.BotTokenSecretName, Namespace, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Discord bot token secret '{Options.BotTokenSecretName}' not found in namespace '{Namespace}'.");
        }

        if (secret.Data is null || !secret.Data.TryGetValue(Options.BotTokenSecretKey, out var bytes))
        {
            throw new InvalidOperationException(
                $"Discord bot token secret '{Options.BotTokenSecretName}' is missing the '{Options.BotTokenSecretKey}' key.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
