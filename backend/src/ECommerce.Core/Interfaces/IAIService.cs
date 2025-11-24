using ECommerce.Core.DTOs;

namespace ECommerce.Core.Interfaces;

public interface IAIService
{
    Task<string> GenerateProductDescriptionAsync(string name, string? existingDescription, CancellationToken ct = default);
    Task<string> ChatAsync(string message, CancellationToken ct = default);
}
