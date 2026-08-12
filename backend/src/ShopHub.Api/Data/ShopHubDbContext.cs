using Microsoft.EntityFrameworkCore;
using ShopHub.Api.Models;

namespace ShopHub.Api.Data;

public class ShopHubDbContext(DbContextOptions<ShopHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ShopSite> ShopSites => Set<ShopSite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Email).HasMaxLength(320);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ShopSite>(entity =>
        {
            entity.Property(s => s.Name).HasMaxLength(200);
            entity.Property(s => s.Availability).HasMaxLength(20);
            entity.Property(s => s.WalletAddress).HasMaxLength(100);
            entity.Property(s => s.DatabaseKind).HasMaxLength(20);
            entity.HasIndex(s => s.UserId);
            entity.Ignore(s => s.K8sName);
        });
    }
}
