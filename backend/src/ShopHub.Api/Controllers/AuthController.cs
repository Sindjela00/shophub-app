using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopHub.Api.Contracts;
using ShopHub.Api.Data;
using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public partial class AuthController(
    ShopHubDbContext db,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService) : ControllerBase
{
    private const int MinPasswordLength = 8;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        if (!EmailPattern().IsMatch(email))
        {
            return BadRequest(new ErrorResponse("A valid email address is required."));
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return BadRequest(new ErrorResponse($"Password must be at least {MinPasswordLength} characters."));
        }

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new ErrorResponse("An account with this email already exists."));
        }

        var user = new User { Id = Guid.NewGuid(), Email = email, PasswordHash = "" };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = jwtTokenService.GenerateToken(user);
        return StatusCode(StatusCodes.Status201Created, new AuthResponse(user.Id, user.Email, token));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Same generic error whether the email doesn't exist or the password is wrong —
        // don't let a caller use this endpoint to discover which emails are registered.
        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Invalid email or password."));
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "");
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ErrorResponse("Invalid email or password."));
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            await db.SaveChangesAsync();
        }

        var token = jwtTokenService.GenerateToken(user);
        return Ok(new AuthResponse(user.Id, user.Email, token));
    }

    // Proves the issued token is actually accepted, not just issued — a bare-minimum
    // exercise of [Authorize]/JWT bearer validation rather than just token generation.
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        return Ok(new { userId, email });
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
