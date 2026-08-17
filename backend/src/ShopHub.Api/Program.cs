using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using k8s;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShopHub.Api.Data;
using ShopHub.Api.Models;
using ShopHub.Api.Services;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    // Wide open, but only ever applied under the Development environment check below —
    // the frontend's origin isn't fixed yet (dev server port, deployed domain, etc.).
    options.AddPolicy(DevCorsPolicy, policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ShopHubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Bound via IOptions instead of reading builder.Configuration directly above: this defers the
// read until the JWT bearer handler first needs it (first request), not at startup — so a
// WebApplicationFactory-based test's config overrides (applied after Program's top-level code
// runs but before the app serves requests) are picked up correctly.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var opts = jwtOptions.Value;

        // Without this, the validation pipeline silently remaps short claim types (e.g.
        // "sub", "email") to long ClaimTypes.* URIs, so a lookup by JwtRegisteredClaimNames
        // after validation finds nothing even though the token clearly has the claim.
        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opts.Issuer,
            ValidateAudience = true,
            ValidAudience = opts.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // The Grafana dashboard link is a plain browser navigation (new tab), which can't set
        // an Authorization header — only requests made through our own fetch() client can. This
        // is the standard ASP.NET Core escape hatch (also used for SignalR's WebSocket
        // handshake, which has the same problem) for exactly that case, scoped to only the
        // proxy path so every other endpoint still requires a real Authorization header.
        bearerOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/grafana-proxy") &&
                    context.Request.Query.TryGetValue("access_token", out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<KubernetesOptions>(builder.Configuration.GetSection(KubernetesOptions.SectionName));
builder.Services.AddSingleton<IKubernetes>(sp =>
{
    var k8sOptions = sp.GetRequiredService<IOptions<KubernetesOptions>>().Value;
    var config = k8sOptions.InCluster
        ? KubernetesClientConfiguration.InClusterConfig()
        : KubernetesClientConfiguration.BuildDefaultConfig();
    return new Kubernetes(config);
});
builder.Services.AddScoped<IShopProvisioningService, KubernetesShopProvisioningService>();

builder.Services.Configure<GrafanaOptions>(builder.Configuration.GetSection(GrafanaOptions.SectionName));
builder.Services.AddHttpClient<IGrafanaProvisioningService, GrafanaProvisioningService>((sp, client) =>
{
    var grafanaOptions = sp.GetRequiredService<IOptions<GrafanaOptions>>().Value;
    client.BaseAddress = new Uri(grafanaOptions.BaseUrl);
});

// Grafana has no public route of its own (see shophub-kube-state's Grafana access-control
// notes) — this is the *only* way a browser ever reaches it. Mounted at a fixed sub-path
// (Grafana is configured with a matching server.root_url/serve_from_sub_path) rather than a
// per-shop path: which shop(s) a viewer can actually see is enforced by Grafana's own folder
// permissions (see GrafanaProvisioningService), not by anything in this route.
var grafanaBaseUrl = builder.Configuration["Grafana:BaseUrl"]
    ?? throw new InvalidOperationException("Grafana:BaseUrl configuration is required.");
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes:
        [
            new RouteConfig
            {
                RouteId = "grafana-proxy",
                ClusterId = "grafana",
                Match = new RouteMatch { Path = "/grafana-proxy/{**catch-all}" },
            },
        ],
        clusters:
        [
            new ClusterConfig
            {
                ClusterId = "grafana",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["grafana"] = new() { Address = grafanaBaseUrl },
                },
            },
        ])
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            // Trusted by Grafana's [auth.proxy] config as the identity to sign in as — safe
            // only because Grafana is unreachable except through this already-authenticated
            // route (no public ingress), so nothing else can forge this header.
            var email = transformContext.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Email);
            transformContext.ProxyRequest.Headers.Remove("X-WEBAUTH-USER");
            if (email is not null)
            {
                transformContext.ProxyRequest.Headers.Add("X-WEBAUTH-USER", email);
            }

            return ValueTask.CompletedTask;
        });
    });

builder.Services.AddHttpForwarder();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

// No separate migration step exists anywhere in the deployment pipeline yet (the chart just
// creates the Deployment, nothing runs `dotnet ef database update` out-of-band), so gating this
// to Development left every real install with no schema at all. Runs unconditionally rather
// than only in Development; EF Core's migration lock (see Database.MigrateAsync's own
// "acquiring an exclusive lock" behavior) makes this safe if multiple replicas start at once.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ShopHubDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy().RequireAuthorization();

// Public — unlike /grafana-proxy, a Shop's storefront is meant for its own customers to browse
// and buy from, who have no ShopHub account/JWT at all. Destination is dynamic per shop (unlike
// Grafana's single fixed backend), so this uses YARP's direct-forwarding API instead of the
// declarative route/cluster config above. The path only ever carries a shop *id*, not its raw
// K8s service name, so this can't be used to reach arbitrary in-cluster services — only real,
// currently-existing ShopSites resolve to a destination.
var shopProxyHttpClient = new HttpMessageInvoker(new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
});
async Task ProxyToShopAsync(
    HttpContext context, Guid id, string? catchAll, ShopHubDbContext db, IHttpForwarder forwarder, IOptions<KubernetesOptions> kubernetesOptions)
{
    var site = await db.ShopSites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    if (site is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    // The Shop CR's own metadata.name (== site.K8sName) is what the operator names the
    // Deployment/Service after (shop_controller.go) — same value ShopHub already uses to talk
    // to the Shop/Wallet/DiscordChannel CRs themselves.
    var destinationPrefix = $"http://{site.K8sName}.{kubernetesOptions.Value.Namespace}.svc.cluster.local";
    await forwarder.SendAsync(context, destinationPrefix, shopProxyHttpClient, (ctx, request) =>
    {
        // The default transform mirrors this whole request's incoming path, including this
        // route's own "/shop-proxy/{id}" prefix — Map-with-a-route-template doesn't strip a
        // matched prefix from HttpContext.Request.Path the way classic path-branching Map does,
        // and the real Shop app obviously has no route registered for that prefix.
        request.RequestUri = new Uri($"{destinationPrefix}/{catchAll}{ctx.Request.QueryString}");
        return ValueTask.CompletedTask;
    });
}

// Two routes, not one: {**catchAll} alone doesn't match a bare "/shop-proxy/{id}" or
// "/shop-proxy/{id}/" — confirmed for real (both 404'd, never even reaching the handler) — so
// "open this shop's storefront root", the single most common case, needs its own route rather
// than relying on the catch-all to cover it.
app.Map("/shop-proxy/{id:guid}", (HttpContext context, Guid id, ShopHubDbContext db, IHttpForwarder forwarder, IOptions<KubernetesOptions> kubernetesOptions) =>
    ProxyToShopAsync(context, id, catchAll: "", db, forwarder, kubernetesOptions));
app.Map("/shop-proxy/{id:guid}/{**catchAll}", (HttpContext context, Guid id, string? catchAll, ShopHubDbContext db, IHttpForwarder forwarder, IOptions<KubernetesOptions> kubernetesOptions) =>
    ProxyToShopAsync(context, id, catchAll, db, forwarder, kubernetesOptions));

app.Run();

public partial class Program;
