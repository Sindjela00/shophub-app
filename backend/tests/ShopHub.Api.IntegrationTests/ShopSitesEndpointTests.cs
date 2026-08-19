using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShopHub.Api.Contracts;
using ShopHub.Api.Services;
using Xunit;

namespace ShopHub.Api.IntegrationTests;

[Collection(ShopHubApiCollection.Name)]
public class ShopSitesEndpointTests(ShopHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateShopSiteRequest NewCreateRequest(string? name = null) => new(
        name ?? $"Shop {Guid.NewGuid():N}",
        Availability: "standard",
        WalletAddress: "0x1234567890abcdef1234567890abcdef12345678",
        DatabaseKind: "standard");

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: TestJson.Options);
        }

        return request;
    }

    private async Task<ShopSiteDto> CreateShopSiteAsync(string token, CreateShopSiteRequest? request = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/shop-sites", token, request ?? NewCreateRequest()));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShopSiteDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task Create_with_valid_data_returns_201_and_provisions_the_custom_resources()
    {
        var token = await RegisterAndGetTokenAsync();
        var request = NewCreateRequest("Aurora Shop");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/shop-sites", token, request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var site = await response.Content.ReadFromJsonAsync<ShopSiteDto>(TestJson.Options);
        Assert.Equal("Aurora Shop", site!.Name);
        Assert.Equal("standard", site.Availability);
        Assert.Contains(site.Id, factory.Provisioning.Provisioned);
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/shop-sites", NewCreateRequest(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("ultra", "standard")]
    [InlineData("standard", "ultra")]
    public async Task Create_with_invalid_availability_or_database_kind_returns_400(string availability, string databaseKind)
    {
        var token = await RegisterAndGetTokenAsync();
        var request = new CreateShopSiteRequest("Bad Shop", availability, "0xabc", databaseKind);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/shop-sites", token, request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_valid_wallet_address_returns_201()
    {
        var token = await RegisterAndGetTokenAsync();
        var request = NewCreateRequest("Valid Wallet Shop") with { WalletAddress = "0x1234567890ABCDEF1234567890abcdef12345678" };

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/shop-sites", token, request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("not-a-wallet-address")]
    [InlineData("0x123")]
    [InlineData("1234567890abcdef1234567890abcdef12345678")]
    public async Task Create_with_an_invalid_wallet_address_returns_400_and_does_not_provision(string walletAddress)
    {
        var token = await RegisterAndGetTokenAsync();
        var request = NewCreateRequest("Bad Wallet Shop") with { WalletAddress = walletAddress };

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/shop-sites", token, request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/shop-sites", token));
        var sites = await listResponse.Content.ReadFromJsonAsync<List<ShopSiteDto>>(TestJson.Options);
        Assert.DoesNotContain(sites!, s => s.Name == "Bad Wallet Shop");
    }

    [Fact]
    public async Task Update_with_an_invalid_wallet_address_returns_400_and_does_not_change_the_site()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/shop-sites/{created.Id}", token, new UpdateShopSiteRequest("standard", "not-a-wallet-address")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var fetched = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}", token)))
            .Content.ReadFromJsonAsync<ShopSiteDto>(TestJson.Options);
        Assert.Equal(created.WalletAddress, fetched!.WalletAddress);
    }

    [Fact]
    public async Task Create_does_not_persist_a_shop_site_when_provisioning_fails()
    {
        var token = await RegisterAndGetTokenAsync();
        factory.Provisioning.ThrowOnProvision = new InvalidOperationException("simulated cluster failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/shop-sites", token, NewCreateRequest()));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.Provisioning.ThrowOnProvision = null;
        }

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/shop-sites", token));
        var sites = await listResponse.Content.ReadFromJsonAsync<List<ShopSiteDto>>(TestJson.Options);
        Assert.Empty(sites!);
    }

    [Fact]
    public async Task List_only_returns_the_authenticated_users_own_sites()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var ownerList = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/shop-sites", ownerToken)))
            .Content.ReadFromJsonAsync<List<ShopSiteDto>>(TestJson.Options);
        var otherList = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/shop-sites", otherToken)))
            .Content.ReadFromJsonAsync<List<ShopSiteDto>>(TestJson.Options);

        Assert.Contains(ownerList!, s => s.Id == created.Id);
        Assert.DoesNotContain(otherList!, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Get_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_id()
    {
        var token = await RegisterAndGetTokenAsync();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{Guid.NewGuid()}", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_availability_and_wallet_address_and_reflects_them_onto_the_crs()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);
        var update = new UpdateShopSiteRequest("high", "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/shop-sites/{created.Id}", token, update));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ShopSiteDto>(TestJson.Options);
        Assert.Equal("high", updated!.Availability);
        Assert.Equal("0xabcdefabcdefabcdefabcdefabcdefabcdefabcd", updated.WalletAddress);
        Assert.Contains(created.Id, factory.Provisioning.Updated);
    }

    [Fact]
    public async Task Update_with_invalid_availability_returns_400_and_does_not_change_the_site()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/shop-sites/{created.Id}", token, new UpdateShopSiteRequest("ultra", created.WalletAddress)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var fetched = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}", token)))
            .Content.ReadFromJsonAsync<ShopSiteDto>(TestJson.Options);
        Assert.Equal(created.Availability, fetched!.Availability);
    }

    [Fact]
    public async Task Update_rolls_back_the_in_memory_change_when_provisioning_update_fails()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        factory.Provisioning.ThrowOnUpdate = new InvalidOperationException("simulated cluster failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(
                HttpMethod.Put, $"/api/shop-sites/{created.Id}", token, new UpdateShopSiteRequest("high", created.WalletAddress)));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.Provisioning.ThrowOnUpdate = null;
        }

        var fetched = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}", token)))
            .Content.ReadFromJsonAsync<ShopSiteDto>(TestJson.Options);
        Assert.Equal("standard", fetched!.Availability);
    }

    [Fact]
    public async Task Delete_removes_the_site_and_deprovisions_the_crs()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/shop-sites/{created.Id}", token));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Contains(created.Id, factory.Provisioning.Deprovisioned);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}", token));
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_for_another_users_site_returns_404_and_does_not_delete_it()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/shop-sites/{created.Id}", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}", ownerToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetDashboardLink_returns_the_path_the_grafana_service_builds_for_the_owner()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/dashboard-link", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<DashboardLinkDto>(TestJson.Options);
        Assert.Equal($"/grafana-proxy/d/shop-{created.Id:N}?orgId=2", link!.Path);
    }

    [Fact]
    public async Task GetDashboardLink_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/dashboard-link", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboardLink_returns_502_when_grafana_provisioning_fails()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        factory.GrafanaProvisioning.ThrowOnGetDashboardPath = new InvalidOperationException("simulated Grafana failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/dashboard-link", token));
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.GrafanaProvisioning.ThrowOnGetDashboardPath = null;
        }
    }

    [Fact]
    public async Task GetAdminKey_returns_the_key_the_operator_provisioned()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/admin-key", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var adminKey = await response.Content.ReadFromJsonAsync<AdminKeyDto>(TestJson.Options);
        Assert.Equal($"fake-admin-key-shop-{created.Id:N}", adminKey!.Key);
    }

    [Fact]
    public async Task GetAdminKey_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/admin-key", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminKey_returns_502_when_the_secret_cannot_be_read()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        factory.ShopAdminKey.ThrowOnGetAdminKey = new InvalidOperationException("simulated Secret read failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/admin-key", token));
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.ShopAdminKey.ThrowOnGetAdminKey = null;
        }
    }

    [Fact]
    public async Task GetSiteUrl_returns_the_url_the_service_reports()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/site-url", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var siteUrl = await response.Content.ReadFromJsonAsync<SiteUrlDto>(TestJson.Options);
        Assert.Equal($"http://fake-host:30000/shop-{created.Id:N}", siteUrl!.Url);
    }

    [Fact]
    public async Task GetSiteUrl_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/site-url", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSiteUrl_returns_502_when_the_service_cannot_be_read()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        factory.ShopSiteUrl.ThrowOnGetSiteUrl = new InvalidOperationException("simulated Service read failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/site-url", token));
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.ShopSiteUrl.ThrowOnGetSiteUrl = null;
        }
    }

    [Fact]
    public async Task GetDiscordInviteUrl_returns_the_url_the_service_builds()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/discord/invite-url", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invite = await response.Content.ReadFromJsonAsync<DiscordInviteDto>(TestJson.Options);
        Assert.Equal("https://discord.com/oauth2/authorize?client_id=fake&scope=bot", invite!.InviteUrl);
    }

    [Fact]
    public async Task GetDiscordInviteUrl_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/discord/invite-url", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDiscordStatus_returns_the_status_the_service_reports()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);
        factory.ShopDiscord.StatusResult = new DiscordStatus(Attached: true, GuildId: "123", Ready: true, Message: "ok");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/discord/status", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<DiscordStatusDto>(TestJson.Options);
        Assert.True(status!.Attached);
        Assert.Equal("123", status.GuildId);
        Assert.True(status.Ready);
    }

    [Fact]
    public async Task GetDiscordStatus_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/discord/status", otherToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDiscordStatus_returns_502_when_the_cr_cannot_be_read()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);

        factory.ShopDiscord.ThrowOnGetStatus = new InvalidOperationException("simulated CR read failure");
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/shop-sites/{created.Id}/discord/status", token));
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.ShopDiscord.ThrowOnGetStatus = null;
        }
    }

    [Fact]
    public async Task AttachDiscord_attaches_once_the_bot_is_verified_as_a_guild_member()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);
        factory.ShopDiscord.VerifyResult = true;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/shop-sites/{created.Id}/discord/attach", token, new AttachDiscordRequest("999")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(created.Id, factory.ShopDiscord.AttachRequested);
        Assert.Contains("999", factory.ShopDiscord.GuildIdsVerified);
    }

    [Fact]
    public async Task AttachDiscord_returns_400_and_does_not_attach_when_the_bot_has_not_joined_the_guild()
    {
        var token = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(token);
        factory.ShopDiscord.VerifyResult = false;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/shop-sites/{created.Id}/discord/attach", token, new AttachDiscordRequest("999")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(created.Id, factory.ShopDiscord.AttachRequested);
    }

    [Fact]
    public async Task AttachDiscord_returns_404_for_another_users_site()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        var otherToken = await RegisterAndGetTokenAsync();
        var created = await CreateShopSiteAsync(ownerToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/shop-sites/{created.Id}/discord/attach", otherToken, new AttachDiscordRequest("999")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
