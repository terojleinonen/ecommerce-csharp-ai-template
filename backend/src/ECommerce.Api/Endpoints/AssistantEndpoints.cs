using ECommerce.Core.AI;
using ECommerce.Infrastructure.AI;
using Microsoft.Extensions.Options;

namespace ECommerce.Api.Endpoints;

internal static class AssistantEndpoints
{
    public const string RateLimitPolicy = "ai";
    private const int MaxHistory = 20;

    public static RouteGroupBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assistant").WithTags("AI Assistant");

        group.MapPost("/chat", async Task<IResult> (AssistantChatRequest request, IShoppingAssistant assistant, CancellationToken ct) =>
            {
                if (request.Messages[^1].Role != ChatRole.User)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["messages"] = ["The last message must come from the user."],
                    });
                }

                // Bound the context we send upstream; the client keeps the full transcript.
                var window = request.Messages.Skip(Math.Max(0, request.Messages.Count - MaxHistory)).ToList();
                while (window.Count > 0 && window[0].Role != ChatRole.User) window.RemoveAt(0);

                return TypedResults.Ok(await assistant.ReplyAsync(window, ct));
            })
            .WithName("AssistantChat")
            .WithSummary("Chat with the catalog-grounded shopping assistant")
            .Produces<AssistantChatResponse>()
            .ProducesValidationProblem()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapGet("/status", (IOptions<AnthropicOptions> options) =>
            {
                var o = options.Value;
                return TypedResults.Ok(new AssistantStatusDto(
                    o.IsConfigured ? "claude" : "offline", o.IsConfigured, o.IsConfigured ? o.Model : null));
            })
            .WithName("AssistantStatus");

        return group;
    }
}
