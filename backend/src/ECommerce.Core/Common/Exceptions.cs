namespace ECommerce.Core.Common;

/// <summary>A business rule was violated. Mapped to HTTP 400/409 by the API.</summary>
public class DomainException(string message, string code = "domain_error") : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>The requested resource does not exist (or is not visible to the caller).</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>The request conflicts with current state (duplicate key, concurrent update, ...).</summary>
public sealed class ConflictException(string message, string code = "conflict") : DomainException(message, code);

/// <summary>Credentials were invalid. Deliberately vague to avoid account enumeration.</summary>
public sealed class AuthenticationFailedException() : Exception("Invalid email or password.");

/// <summary>An upstream AI provider failed. Mapped to HTTP 503.</summary>
public sealed class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
