using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopHub.Api.Contracts;
using ShopHub.Api.Data;
using ShopHub.Api.Models;
using ShopHub.Api.Services;

namespace ShopHub.Api.Controllers;

[ApiController]
[Route("api/shop-sites")]
[Authorize]
public class ShopSitesController(
    ShopHubDbContext db,
    IShopProvisioningService provisioningService,
    IGrafanaProvisioningService grafanaProvisioningService,
    IShopAdminKeyService shopAdminKeyService,
    ILogger<ShopSitesController> logger) : ControllerBase
{
    private static readonly HashSet<string> ValidAvailabilities = ["standard", "high"];
    private static readonly HashSet<string> ValidDatabaseKinds = ["standard", "light"];

    [HttpPost]
    public async Task<ActionResult<ShopSiteDto>> Create(CreateShopSiteRequest request)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("Name is required."));
        }

        if (!ValidAvailabilities.Contains(request.Availability))
        {
            return BadRequest(new ErrorResponse("Availability must be 'standard' or 'high'."));
        }

        if (!ValidDatabaseKinds.Contains(request.DatabaseKind))
        {
            return BadRequest(new ErrorResponse("DatabaseKind must be 'standard' or 'light'."));
        }

        if (string.IsNullOrWhiteSpace(request.WalletAddress))
        {
            return BadRequest(new ErrorResponse("WalletAddress is required."));
        }

        var site = new ShopSite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Availability = request.Availability,
            WalletAddress = request.WalletAddress.Trim(),
            DatabaseKind = request.DatabaseKind,
        };

        // Provision the CRs before persisting: if the cluster rejects the request (bad
        // wallet format caught by the CRD's own validation, cluster unreachable, etc.) we
        // want no DB row at all rather than a ShopSite pointing at CRs that don't exist.
        try
        {
            await provisioningService.ProvisionAsync(site);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to provision custom resources for shop site {Name}", site.Name);
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Failed to provision the shop site in the cluster."));
        }

        db.ShopSites.Add(site);
        await db.SaveChangesAsync();

        // Best-effort: the Grafana dashboard is an optional add-on, not core to the shop
        // existing, so a Grafana-side failure here shouldn't roll back an otherwise-successful
        // creation the way a K8s provisioning failure above does. The dashboard link just won't
        // resolve until this is retried (e.g. next time the site is provisioned/updated).
        try
        {
            await grafanaProvisioningService.ProvisionAsync(site, GetUserEmail());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to provision Grafana dashboard for shop site {Id}", site.Id);
        }

        return StatusCode(StatusCodes.Status201Created, ShopSiteDto.FromEntity(site));
    }

    [HttpGet]
    public async Task<ActionResult<List<ShopSiteDto>>> List()
    {
        var userId = GetUserId();
        var sites = await db.ShopSites
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(sites.Select(ShopSiteDto.FromEntity).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShopSiteDto>> Get(Guid id)
    {
        var site = await FindOwnedSiteAsync(id);
        if (site is null)
        {
            return NotFound(new ErrorResponse("Shop site not found."));
        }

        return Ok(ShopSiteDto.FromEntity(site));
    }

    // Called right before the frontend opens the dashboard in a new tab — not something a plain
    // <a href> can point at directly, since actually viewing it requires switching the owner's
    // Grafana account into the dedicated org first (see GetDashboardPathAsync).
    [HttpGet("{id:guid}/dashboard-link")]
    public async Task<ActionResult<DashboardLinkDto>> GetDashboardLink(Guid id)
    {
        var site = await FindOwnedSiteAsync(id);
        if (site is null)
        {
            return NotFound(new ErrorResponse("Shop site not found."));
        }

        try
        {
            var path = await grafanaProvisioningService.GetDashboardPathAsync(site, GetUserEmail());
            return Ok(new DashboardLinkDto(path));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build Grafana dashboard link for shop site {Id}", site.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("The dashboard isn't available right now."));
        }
    }

    // The owner pastes the returned key into shophub-shop's own admin login for this shop
    // (catalog management, orders) — shophub-shop-operator provisions the key itself, this
    // just reveals it. Same shape as GetDashboardLink: reads a per-shop Secret the operator
    // owns rather than anything this API tracks itself.
    [HttpGet("{id:guid}/admin-key")]
    public async Task<ActionResult<AdminKeyDto>> GetAdminKey(Guid id)
    {
        var site = await FindOwnedSiteAsync(id);
        if (site is null)
        {
            return NotFound(new ErrorResponse("Shop site not found."));
        }

        try
        {
            var key = await shopAdminKeyService.GetAdminKeyAsync(site);
            return Ok(new AdminKeyDto(key));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read the admin key for shop site {Id}", site.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("The admin key isn't available right now."));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShopSiteDto>> Update(Guid id, UpdateShopSiteRequest request)
    {
        var site = await FindOwnedSiteAsync(id);
        if (site is null)
        {
            return NotFound(new ErrorResponse("Shop site not found."));
        }

        if (!ValidAvailabilities.Contains(request.Availability))
        {
            return BadRequest(new ErrorResponse("Availability must be 'standard' or 'high'."));
        }

        if (string.IsNullOrWhiteSpace(request.WalletAddress))
        {
            return BadRequest(new ErrorResponse("WalletAddress is required."));
        }

        var previousAvailability = site.Availability;
        var previousWalletAddress = site.WalletAddress;

        site.Availability = request.Availability;
        site.WalletAddress = request.WalletAddress.Trim();

        try
        {
            await provisioningService.UpdateAsync(site);
        }
        catch (Exception ex)
        {
            // Roll back the in-memory change so the DB write below (which hasn't happened
            // yet) can't persist values that were never actually applied to the CRs.
            site.Availability = previousAvailability;
            site.WalletAddress = previousWalletAddress;

            logger.LogError(ex, "Failed to update custom resources for shop site {Id}", site.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Failed to update the shop site in the cluster."));
        }

        await db.SaveChangesAsync();

        return Ok(ShopSiteDto.FromEntity(site));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var site = await FindOwnedSiteAsync(id);
        if (site is null)
        {
            return NotFound(new ErrorResponse("Shop site not found."));
        }

        try
        {
            await provisioningService.DeprovisionAsync(site);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deprovision custom resources for shop site {Id}", site.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Failed to remove the shop site from the cluster."));
        }

        db.ShopSites.Remove(site);
        await db.SaveChangesAsync();

        try
        {
            await grafanaProvisioningService.DeprovisionAsync(site);
        }
        catch (Exception ex)
        {
            // Same best-effort reasoning as ProvisionAsync above: a leftover Grafana folder is
            // an orphan to clean up later, not a reason to fail a delete that's otherwise done.
            logger.LogError(ex, "Failed to deprovision Grafana dashboard for shop site {Id}", site.Id);
        }

        return NoContent();
    }

    private Task<ShopSite?> FindOwnedSiteAsync(Guid id)
    {
        var userId = GetUserId();
        return db.ShopSites.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.Parse(sub!);
    }

    private string GetUserEmail() => User.FindFirstValue(JwtRegisteredClaimNames.Email)!;
}
