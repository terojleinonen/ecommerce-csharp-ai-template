namespace ECommerce.Core.Users;

public enum UserRole
{
    Customer,
    Admin,
}

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string DisplayName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Customer;
    public DateTimeOffset CreatedAt { get; set; }

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
