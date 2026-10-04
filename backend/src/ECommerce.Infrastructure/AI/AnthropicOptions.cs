namespace ECommerce.Infrastructure.AI;

public sealed class AnthropicOptions
{
    public const string SectionName = "AI:Anthropic";

    /// <summary>API key. Falls back to the conventional ANTHROPIC_API_KEY variable. Empty → offline mode.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>Reasoning effort for the chat assistant (low | medium | high | xhigh | max).</summary>
    public string AssistantEffort { get; set; } = "low";

    /// <summary>Reasoning effort for product copywriting.</summary>
    public string CopywriterEffort { get; set; } = "medium";

    public int MaxTokens { get; set; } = 8000;

    /// <summary>Upper bound on tool-use round trips per chat turn.</summary>
    public int MaxToolIterations { get; set; } = 6;

    /// <summary>Opt into server-side refusal fallbacks ("default" routing).</summary>
    public bool EnableRefusalFallbacks { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
