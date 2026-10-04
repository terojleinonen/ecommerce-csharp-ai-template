namespace ECommerce.Infrastructure.Data;

public enum DatabaseProvider
{
    Postgres,
    Sqlite,
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>PostgreSQL for deployments; SQLite for zero-dependency local runs and tests.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;

    /// <summary>Apply EF Core migrations on startup (PostgreSQL only).</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Insert the demo catalog when the database is empty.</summary>
    public bool SeedDemoData { get; set; }

    /// <summary>Optional bootstrap admin account, created once if it doesn't exist.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}
