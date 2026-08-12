using System.Net;
using System.Text;
using System.Text.Json;
using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.Tests;

public class KubernetesShopProvisioningServiceTests
{
    private static ShopSite NewSite() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "Test Shop",
        Availability = "standard",
        WalletAddress = "0x1234567890abcdef1234567890abcdef12345678",
        DatabaseKind = "standard",
    };

    private static (KubernetesShopProvisioningService Service, RecordingHandler Handler) CreateService(
        Func<RecordedRequest, HttpResponseMessage>? respond = null)
    {
        var handler = new RecordingHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{}") }));
        var config = new KubernetesClientConfiguration { Host = "https://fake-cluster.test" };
        var client = new Kubernetes(config, handler);
        var options = Options.Create(new KubernetesOptions { Namespace = "shops", InCluster = false });
        var service = new KubernetesShopProvisioningService(client, options, NullLogger<KubernetesShopProvisioningService>.Instance);
        return (service, handler);
    }

    private static StringContent RawJsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task ProvisionAsync_creates_shop_wallet_and_discordchannel_with_correct_spec()
    {
        var site = NewSite();
        var (service, handler) = CreateService();

        await service.ProvisionAsync(site);

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));

        var shopReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("/namespaces/shops/shops"));
        var shopSpec = shopReq.BodyJson!.RootElement.GetProperty("spec");
        Assert.Equal(site.Name, shopSpec.GetProperty("name").GetString());
        Assert.Equal(site.Availability, shopSpec.GetProperty("availability").GetString());
        Assert.Equal(site.WalletAddress, shopSpec.GetProperty("walletAddress").GetString());
        Assert.Equal(site.DatabaseKind, shopSpec.GetProperty("databaseKind").GetString());
        Assert.Equal(site.K8sName, shopReq.BodyJson.RootElement.GetProperty("metadata").GetProperty("name").GetString());

        var walletReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("/namespaces/shops/wallets"));
        var walletSpec = walletReq.BodyJson!.RootElement.GetProperty("spec");
        Assert.Equal(site.K8sName, walletSpec.GetProperty("shopRef").GetString());
        Assert.Equal(site.WalletAddress, walletSpec.GetProperty("address").GetString());

        var discordReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("/namespaces/shops/discordchannels"));
        var discordSpec = discordReq.BodyJson!.RootElement.GetProperty("spec");
        Assert.Equal(site.K8sName, discordSpec.GetProperty("shopRef").GetString());
    }

    [Fact]
    public async Task ProvisionAsync_rolls_back_already_created_resources_when_a_later_create_fails()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req =>
            req.Method == HttpMethod.Post && req.Uri.AbsolutePath.EndsWith("/wallets")
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = RawJsonContent("{\"message\":\"boom\"}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{}") });

        await Assert.ThrowsAsync<HttpOperationException>(() => service.ProvisionAsync(site));

        // Shop was created before wallet failed — it must be rolled back. DiscordChannel was
        // never reached, so there should be no create *or* delete for it.
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath.EndsWith("/shops/shops"));
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath.EndsWith("/wallets"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath.EndsWith("/discordchannels"));

        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Delete && r.Uri.AbsolutePath.EndsWith($"/shops/shops/{site.K8sName}"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Delete && r.Uri.AbsolutePath.Contains("/wallets/"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Delete && r.Uri.AbsolutePath.Contains("/discordchannels/"));
    }

    [Fact]
    public async Task UpdateAsync_merge_patches_only_shop_and_wallet_with_changed_fields()
    {
        var site = NewSite();
        var (service, handler) = CreateService();

        await service.UpdateAsync(site);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(new HttpMethod("PATCH"), r.Method));
        Assert.All(handler.Requests, r => Assert.Equal("application/merge-patch+json", r.ContentType));

        var shopReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith($"/shops/shops/{site.K8sName}"));
        var shopSpec = shopReq.BodyJson!.RootElement.GetProperty("spec");
        Assert.Equal(site.Availability, shopSpec.GetProperty("availability").GetString());
        Assert.Equal(site.WalletAddress, shopSpec.GetProperty("walletAddress").GetString());
        Assert.False(shopSpec.TryGetProperty("name", out _));
        Assert.False(shopSpec.TryGetProperty("databaseKind", out _));

        var walletReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith($"/wallets/{site.K8sName}"));
        var walletSpec = walletReq.BodyJson!.RootElement.GetProperty("spec");
        Assert.Equal(site.WalletAddress, walletSpec.GetProperty("address").GetString());
        Assert.False(walletSpec.TryGetProperty("shopRef", out _));
    }

    [Fact]
    public async Task DeprovisionAsync_deletes_discordchannel_wallet_and_shop_in_that_order()
    {
        var site = NewSite();
        var (service, handler) = CreateService();

        await service.DeprovisionAsync(site);

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Delete, r.Method));
        Assert.EndsWith($"/discordchannels/{site.K8sName}", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith($"/wallets/{site.K8sName}", handler.Requests[1].Uri.AbsolutePath);
        Assert.EndsWith($"/shops/shops/{site.K8sName}", handler.Requests[2].Uri.AbsolutePath);
    }

    [Fact]
    public async Task DeprovisionAsync_treats_404_as_already_gone_and_continues()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req =>
            req.Uri.AbsolutePath.Contains("/discordchannels/")
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = RawJsonContent("{\"reason\":\"NotFound\"}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{}") });

        await service.DeprovisionAsync(site);

        // All three deletes were still attempted despite the first 404.
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task DeprovisionAsync_propagates_a_non_404_delete_failure()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req =>
            req.Uri.AbsolutePath.Contains("/wallets/")
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = RawJsonContent("{\"message\":\"boom\"}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{}") });

        await Assert.ThrowsAsync<HttpOperationException>(() => service.DeprovisionAsync(site));

        // discordchannels (first) succeeded, wallets (second) failed and aborted the rest —
        // shops (third) was never attempted.
        Assert.Equal(2, handler.Requests.Count);
    }

    internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? ContentType, JsonDocument? BodyJson);

    internal sealed class RecordingHandler(Func<RecordedRequest, HttpResponseMessage> respond) : DelegatingHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Content?.Headers.ContentType?.MediaType,
                string.IsNullOrEmpty(body) ? null : JsonDocument.Parse(body));
            Requests.Add(recorded);

            var response = respond(recorded);
            response.RequestMessage = request;
            return response;
        }
    }
}
