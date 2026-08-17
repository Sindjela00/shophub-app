using System.Net;
using System.Text;
using k8s;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;
using ShopHub.Api.Services;
using Xunit;

namespace ShopHub.Api.Tests;

public class ShopAdminKeyServiceTests
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

    private static ShopAdminKeyService CreateService(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var config = new KubernetesClientConfiguration { Host = "https://fake-cluster.test" };
        var client = new Kubernetes(config, handler);
        var options = Options.Create(new KubernetesOptions { Namespace = "shops", InCluster = false });
        return new ShopAdminKeyService(client, options);
    }

    private static HttpResponseMessage SecretResponse(string secretName, string key, string value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "apiVersion": "v1",
                  "kind": "Secret",
                  "metadata": { "name": "{{secretName}}", "namespace": "shops" },
                  "data": { "{{key}}": "{{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}}" }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };

    [Fact]
    public async Task GetAdminKeyAsync_returns_the_decoded_key_from_the_secret()
    {
        var site = NewSite();
        var service = CreateService(req =>
        {
            Assert.Equal($"/api/v1/namespaces/shops/secrets/{site.K8sName}-admin-key", req.RequestUri!.AbsolutePath);
            return SecretResponse($"{site.K8sName}-admin-key", "apiKey", "sekrit-value-123");
        });

        var key = await service.GetAdminKeyAsync(site);

        Assert.Equal("sekrit-value-123", key);
    }

    [Fact]
    public async Task GetAdminKeyAsync_throws_when_the_secret_does_not_exist()
    {
        var site = NewSite();
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"secrets \"x\" not found"}""", Encoding.UTF8, "application/json"),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAdminKeyAsync(site));
    }

    [Fact]
    public async Task GetAdminKeyAsync_throws_when_the_secret_is_missing_the_expected_key()
    {
        var site = NewSite();
        var service = CreateService(_ => SecretResponse($"{site.K8sName}-admin-key", "someOtherKey", "value"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAdminKeyAsync(site));
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
