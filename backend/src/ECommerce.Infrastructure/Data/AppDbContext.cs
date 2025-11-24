using Microsoft.EntityFrameworkCore;
using ECommerce.Core.Entities;

namespace ECommerce.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Sku = "SKU-001", Name = "Sample T-Shirt", Description = "A comfy T-shirt", Price = 19.90m, Category = "Clothing", ImageUrl = "/images/sample-shirt.jpg" },
            new Product { Id = 2, Sku = "SKU-002", Name = "Gaming Mouse", Description = "RGB gaming mouse", Price = 49.90m, Category = "Electronics", ImageUrl = "/images/gaming-mouse.jpg" }
        );
    }
}
