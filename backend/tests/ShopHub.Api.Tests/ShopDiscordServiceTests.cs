using System.Net;
using System.Text;
using k8s;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;
using ShopHub.Api.Services;
using Xunit;

namespace ShopHub.Api.Tests;

public class ShopDiscordServiceTests
{
    private const string SecretName = "shop-operator-discord";
    private const string SecretKey = "DISCORD_BOT_TOKEN";

    private static ShopSite NewSite() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "Aurora Shop",
        Availability = "standard",
        WalletAddress = "0x1234567890abcdef1234567890abcdef12345678",
        DatabaseKind = "standard",
    };

    private static ShopDiscordService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respondK8s,
        Func<HttpRequestMessage, HttpResponseMessage>? respondDiscord = null)
    {
        var k8sConfig = new KubernetesClientConfiguration { Host = "https://fake-cluster.test" };
        var k8sClient = new Kubernetes(k8sConfig, new StubHandler(respondK8s));

        var discordHandler = new StubHandler(respondDiscord ?? (_ => throw new InvalidOperationException("no Discord call expected")));
        var discordClient = new HttpClient(discordHandler) { BaseAddress = new Uri("https://discord.com/api/v10/") };

        var k8sOptions = Options.Create(new KubernetesOptions { Namespace = "shops", InCluster = false });
        var discordOptions = Options.Create(new DiscordOptions { ClientId = "test-client-id" });
        return new ShopDiscordService(k8sClient, discordClient, k8sOptions, discordOptions);
    }

    private static HttpResponseMessage SecretResponse(string token) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "apiVersion": "v1",
                  "kind": "Secret",
                  "metadata": { "name": "{{SecretName}}", "namespace": "shops" },
                  "data": { "{{SecretKey}}": "{{Convert.ToBase64String(Encoding.UTF8.GetBytes(token))}}" }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public void BuildInviteUrl_includes_the_configured_client_id()
    {
        var service = CreateService(_ => throw new InvalidOperationException("no K8s call expected"));

        var url = service.BuildInviteUrl();

        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains("scope=bot", url);
    }

    [Fact]
    public async Task VerifyGuildMembershipAsync_returns_true_when_the_bot_is_a_member()
    {
        var service = CreateService(
            respondK8s: req =>
            {
                Assert.Equal($"/api/v1/namespaces/shops/secrets/{SecretName}", req.RequestUri!.AbsolutePath);
                return SecretResponse("bot-token-123");
            },
            respondDiscord: req =>
            {
                Assert.Equal("/api/v10/guilds/999", req.RequestUri!.AbsolutePath);
                Assert.Equal("Bot bot-token-123", req.Headers.Authorization!.ToString());
                return JsonResponse(HttpStatusCode.OK, """{"id":"999"}""");
            });

        var isMember = await service.VerifyGuildMembershipAsync("999");

        Assert.True(isMember);
    }

    [Fact]
    public async Task VerifyGuildMembershipAsync_returns_false_when_the_bot_has_not_joined()
    {
        var service = CreateService(
            respondK8s: _ => SecretResponse("bot-token-123"),
            respondDiscord: _ => JsonResponse(HttpStatusCode.Forbidden, """{"message":"Missing Access"}"""));

        var isMember = await service.VerifyGuildMembershipAsync("999");

        Assert.False(isMember);
    }

    [Fact]
    public async Task VerifyGuildMembershipAsync_throws_when_the_bot_token_secret_does_not_exist()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"secrets \"x\" not found"}""", Encoding.UTF8, "application/json"),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyGuildMembershipAsync("999"));
    }

    [Fact]
    public async Task AttachAsync_creates_the_cr_when_it_does_not_exist()
    {
        var site = NewSite();
        var created = false;

        var service = CreateService(req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                Assert.Equal($"/apis/apps.shophub.io/v1/namespaces/shops/discordchannels/{site.K8sName}", req.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"message":"not found"}""", Encoding.UTF8, "application/json"),
                };
            }

            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/apis/apps.shophub.io/v1/namespaces/shops/discordchannels", req.RequestUri!.AbsolutePath);
            created = true;
            return JsonResponse(HttpStatusCode.Created, """{"apiVersion":"apps.shophub.io/v1","kind":"DiscordChannel"}""");
        });

        await service.AttachAsync(site, "999");

        Assert.True(created);
    }

    [Fact]
    public async Task AttachAsync_patches_the_cr_when_it_already_exists()
    {
        var site = NewSite();
        var patched = false;

        var service = CreateService(req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                return JsonResponse(HttpStatusCode.OK, """{"apiVersion":"apps.shophub.io/v1","kind":"DiscordChannel","spec":{"guildId":"old"}}""");
            }

            Assert.Equal(new HttpMethod("PATCH"), req.Method);
            Assert.Equal($"/apis/apps.shophub.io/v1/namespaces/shops/discordchannels/{site.K8sName}", req.RequestUri!.AbsolutePath);
            patched = true;
            return JsonResponse(HttpStatusCode.OK, """{"apiVersion":"apps.shophub.io/v1","kind":"DiscordChannel"}""");
        });

        await service.AttachAsync(site, "999");

        Assert.True(patched);
    }

    [Fact]
    public async Task GetStatusAsync_returns_not_attached_when_the_cr_does_not_exist()
    {
        var site = NewSite();
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"not found"}""", Encoding.UTF8, "application/json"),
        });

        var status = await service.GetStatusAsync(site);

        Assert.False(status.Attached);
        Assert.Null(status.GuildId);
        Assert.False(status.Ready);
    }

    [Fact]
    public async Task GetStatusAsync_returns_the_guild_and_ready_state_from_the_cr()
    {
        var site = NewSite();
        var service = CreateService(_ => JsonResponse(HttpStatusCode.OK, """
            {
              "apiVersion": "apps.shophub.io/v1",
              "kind": "DiscordChannel",
              "spec": { "shopRef": "shop-1", "channelName": "aurora-shop-alerts", "guildId": "999" },
              "status": {
                "channelId": "1",
                "conditions": [
                  { "type": "Ready", "status": "True", "message": "Discord channel provisioned.", "reason": "ChannelProvisioned", "lastTransitionTime": "2024-01-01T00:00:00Z" }
                ]
              }
            }
            """));

        var status = await service.GetStatusAsync(site);

        Assert.True(status.Attached);
        Assert.Equal("999", status.GuildId);
        Assert.True(status.Ready);
        Assert.Equal("Discord channel provisioned.", status.Message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
