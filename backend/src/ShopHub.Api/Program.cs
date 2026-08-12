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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ShopHubDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
