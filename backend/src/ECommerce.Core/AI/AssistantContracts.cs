using System.ComponentModel.DataAnnotations;
using ECommerce.Core.Catalog;

namespace ECommerce.Core.AI;

public enum ChatRole
{
    User,
    Assistant,
}

public sealed record ChatMessageDto
{
    public ChatRole Role { get; init; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public required string Content { get; init; }
}

public sealed record AssistantChatRequest
{
    /// <summary>The conversation so far, oldest first. The last message must be from the user.</summary>
    [Required, MinLength(1), MaxLength(30)]
    public required IReadOnlyList<ChatMessageDto> Messages { get; init; }
}

/// <param name="Reply">Assistant answer (plain text / light markdown).</param>
/// <param name="Products">Catalog products the assistant recommended in this reply.</param>
/// <param name="Provider">Which backend produced the reply ("claude" or "offline").</param>
public sealed record AssistantChatResponse(string Reply, IReadOnlyList<ProductDto> Products, string Provider);

public sealed record AssistantStatusDto(string Provider, bool LlmEnabled, string? Model);

public sealed record GeneratedDescriptionDto(string Description, string Provider);

/// <summary>Conversational shopping assistant grounded in the live product catalog.</summary>
public interface IShoppingAssistant
{
    string Provider { get; }
    Task<AssistantChatResponse> ReplyAsync(IReadOnlyList<ChatMessageDto> conversation, CancellationToken ct = default);
}

/// <summary>Writes marketing copy for a product (admin tooling).</summary>
public interface IProductCopywriter
{
    string Provider { get; }
    Task<string> WriteDescriptionAsync(ProductDto product, CancellationToken ct = default);
}
