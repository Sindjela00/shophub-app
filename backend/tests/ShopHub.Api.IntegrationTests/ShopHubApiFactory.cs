using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShopHub.Api.Data;
using ShopHub.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace ShopHub.Api.IntegrationTests;

/// <summary>
/// Boots the real app against a real, disposable Postgres container (Testcontainers). The
/// only thing swapped out is Kubernetes CR provisioning — that talks to a real cluster and is
/// covered separately, both by unit tests against a faked transport and by manual end-to-end
/// verification against a real local cluster (see the shop-site-management PR).
/// </summary>
public class ShopHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();

    public const string JwtSigningKey = "integration-test-signing-key-0123456789abcdef";
    public const string JwtIssuer = "shophub-app-tests";
    public const string JwtAudience = "shophub-app-tests-clients";

    public FakeShopProvisioningService Provisioning { get; } = new();
    public FakeGrafanaProvisioningService GrafanaProvisioning { get; } = new();
    public FakeShopAdminKeyService ShopAdminKey { get; } = new();
    public FakeShopSiteUrlService ShopSiteUrl { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:ExpiryMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IShopProvisioningService>();
            services.AddSingleton<IShopProvisioningService>(Provisioning);

            services.RemoveAll<IGrafanaProvisioningService>();
            services.AddSingleton<IGrafanaProvisioningService>(GrafanaProvisioning);

            services.RemoveAll<IShopAdminKeyService>();
            services.AddSingleton<IShopAdminKeyService>(ShopAdminKey);

            services.RemoveAll<IShopSiteUrlService>();
            services.AddSingleton<IShopSiteUrlService>(ShopSiteUrl);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ShopHubDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ShopHubApiCollection : ICollectionFixture<ShopHubApiFactory>
{
    public const string Name = "ShopHubApi";
}
