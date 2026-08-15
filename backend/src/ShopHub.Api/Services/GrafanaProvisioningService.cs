using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

public class GrafanaProvisioningService(
    HttpClient client,
    IOptions<GrafanaOptions> options,
    ILogger<GrafanaProvisioningService> logger) : IGrafanaProvisioningService
{
    private GrafanaOptions Options => options.Value;

    public async Task ProvisionAsync(ShopSite site, string ownerEmail, CancellationToken cancellationToken = default)
    {
        var orgId = await EnsureUsersOrgAsync(cancellationToken);
        var userId = await EnsureGlobalUserAsync(ownerEmail, cancellationToken);
        await EnsureOrgMembershipAsync(orgId, ownerEmail, cancellationToken);

        var folderUid = site.K8sName;
        await EnsureFolderAsync(folderUid, site.Name, cancellationToken);
        await ProvisionDashboardAsync(folderUid, site, cancellationToken);

        // Replaces the folder's whole ACL — this is what actually removes the org-wide
        // "any Viewer in this org can see any unrestricted folder" default down to just this
        // one user, not merely an addition on top of it.
        await SetFolderPermissionsAsync(folderUid, userId, cancellationToken);
    }

    public async Task<string> GetDashboardPathAsync(ShopSite site, string ownerEmail, CancellationToken cancellationToken = default)
    {
        // Grafana's dashboard-by-uid routes only resolve against the viewer's *current* org —
        // a `?orgId=` query param on the URL does not switch it (confirmed against a real
        // instance: a multi-org user whose active org is still their original default gets a
        // 404 fetching a dashboard that lives in a different org they're also a member of).
        // Switching it here, authenticated *as that user* via the same X-WEBAUTH-USER trust
        // auth.proxy itself relies on, means the browser's own navigation always lands in the
        // right org on the very first click — not just on the second visit.
        var orgId = await EnsureUsersOrgAsync(cancellationToken);
        var switchOrg = await SendAsync(HttpMethod.Post, $"/api/user/using/{orgId}", body: null, useOrgScopedAuth: false, cancellationToken, impersonateEmail: ownerEmail);
        await EnsureSuccessAsync(switchOrg, "switch owner's active Grafana org", cancellationToken);

        return $"/grafana-proxy/d/{site.K8sName}?orgId={orgId}";
    }

    public async Task DeprovisionAsync(ShopSite site, CancellationToken cancellationToken = default)
    {
        // Deleting the folder cascades to the dashboard (and its permissions) inside it —
        // nothing else to clean up. The Grafana user/org-membership stay; they're harmless
        // with zero folders left to see and may still be needed for other shops they own.
        var response = await SendAsync(HttpMethod.Delete, $"/api/folders/{site.K8sName}", body: null, useOrgScopedAuth: true, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
        {
            await EnsureSuccessAsync(response, $"delete folder '{site.K8sName}'", cancellationToken);
        }
    }

    private async Task<int> EnsureUsersOrgAsync(CancellationToken cancellationToken)
    {
        var lookup = await SendAsync(HttpMethod.Get, $"/api/orgs/name/{Uri.EscapeDataString(Options.UsersOrgName)}", body: null, useOrgScopedAuth: false, cancellationToken);
        if (lookup.StatusCode == HttpStatusCode.OK)
        {
            var org = await lookup.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
            return org!["id"]!.GetValue<int>();
        }

        var create = await SendAsync(HttpMethod.Post, "/api/orgs", new { name = Options.UsersOrgName }, useOrgScopedAuth: false, cancellationToken);
        await EnsureSuccessAsync(create, "create org", cancellationToken);
        var created = await create.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        return created!["orgId"]!.GetValue<int>();
    }

    private async Task<int> EnsureGlobalUserAsync(string email, CancellationToken cancellationToken)
    {
        var lookup = await SendAsync(HttpMethod.Get, $"/api/users/lookup?loginOrEmail={Uri.EscapeDataString(email)}", body: null, useOrgScopedAuth: false, cancellationToken);
        if (lookup.StatusCode == HttpStatusCode.OK)
        {
            var user = await lookup.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
            return user!["id"]!.GetValue<int>();
        }

        // Never signed in with — access only ever happens through ShopHub's own auth.proxy
        // reverse proxy, which authenticates via the ShopHub JWT and never touches this
        // password. Still a real, unguessable secret rather than a fixed placeholder, on the
        // off chance direct Grafana login is ever re-enabled for this org.
        var password = RandomNumberGenerator.GetHexString(32);
        var create = await SendAsync(HttpMethod.Post, "/api/admin/users", new
        {
            name = email,
            email,
            login = email,
            password,
        }, useOrgScopedAuth: false, cancellationToken);
        await EnsureSuccessAsync(create, "create Grafana user", cancellationToken);
        var created = await create.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
        return created!["id"]!.GetValue<int>();
    }

    private async Task EnsureOrgMembershipAsync(int orgId, string email, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/orgs/{orgId}/users", new
        {
            loginOrEmail = email,
            role = "Viewer",
        }, useOrgScopedAuth: false, cancellationToken);

        // 409 == already a member — fine, this runs on every shop a user creates.
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict))
        {
            await EnsureSuccessAsync(response, "add user to org", cancellationToken);
        }
    }

    private async Task EnsureFolderAsync(string uid, string title, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/folders", new { uid, title }, useOrgScopedAuth: true, cancellationToken);

        // 409/412 == already exists (folder uids are per-shop and stable, so this only ever
        // matters if a previous provisioning attempt got partway through and retried).
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed))
        {
            await EnsureSuccessAsync(response, "create folder", cancellationToken);
        }
    }

    private async Task ProvisionDashboardAsync(string folderUid, ShopSite site, CancellationToken cancellationToken)
    {
        var dashboard = ShopDashboard.Build(site);
        var response = await SendAsync(HttpMethod.Post, "/api/dashboards/db", new
        {
            dashboard,
            folderUid,
            overwrite = true,
        }, useOrgScopedAuth: true, cancellationToken);
        await EnsureSuccessAsync(response, "provision dashboard", cancellationToken);
    }

    private async Task SetFolderPermissionsAsync(string folderUid, int grafanaUserId, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/folders/{folderUid}/permissions", new
        {
            items = new[] { new { userId = grafanaUserId, permission = 1 /* View */ } },
        }, useOrgScopedAuth: true, cancellationToken);
        await EnsureSuccessAsync(response, "set folder permissions", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, bool useOrgScopedAuth, CancellationToken cancellationToken, string? impersonateEmail = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (impersonateEmail is not null)
        {
            // Trusted the same way Grafana's [auth.proxy] trusts it from the reverse proxy —
            // safe here for the same reason: Grafana is only reachable from inside the cluster,
            // and this HttpClient's traffic originates from ShopHub's own backend pod.
            request.Headers.Add("X-WEBAUTH-USER", impersonateEmail);
        }
        else
        {
            request.Headers.Authorization = useOrgScopedAuth
                ? new AuthenticationHeaderValue("Bearer", Options.UsersOrgServiceAccountToken)
                : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{Options.AdminUser}:{Options.AdminPassword}")));
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Grafana API call to {Action} failed: {StatusCode} {Body}", action, response.StatusCode, body);
            throw new InvalidOperationException($"Grafana API call to {action} failed: {response.StatusCode} {body}");
        }
    }
}
