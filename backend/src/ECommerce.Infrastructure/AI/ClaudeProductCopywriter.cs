using System.Globalization;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;
using ECommerce.Core.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

internal sealed partial class ClaudeProductCopywriter(
    AnthropicClient client,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeProductCopywriter> logger) : IProductCopywriter
{
    public string Provider => "claude";

    public async Task<string> WriteDescriptionAsync(ProductDto product, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        var opts = options.Value;

        var facts = string.Create(CultureInfo.InvariantCulture, $"""
            Product name: {product.Name}
            Category: {product.Category}
            Price: {product.Price:0.00} EUR
            Current description / notes: {(string.IsNullOrWhiteSpace(product.Description) ? "(none)" : product.Description)}
            """);

        var parameters = new MessageCreateParams
        {
            Model = opts.Model,
            MaxTokens = opts.MaxTokens,
            System = Prompts.Copywriter,
            OutputConfig = new BetaOutputConfig { Effort = ClaudeShoppingAssistant.ParseEffort(opts.CopywriterEffort) },
            Messages = [new() { Role = Role.User, Content = facts }],
        };
        if (opts.EnableRefusalFallbacks)
            parameters = parameters with { Betas = ["server-side-fallback-2026-07-01"], Fallbacks = new Default() };

        try
        {
            var response = await client.Beta.Messages.Create(parameters, ct);
            if (response.StopReason == "refusal")
                throw new DomainException("The AI declined to write a description for this product.", "ai_refusal");

            var text = string.Concat(response.Content
                .Select(b => b.TryPickText(out var t) ? t.Text : null)
                .Where(t => t is not null)).Trim();

            return string.IsNullOrWhiteSpace(text)
                ? throw new DomainException("The AI returned an empty description.", "ai_empty")
                : text;
        }
        catch (AnthropicException ex)
        {
            LogFailure(logger, ex);
            throw new AiUnavailableException("The AI copywriter is temporarily unavailable.", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Claude copywriter request failed")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
