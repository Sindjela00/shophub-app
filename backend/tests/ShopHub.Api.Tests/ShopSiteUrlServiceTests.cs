using System.Net;
using System.Text;
using k8s;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;
using ShopHub.Api.Services;
using Xunit;

namespace ShopHub.Api.Tests;

public class ShopSiteUrlServiceTests
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

    private static ShopSiteUrlService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respond, string externalHost = "localhost")
    {
        var handler = new StubHandler(respond);
        var config = new KubernetesClientConfiguration { Host = "https://fake-cluster.test" };
        var client = new Kubernetes(config, handler);
        var options = Options.Create(new KubernetesOptions { Namespace = "shops", InCluster = false, ExternalHost = externalHost });
        return new ShopSiteUrlService(client, options);
    }

    private static HttpResponseMessage ServiceResponse(string name, int? nodePort) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "apiVersion": "v1",
                  "kind": "Service",
                  "metadata": { "name": "{{name}}", "namespace": "shops" },
                  "spec": { "ports": [ { "name": "http", "port": 80{{(nodePort is { } p ? $", \"nodePort\": {p}" : "")}} } ] }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        };

    [Fact]
    public async Task GetSiteUrlAsync_returns_a_url_built_from_the_services_allocated_node_port()
    {
        var site = NewSite();
        var service = CreateService(req =>
        {
            Assert.Equal($"/api/v1/namespaces/shops/services/{site.K8sName}", req.RequestUri!.AbsolutePath);
            return ServiceResponse(site.K8sName, 31234);
        });

        var url = await service.GetSiteUrlAsync(site);

        Assert.Equal("http://localhost:31234/", url);
    }

    [Fact]
    public async Task GetSiteUrlAsync_throws_when_the_service_does_not_exist()
    {
        var site = NewSite();
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"services \"x\" not found"}""", Encoding.UTF8, "application/json"),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSiteUrlAsync(site));
    }

    [Fact]
    public async Task GetSiteUrlAsync_throws_when_the_service_has_no_nodeport_yet()
    {
        var site = NewSite();
        var service = CreateService(_ => ServiceResponse(site.K8sName, nodePort: null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSiteUrlAsync(site));
    }

    [Fact]
    public async Task GetSiteUrlAsync_throws_when_externalhost_is_not_configured()
    {
        var site = NewSite();
        var service = CreateService(
            _ => throw new InvalidOperationException("should not make an HTTP call"), externalHost: "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSiteUrlAsync(site));
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
