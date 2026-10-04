using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Infrastructure.Data;

/// <summary>Used by `dotnet ef` to create PostgreSQL migrations without booting the API.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ecommerce;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
