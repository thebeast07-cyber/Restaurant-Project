using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Api.Auth;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Name = User.Identity?.Name,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            TenantId = User.FindFirst(AppClaimTypes.TenantId)?.Value,
            BranchId = User.FindFirst(AppClaimTypes.BranchId)?.Value
        });
    }
}
