using ECommerce.Core.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Data;

public sealed partial class DatabaseInitializer(
    AppDbContext db,
    IOptions<DatabaseOptions> options,
    IPasswordHasher<User> passwordHasher,
    TimeProvider clock,
    ILogger<DatabaseInitializer> logger)
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.RunAsync(ct);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        if (db.Database.IsRelational() && opts.Provider == DatabaseProvider.Postgres)
        {
            if (opts.MigrateOnStartup)
            {
                LogMigrating(logger);
                await db.Database.MigrateAsync(ct);
            }
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        if (opts.SeedDemoData)
            await SeedCatalogAsync(ct);

        if (!string.IsNullOrWhiteSpace(opts.AdminEmail) && !string.IsNullOrWhiteSpace(opts.AdminPassword))
            await EnsureAdminAsync(opts.AdminEmail, opts.AdminPassword, ct);
    }

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        var categories = SeedData.Categories();
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync(ct);

        var ids = categories.ToDictionary(c => c.Slug, c => c.Id);
        db.Products.AddRange(SeedData.ProductsFor(ids, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        LogSeeded(logger, categories.Count);
    }

    private async Task EnsureAdminAsync(string email, string password, CancellationToken ct)
    {
        var normalized = User.Normalize(email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct)) return;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalized,
            DisplayName = "Store Admin",
            Role = UserRole.Admin,
            CreatedAt = clock.GetUtcNow(),
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        LogAdminCreated(logger, admin.Email);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying database migrations")]
    private static partial void LogMigrating(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo catalog with {CategoryCount} categories")]
    private static partial void LogSeeded(ILogger logger, int categoryCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created admin user {Email}")]
    private static partial void LogAdminCreated(ILogger logger, string email);
}
