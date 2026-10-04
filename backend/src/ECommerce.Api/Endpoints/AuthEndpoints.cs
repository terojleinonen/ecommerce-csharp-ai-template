using ECommerce.Api.Auth;
using ECommerce.Core.Users;

namespace ECommerce.Api.Endpoints;

internal static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
                TypedResults.Ok(await auth.RegisterAsync(request, ct)))
            .WithName("Register")
            .RequireRateLimiting(RateLimitPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
                TypedResults.Ok(await auth.LoginAsync(request, ct)))
            .WithName("Login")
            .RequireRateLimiting(RateLimitPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (HttpContext http, IAuthService auth, CancellationToken ct) =>
                TypedResults.Ok(await auth.GetUserAsync(http.User.GetUserId(), ct)))
            .WithName("GetCurrentUser")
            .RequireAuthorization();

        return group;
    }
}
