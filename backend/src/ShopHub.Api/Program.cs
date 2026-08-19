using System.IdentityModel.Tokens.Jwt;
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
using ShopHub.Api.Observability;
using ShopHub.Api.Services;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

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
        //
        // The query param alone only gets the *first* request there: the HTML Grafana returns
        // references its own JS/CSS/image assets with relative URLs, which the browser resolves
        // against <base href> and requests directly — dropping the original querystring
        // entirely. Those follow-up requests 401 with no cookie fallback (confirmed for real:
        // Grafana's own "failed to load its application files" screen, since its client bundle
        // never finishes loading). A short-lived, path-scoped cookie set on that first request
        // covers them: the browser attaches it automatically to every same-path request that
        // follows, asset loads included.
        bearerOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!context.Request.Path.StartsWithSegments("/grafana-proxy"))
                {
                    return Task.CompletedTask;
                }

                if (context.Request.Query.TryGetValue("access_token", out var token))
                {
                    context.Token = token;
                    context.Response.Cookies.Append("grafana_proxy_token", token!, new CookieOptions
                    {
                        Path = "/grafana-proxy",
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        MaxAge = TimeSpan.FromMinutes(5),
                    });
                }
                else if (context.Request.Cookies.TryGetValue("grafana_proxy_token", out var cookieToken))
                {
                    context.Token = cookieToken;
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
builder.Services.AddScoped<IShopAdminKeyService, ShopAdminKeyService>();
builder.Services.AddScoped<IShopSiteUrlService, ShopSiteUrlService>();

builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection(DiscordOptions.SectionName));
builder.Services.AddHttpClient<IShopDiscordService, ShopDiscordService>(client =>
{
    // Trailing slash matters: combined with ShopDiscordService's relative (no leading slash)
    // request URIs, this is what keeps "/api/v10" instead of a bare request URI discarding it.
    client.BaseAddress = new Uri("https://discord.com/api/v10/");
});

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

var app = builder.Build();

// First, so every request is counted even if later middleware redirects/short-circuits it.
app.UseMiddleware<TrafficMetricsMiddleware>();

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
app.MapPrometheusScrapingEndpoint();

app.Run();

public partial class Program;
