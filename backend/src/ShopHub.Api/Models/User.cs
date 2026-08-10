namespace ShopHub.Api.Models;

public class User
{
    public Guid Id { get; set; }

    /// <summary>Always stored lowercased; uniqueness and lookups rely on that.</summary>
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
