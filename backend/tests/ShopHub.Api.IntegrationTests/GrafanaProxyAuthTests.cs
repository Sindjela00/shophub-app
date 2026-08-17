using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ShopHub.Api.Contracts;
using Xunit;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Covers just the /grafana-proxy authentication mechanism (the access_token query-param
/// escape hatch and its cookie fallback) — not the actual proxying, which needs a real Grafana
/// backend and is covered by manual end-to-end verification instead. A real Grafana being
/// unreachable from the test host is fine here: what matters is whether the request gets past
/// authentication, not whether the proxied response is a real Grafana page.
/// </summary>
[Collection(ShopHubApiCollection.Name)]
public class GrafanaProxyAuthTests(ShopHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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

    [Fact]
    public async Task Request_with_no_token_and_no_cookie_is_rejected()
    {
        var response = await _client.GetAsync("/grafana-proxy/some/path");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_access_token_query_param_authenticates_and_sets_a_cookie()
    {
        var token = await RegisterAndGetTokenAsync();

        var response = await _client.GetAsync($"/grafana-proxy/some/path?access_token={Uri.EscapeDataString(token)}");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookie), "expected a Set-Cookie header");
        Assert.Contains(setCookie!, c => c.StartsWith("grafana_proxy_token=") && c.Contains("path=/grafana-proxy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Follow_up_request_with_only_the_cookie_also_authenticates()
    {
        // Simulates exactly what a browser does after the first navigation: the page's own
        // relative asset URLs get requested with no query string, only whatever cookies are
        // scoped to that path — this is the case the query-param-only mechanism used to miss.
        var token = await RegisterAndGetTokenAsync();
        var first = await _client.GetAsync($"/grafana-proxy/some/path?access_token={Uri.EscapeDataString(token)}");
        var setCookie = first.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("grafana_proxy_token="));
        var cookiePair = setCookie.Split(';')[0];

        var followUp = new HttpRequestMessage(HttpMethod.Get, "/grafana-proxy/public/build/some-asset.css");
        followUp.Headers.Add("Cookie", cookiePair);
        var response = await _client.SendAsync(followUp);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
