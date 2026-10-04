using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using ECommerce.Core.AI;
using ECommerce.Core.Catalog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

/// <summary>
/// Claude-powered shopping assistant. Runs a bounded tool-use loop over read-only catalog tools,
/// then returns the reply together with the real products it referenced.
/// Falls back to the offline assistant if the API is unavailable so the storefront never breaks.
/// </summary>
internal sealed partial class ClaudeShoppingAssistant(
    AnthropicClient client,
    ICatalogService catalog,
    OfflineShoppingAssistant offline,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeShoppingAssistant> logger) : IShoppingAssistant
{
    private const string RefusalReply =
        "Sorry, I can't help with that request. I'm happy to help you find products or answer questions about the store, though!";

    public string Provider => "claude";

    public async Task<AssistantChatResponse> ReplyAsync(IReadOnlyList<ChatMessageDto> conversation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        try
        {
            return await RunAsync(conversation, ct);
        }
        catch (Exception ex) when (ex is AnthropicException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LogDegraded(logger, ex);
            return await offline.ReplyAsync(conversation, ct);
        }
    }

    private async Task<AssistantChatResponse> RunAsync(IReadOnlyList<ChatMessageDto> conversation, CancellationToken ct)
    {
        var opts = options.Value;
        var tools = new CatalogTools(catalog);
        var messages = conversation
            .Select(m => new BetaMessageParam
            {
                Role = m.Role == ChatRole.User ? Role.User : Role.Assistant,
                Content = m.Content,
            })
            .ToList();

        for (var iteration = 0; iteration < opts.MaxToolIterations; iteration++)
        {
            var response = await client.Beta.Messages.Create(BuildParams(opts, messages), ct);
            var stopReason = response.StopReason?.ToString() ?? "unknown";
            LogUsage(logger, iteration, stopReason, response.Usage.InputTokens, response.Usage.OutputTokens);

            // Always branch on stop_reason before reading content.
            if (response.StopReason == "refusal")
                return new AssistantChatResponse(RefusalReply, [], Provider);

            var (assistantContent, toolUses, text) = Collect(response);

            if (response.StopReason != "tool_use" || toolUses.Count == 0)
                return await FinishAsync(text, tools, ct);

            // Return every tool result in a single user message (keeps parallel tool use working).
            var results = new List<BetaContentBlockParam>(toolUses.Count);
            foreach (var use in toolUses)
            {
                var (content, isError) = await tools.ExecuteAsync(use.Name, use.Input, ct);
                results.Add(new BetaToolResultBlockParam { ToolUseID = use.ID, Content = content, IsError = isError });
            }

            messages.Add(new BetaMessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new BetaMessageParam { Role = Role.User, Content = results });
        }

        LogIterationLimit(logger, opts.MaxToolIterations);
        return new AssistantChatResponse(
            "I looked through a lot of the catalog but couldn't settle on an answer. Could you tell me a bit more about what you need?",
            [], Provider);
    }

    private static MessageCreateParams BuildParams(AnthropicOptions opts, List<BetaMessageParam> messages)
    {
        var parameters = new MessageCreateParams
        {
            Model = opts.Model,
            MaxTokens = opts.MaxTokens,
            System = new List<BetaTextBlockParam>
            {
                new() { Text = Prompts.ShoppingAssistant, CacheControl = new BetaCacheControlEphemeral() },
            },
            Tools = [.. CatalogTools.Definitions.Select(ToBetaTool)],
            OutputConfig = new BetaOutputConfig { Effort = ParseEffort(opts.AssistantEffort) },
            Messages = messages,
        };

        if (opts.EnableRefusalFallbacks)
        {
            parameters = parameters with
            {
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
            };
        }
        return parameters;
    }

    private static BetaToolUnion ToBetaTool(CatalogTools.ToolDefinition d) => new BetaTool
    {
        Name = d.Name,
        Description = d.Description,
        InputSchema = new()
        {
            Properties = d.Properties.ToDictionary(),
            Required = [.. d.Required],
        },
    };

    /// <summary>
    /// Converts response blocks into the assistant turn we echo back. Thinking blocks are kept unchanged
    /// (signatures must round-trip). If a server-side fallback happened mid-output, only text blocks from
    /// before the final fallback marker are echoed.
    /// </summary>
    private static (List<BetaContentBlockParam> Assistant, List<BetaToolUseBlock> ToolUses, string Text) Collect(BetaMessage response)
    {
        var lastFallback = -1;
        for (var i = 0; i < response.Content.Count; i++)
        {
            if (response.Content[i].TryPickFallback(out _)) lastFallback = i;
        }

        var assistant = new List<BetaContentBlockParam>();
        var toolUses = new List<BetaToolUseBlock>();
        var text = new System.Text.StringBuilder();

        for (var i = 0; i < response.Content.Count; i++)
        {
            var block = response.Content[i];
            var beforeBoundary = i < lastFallback;

            if (block.TryPickText(out var t))
            {
                assistant.Add(new BetaTextBlockParam { Text = t.Text });
                if (!beforeBoundary) text.Append(t.Text);
            }
            else if (beforeBoundary)
            {
                continue;
            }
            else if (block.TryPickThinking(out var thinking))
            {
                assistant.Add(new BetaThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
            }
            else if (block.TryPickRedactedThinking(out var redacted))
            {
                assistant.Add(new BetaRedactedThinkingBlockParam { Data = redacted.Data });
            }
            else if (block.TryPickToolUse(out var use))
            {
                assistant.Add(new BetaToolUseBlockParam { ID = use.ID, Name = use.Name, Input = use.Input });
                toolUses.Add(use);
            }
        }

        return (assistant, toolUses, text.ToString());
    }

    private async Task<AssistantChatResponse> FinishAsync(string text, CatalogTools tools, CancellationToken ct)
    {
        var (clean, referenced) = ProductReferences.Extract(text);
        // Only surface products the tools actually returned — guards against hallucinated ids.
        var ids = referenced.Where(tools.SeenProductIds.Contains).ToList();
        var products = await catalog.GetProductsByIdsAsync(ids, ct);
        var reply = string.IsNullOrWhiteSpace(clean) ? "Here's what I found:" : clean;
        return new AssistantChatResponse(reply, products, Provider);
    }

    internal static ApiEnum<string, Effort> ParseEffort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude unavailable, falling back to offline assistant")]
    private static partial void LogDegraded(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assistant iteration {Iteration}: stop={StopReason} in={InputTokens} out={OutputTokens}")]
    private static partial void LogUsage(ILogger logger, int iteration, string stopReason, long inputTokens, long outputTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assistant hit the tool iteration limit ({Limit})")]
    private static partial void LogIterationLimit(ILogger logger, int limit);
}
