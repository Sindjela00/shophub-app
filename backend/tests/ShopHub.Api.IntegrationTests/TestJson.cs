using System.Text.Json;

namespace ShopHub.Api.IntegrationTests;

internal static class TestJson
{
    // The API serializes camelCase; be explicit rather than relying on HttpClient defaults.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
