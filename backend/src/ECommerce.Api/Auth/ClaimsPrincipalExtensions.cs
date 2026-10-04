using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ECommerce.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("The access token has no valid subject.");
    }
}
