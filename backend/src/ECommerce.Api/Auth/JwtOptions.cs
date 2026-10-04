using System.ComponentModel.DataAnnotations;

namespace ECommerce.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "ecommerce-api";

    [Required]
    public string Audience { get; set; } = "ecommerce-web";

    /// <summary>HMAC-SHA256 key. Must be at least 32 characters; supply via secrets/env in production.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 24 * 60)]
    public int AccessTokenMinutes { get; set; } = 60;
}
