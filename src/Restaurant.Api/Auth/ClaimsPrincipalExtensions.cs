using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Restaurant.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new InvalidOperationException("Token has no sub claim.");
        return Guid.Parse(sub);
    }
}
