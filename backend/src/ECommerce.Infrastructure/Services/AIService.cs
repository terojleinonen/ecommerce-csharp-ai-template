using ECommerce.Core.DTOs;
using ECommerce.Core.Interfaces;

namespace ECommerce.Infrastructure.Services;

public class AIService : IAIService
{
    // In a real app, inject HttpClient + config with OpenAI (or other) API key.
    public Task<string> GenerateProductDescriptionAsync(string name, string? existingDescription, CancellationToken ct = default)
    {
        // Stub implementation
        var desc = existingDescription ?? $"AI generated description for {name}.";
        return Task.FromResult(desc + " (This is a stub – plug in a real LLM here.)");
    }

    public Task<string> ChatAsync(string message, CancellationToken ct = default)
    {
        // Stub implementation
        var reply = $"You said: '{message}'. This is a placeholder AI response. Wire me to a real LLM!";
        return Task.FromResult(reply);
    }
}
