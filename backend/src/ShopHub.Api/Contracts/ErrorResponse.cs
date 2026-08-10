namespace ShopHub.Api.Contracts;

/// <summary>
/// Wraps error messages so they serialize as JSON. Returning a bare string from an action
/// (e.g. BadRequest("...")) hits ASP.NET Core's built-in StringOutputFormatter, which writes
/// it as text/plain instead of JSON.
/// </summary>
public record ErrorResponse(string Error);
