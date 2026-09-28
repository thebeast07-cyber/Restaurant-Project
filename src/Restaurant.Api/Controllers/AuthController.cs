using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _tokenService;

    public AuthController(AppDbContext db, JwtTokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    // NOTE: with a single seeded Tenant, looking up by Username alone is unambiguous.
    // If a second Tenant is ever onboarded, login must be scoped by tenant (e.g. subdomain
    // or org code) before this lookup — tracked as a fast-follow, not a Day-1 concern.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _db.Users
            .SingleOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var token = _tokenService.IssueToken(user);
        return Ok(new LoginResponse(token, user.Name, user.Role.ToString(), user.TenantId, user.BranchId));
    }
}
