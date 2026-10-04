using System.Security.Claims;
using System.Text;
using ECommerce.Core.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Api.Auth;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var opts = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(opts.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opts.Issuer,
            Audience = opts.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(AuthConstants.RoleClaim, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(CreateKey(opts.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        return (Handler.CreateToken(descriptor), expires);
    }

    public static SymmetricSecurityKey CreateKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}

public static class AuthConstants
{
    public const string RoleClaim = "role";
    public const string AdminPolicy = "Admin";
}
