using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopHub.Api.Contracts;
using ShopHub.Api.Services;

namespace ShopHub.Api.Controllers;

// Not shop-scoped — anyone with a ShopHub account can reach the platform's own dashboard,
// unlike ShopSitesController's per-shop dashboard link which only the owner can request.
[ApiController]
[Route("api/platform")]
[Authorize]
public class PlatformController(IGrafanaProvisioningService grafanaProvisioningService, ILogger<PlatformController> logger) : ControllerBase
{
    // Same "open a blank tab, then navigate it once we have the real URL" pattern the frontend
    // already uses for a shop's own dashboard link — see GetDashboardLink.
    [HttpGet("dashboard-link")]
    public async Task<ActionResult<DashboardLinkDto>> GetDashboardLink()
    {
        try
        {
            var path = await grafanaProvisioningService.GetPlatformDashboardPathAsync(GetUserEmail());
            return Ok(new DashboardLinkDto(path));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build the platform Grafana dashboard link");
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("The dashboard isn't available right now."));
        }
    }

    private string GetUserEmail() => User.FindFirstValue(JwtRegisteredClaimNames.Email)!;
}
