using ECommerce.Core.Catalog;
using ECommerce.Core.Users;
using ECommerce.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.UnitTests.TestSupport;

/// <summary>Isolated in-memory SQLite database seeded with a small catalog.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private TestDatabase(SqliteConnection connection) => _connection = connection;

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new TestDatabase(connection);
        await using var ctx = db.CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        await SeedAsync(ctx);
        return db;
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public static FixedClock Clock { get; } = new(Now);

    private static async Task SeedAsync(AppDbContext ctx)
    {
        var footwear = new Category { Name = "Footwear", Slug = "footwear" };
        var kitchen = new Category { Name = "Home & Kitchen", Slug = "home-kitchen" };
        ctx.Categories.AddRange(footwear, kitchen);
        await ctx.SaveChangesAsync();

        ctx.Products.AddRange(
            Product(1, "Trail Runner GTX", footwear.Id, 149m, 10, "Waterproof trail running shoe."),
            Product(2, "Canvas Sneaker", footwear.Id, 64.90m, 5, "Minimal everyday sneaker."),
            Product(3, "Winter Boot", footwear.Id, 179m, 0, "Insulated waterproof boot."),
            Product(4, "Pour-Over Coffee Set", kitchen.Id, 49.90m, 20, "Ceramic dripper and carafe for coffee."),
            Product(5, "Chef's Knife", kitchen.Id, 89m, 8, "Forged steel knife."),
            Product(6, "Archived Mug", kitchen.Id, 9m, 50, "Old mug.", active: false));

        ctx.Users.Add(new User
        {
            Id = UserId, Email = "shopper@test.dev", NormalizedEmail = "SHOPPER@TEST.DEV",
            DisplayName = "Shopper", PasswordHash = "x", CreatedAt = Now,
        });
        await ctx.SaveChangesAsync();
    }

    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Product Product(int id, string name, int categoryId, decimal price, int stock, string description, bool active = true) => new()
    {
        Id = id,
        Sku = $"SKU-{id:000}",
        Name = name,
        Slug = Core.Common.Slug.From(name),
        Description = description,
        Price = price,
        StockQuantity = stock,
        CategoryId = categoryId,
        IsActive = active,
        CreatedAt = Now.AddMinutes(-id),
        UpdatedAt = Now.AddMinutes(-id),
    };

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
