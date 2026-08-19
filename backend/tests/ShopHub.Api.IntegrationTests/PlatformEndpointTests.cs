using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShopHub.Api.Contracts;
using Xunit;

namespace ShopHub.Api.IntegrationTests;

[Collection(ShopHubApiCollection.Name)]
public class PlatformEndpointTests(ShopHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@example.com", "CorrectHorseBattery1"),
            TestJson.Options);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task GetDashboardLink_returns_the_path_the_grafana_service_builds_for_a_user_with_no_shops()
    {
        var token = await RegisterAndGetTokenAsync();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform/dashboard-link", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<DashboardLinkDto>(TestJson.Options);
        Assert.Equal("/grafana-proxy/d/shophub-platform?orgId=2", link!.Path);
        Assert.Single(factory.GrafanaProvisioning.PlatformDashboardRequestedFor);
    }

    [Fact]
    public async Task GetDashboardLink_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/platform/dashboard-link");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboardLink_returns_502_when_grafana_provisioning_fails()
    {
        var token = await RegisterAndGetTokenAsync();

        factory.GrafanaProvisioning.ThrowOnGetPlatformDashboardPath = new InvalidOperationException("simulated Grafana failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/platform/dashboard-link", token));
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.GrafanaProvisioning.ThrowOnGetPlatformDashboardPath = null;
        }
    }
}
