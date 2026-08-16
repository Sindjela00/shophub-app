using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.Tests;

public class GrafanaProvisioningServiceTests
{
    private const string OwnerEmail = "owner@example.com";

    private static ShopSite NewSite() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "Test Shop",
        Availability = "standard",
        WalletAddress = "0x1234567890abcdef1234567890abcdef12345678",
        DatabaseKind = "standard",
    };

    private static (GrafanaProvisioningService Service, RecordingHandler Handler) CreateService(
        Func<RecordedRequest, HttpResponseMessage>? respond = null)
    {
        var handler = new RecordingHandler(respond ?? DefaultRespond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake-grafana.test") };
        var options = Options.Create(new GrafanaOptions
        {
            BaseUrl = "https://fake-grafana.test",
            AdminUser = "admin",
            AdminPassword = "admin-password",
            UsersOrgServiceAccountToken = "fake-sa-token",
            UsersOrgName = "ShopHub Users",
        });
        var service = new GrafanaProvisioningService(client, options, NullLogger<GrafanaProvisioningService>.Instance);
        return (service, handler);
    }

    // A reasonable default for endpoints whose response shape a given test doesn't care about:
    // org/user lookups "not found" (so create paths run and get exercised), everything else OK.
    private static HttpResponseMessage DefaultRespond(RecordedRequest req) =>
        req.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = RawJsonContent("{\"message\":\"not found\"}") }
            : req.Uri.AbsolutePath switch
            {
                "/api/orgs" => new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{\"orgId\":2}") },
                "/api/admin/users" => new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{\"id\":7}") },
                _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{}") },
            };

    private static StringContent RawJsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task ProvisionAsync_creates_org_and_user_when_neither_exist_then_sets_owner_only_folder_permissions()
    {
        var site = NewSite();
        var (service, handler) = CreateService();

        await service.ProvisionAsync(site, OwnerEmail);

        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath == "/api/orgs");
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath == "/api/admin/users");
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath == "/api/orgs/2/users");

        var folderReq = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath == "/api/folders");
        Assert.Equal(site.K8sName, folderReq.BodyJson!.RootElement.GetProperty("uid").GetString());

        var dashboardReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == "/api/dashboards/db");
        Assert.Equal(site.K8sName, dashboardReq.BodyJson!.RootElement.GetProperty("folderUid").GetString());
        Assert.Equal(site.K8sName, dashboardReq.BodyJson.RootElement.GetProperty("dashboard").GetProperty("uid").GetString());

        var permissionsReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == $"/api/folders/{site.K8sName}/permissions");
        var items = permissionsReq.BodyJson!.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(7, items[0].GetProperty("userId").GetInt32());
        Assert.Equal(1, items[0].GetProperty("permission").GetInt32());
    }

    [Fact]
    public async Task ProvisionAsync_reuses_an_existing_org_and_user_instead_of_creating_new_ones()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req => req.Method == HttpMethod.Get
            ? req.Uri.AbsolutePath switch
            {
                "/api/orgs/name/ShopHub%20Users" => new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{\"id\":2}") },
                _ when req.Uri.AbsolutePath.StartsWith("/api/users/lookup") =>
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{\"id\":9}") },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{}") });

        await service.ProvisionAsync(site, OwnerEmail);

        Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath == "/api/orgs");
        Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath == "/api/admin/users");

        var permissionsReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == $"/api/folders/{site.K8sName}/permissions");
        Assert.Equal(9, permissionsReq.BodyJson!.RootElement.GetProperty("items")[0].GetProperty("userId").GetInt32());
    }

    [Fact]
    public async Task ProvisionAsync_treats_409_on_org_membership_and_folder_creation_as_already_done()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req => req.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : req.Uri.AbsolutePath is "/api/orgs/2/users" or "/api/folders"
                ? new HttpResponseMessage(HttpStatusCode.Conflict)
                : DefaultRespond(req));

        // Would throw if 409 on those two calls weren't treated as "already in this state".
        await service.ProvisionAsync(site, OwnerEmail);

        Assert.Contains(handler.Requests, r => r.Uri.AbsolutePath == $"/api/folders/{site.K8sName}/permissions");
    }

    [Fact]
    public async Task ProvisionAsync_stops_and_throws_when_dashboard_provisioning_fails_never_setting_permissions()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req =>
            req.Uri.AbsolutePath == "/api/dashboards/db"
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = RawJsonContent("{\"message\":\"uid too long, max 40 characters\"}") }
                : DefaultRespond(req));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(site, OwnerEmail));

        Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("/permissions"));
    }

    [Fact]
    public async Task ProvisionAsync_uses_bearer_service_account_auth_for_org_scoped_calls_and_basic_auth_for_admin_calls()
    {
        var site = NewSite();
        var (service, handler) = CreateService();

        await service.ProvisionAsync(site, OwnerEmail);

        var folderReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == "/api/folders");
        Assert.Equal("Bearer", folderReq.AuthScheme);
        Assert.Equal("fake-sa-token", folderReq.AuthParameter);

        var orgReq = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath == "/api/orgs");
        Assert.Equal("Basic", orgReq.AuthScheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:admin-password")), orgReq.AuthParameter);
    }

    [Fact]
    public async Task GetDashboardPathAsync_switches_the_owners_org_impersonated_via_the_trusted_header_and_returns_the_proxy_path()
    {
        var site = NewSite();
        var (service, handler) = CreateService(req => req.Uri.AbsolutePath == "/api/orgs/name/ShopHub%20Users"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = RawJsonContent("{\"id\":2}") }
            : DefaultRespond(req));

        var path = await service.GetDashboardPathAsync(site, OwnerEmail);

        Assert.Equal($"/grafana-proxy/d/{site.K8sName}?orgId=2", path);

        var switchReq = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath == "/api/user/using/2");
        Assert.Equal(HttpMethod.Post, switchReq.Method);
        Assert.Null(switchReq.AuthScheme);
        Assert.Equal(OwnerEmail, switchReq.WebAuthUserHeader);
    }

    [Fact]
    public async Task DeprovisionAsync_deletes_the_folder_and_treats_404_as_already_gone()
    {
        var site = NewSite();
        var (service, handler) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await service.DeprovisionAsync(site);

        var deleteReq = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, deleteReq.Method);
        Assert.Equal($"/api/folders/{site.K8sName}", deleteReq.Uri.AbsolutePath);
    }

    [Fact]
    public async Task DeprovisionAsync_propagates_a_non_404_delete_failure()
    {
        var site = NewSite();
        var (service, _) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = RawJsonContent("{\"message\":\"boom\"}") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeprovisionAsync(site));
    }

    [Fact]
    public void The_folder_and_dashboard_uid_derived_from_a_shop_never_exceeds_Grafanas_40_character_limit()
    {
        // Regression test: a shop's K8sName is "shop-" + a 32-char guid, already 37 characters —
        // a naive "{K8sName}-overview" dashboard uid scheme pushed this to 46 and Grafana
        // rejected it with "uid too long, max 40 characters" (caught for real, not by inspection).
        var site = NewSite();

        Assert.True(site.K8sName.Length <= 40);
    }

    internal sealed record RecordedRequest(
        HttpMethod Method, Uri Uri, JsonDocument? BodyJson, string? AuthScheme, string? AuthParameter, string? WebAuthUserHeader);

    internal sealed class RecordingHandler(Func<RecordedRequest, HttpResponseMessage> respond) : DelegatingHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri!,
                string.IsNullOrEmpty(body) ? null : JsonDocument.Parse(body),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-WEBAUTH-USER", out var values) ? values.First() : null);
            Requests.Add(recorded);

            var response = respond(recorded);
            response.RequestMessage = request;
            return response;
        }
    }
}
