using System.ComponentModel.DataAnnotations;

namespace ECommerce.Core.Users;

public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(128, MinimumLength = 8)]
    public required string Password { get; init; }

    [Required, StringLength(80, MinimumLength = 2)]
    public required string DisplayName { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
}
