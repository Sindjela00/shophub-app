using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ShopHub.Api.Contracts;
using Xunit;

namespace ShopHub.Api.IntegrationTests;

[Collection(ShopHubApiCollection.Name)]
public class AuthEndpointTests(ShopHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterRequest NewRegisterRequest(string? email = null) =>
        new(email ?? $"user-{Guid.NewGuid():N}@example.com", "CorrectHorseBattery1");

    private async Task<AuthResponse> RegisterAsync(RegisterRequest? request = null)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", request ?? NewRegisterRequest(), TestJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Register_with_valid_data_returns_201_with_a_token()
    {
        var request = NewRegisterRequest();

        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.Equal(request.Email, auth!.Email);
        Assert.False(string.IsNullOrEmpty(auth.Token));
    }

    [Fact]
    public async Task Register_lowercases_and_trims_the_email()
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"  User-{unique}@Example.com  ", "CorrectHorseBattery1"), TestJson.Options);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.Equal($"user-{unique}@example.com", auth!.Email);
    }

    [Fact]
    public async Task Register_with_invalid_email_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("not-an-email", "CorrectHorseBattery1"), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_short_password_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest() with { Password = "short" }, TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_an_already_registered_email_returns_409()
    {
        var request = NewRegisterRequest();
        await RegisterAsync(request);

        var response = await _client.PostAsJsonAsync("/api/auth/register", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_with_a_token()
    {
        var registerRequest = NewRegisterRequest();
        await RegisterAsync(registerRequest);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(registerRequest.Email, registerRequest.Password), TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.Equal(registerRequest.Email, auth!.Email);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var registerRequest = NewRegisterRequest();
        await RegisterAsync(registerRequest);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(registerRequest.Email, "TotallyWrongPassword1"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest($"nobody-{Guid.NewGuid():N}@example.com", "CorrectHorseBattery1"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_a_valid_token_returns_the_authenticated_users_claims()
    {
        var registerRequest = NewRegisterRequest();
        var auth = await RegisterAsync(registerRequest);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestJson.Options);
        Assert.Equal(auth.UserId.ToString(), body!["userId"]);
        Assert.Equal(auth.Email, body["email"]);
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_a_forged_signature_returns_401()
    {
        var registerRequest = NewRegisterRequest();
        var auth = await RegisterAsync(registerRequest);

        // Flip the first character of the signature segment — same header/payload, invalid signature.
        // Must be the first character, not the last: with a 32-byte HMAC-SHA256 signature the
        // base64url encoding's final character carries 2 padding bits that .NET's decoder
        // ignores, so swapping A<->B there is a no-op whenever that character is one of
        // A/B/C/D (same top 4 bits) and the token stays validly signed. The first character
        // always sits in a full 3-byte group with no padding bits, so tampering it is guaranteed
        // to change the decoded signature.
        var parts = auth.Token.Split('.');
        var tamperedSignature = (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..];
        var forgedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forgedToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
