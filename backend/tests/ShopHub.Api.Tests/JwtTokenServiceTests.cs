using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.Tests;

public class JwtTokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-0123456789abcdef";

    private static JwtTokenService CreateService(int expiryMinutes = 60) => new(Options.Create(new JwtOptions
    {
        SigningKey = SigningKey,
        Issuer = "shophub-app-tests",
        Audience = "shophub-app-tests-clients",
        ExpiryMinutes = expiryMinutes,
    }));

    private static User NewUser() => new() { Id = Guid.NewGuid(), Email = "jwt-test@example.com", PasswordHash = "irrelevant" };

    [Fact]
    public void GenerateToken_embeds_sub_and_email_claims_for_the_user()
    {
        var user = NewUser();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService().GenerateToken(user));

        Assert.Equal(user.Id.ToString(), token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public void GenerateToken_sets_issuer_and_audience_from_options()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService().GenerateToken(NewUser()));

        Assert.Equal("shophub-app-tests", token.Issuer);
        Assert.Equal("shophub-app-tests-clients", token.Audiences.Single());
    }

    [Fact]
    public void GenerateToken_gives_each_call_a_unique_jti()
    {
        var service = CreateService();
        var handler = new JwtSecurityTokenHandler();
        var first = handler.ReadJwtToken(service.GenerateToken(NewUser()));
        var second = handler.ReadJwtToken(service.GenerateToken(NewUser()));

        Assert.NotEqual(
            first.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void GenerateToken_expires_after_the_configured_number_of_minutes()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService(expiryMinutes: 5).GenerateToken(NewUser()));

        var expectedExpiry = DateTime.UtcNow.AddMinutes(5);
        Assert.True(Math.Abs((token.ValidTo - expectedExpiry).TotalSeconds) < 10,
            $"Expected expiry near {expectedExpiry:o}, got {token.ValidTo:o}");
    }

    [Fact]
    public void GenerateToken_produces_a_token_that_validates_against_the_signing_key()
    {
        var user = NewUser();
        var jwt = CreateService().GenerateToken(user);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "shophub-app-tests",
            ValidateAudience = true,
            ValidAudience = "shophub-app-tests-clients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
        };

        // Mirrors Program.cs's JwtBearerOptions.MapInboundClaims = false: without it,
        // ValidateToken silently remaps "sub" to the long ClaimTypes.NameIdentifier URI and
        // a lookup by JwtRegisteredClaimNames.Sub finds nothing (the bug fixed for issue #2).
        var handler = new JwtSecurityTokenHandler { InboundClaimTypeMap = new Dictionary<string, string>() };
        var principal = handler.ValidateToken(jwt, parameters, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    }

    [Fact]
    public void GenerateToken_produces_a_token_that_fails_validation_with_the_wrong_signing_key()
    {
        var jwt = CreateService().GenerateToken(NewUser());

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "shophub-app-tests",
            ValidateAudience = true,
            ValidAudience = "shophub-app-tests-clients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-signing-key-abcdef")),
            ValidateLifetime = true,
        };

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(jwt, parameters, out _));
    }
}
