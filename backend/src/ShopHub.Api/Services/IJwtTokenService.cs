using ShopHub.Api.Models;

namespace ShopHub.Api.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
