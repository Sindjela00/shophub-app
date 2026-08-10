using Microsoft.EntityFrameworkCore;
using ShopHub.Api.Models;

namespace ShopHub.Api.Data;

public class ShopHubDbContext(DbContextOptions<ShopHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Email).HasMaxLength(320);
            entity.HasIndex(u => u.Email).IsUnique();
        });
    }
}
