using ECommerce.Core.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Data.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(80).IsRequired();
        b.Property(c => c.Slug).HasMaxLength(80).IsRequired();
        b.Property(c => c.Description).HasMaxLength(500);
        b.HasIndex(c => c.Slug).IsUnique();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(p => p.Sku).HasMaxLength(32).IsRequired();
        b.Property(p => p.Name).HasMaxLength(120).IsRequired();
        b.Property(p => p.Slug).HasMaxLength(140).IsRequired();
        b.Property(p => p.Description).HasMaxLength(4000);
        b.Property(p => p.ImageUrl).HasMaxLength(500);
        b.Property(p => p.ConcurrencyStamp).IsConcurrencyToken();
        b.Ignore(p => p.IsAvailable);

        b.HasIndex(p => p.Sku).IsUnique();
        b.HasIndex(p => p.Slug).IsUnique();
        b.HasIndex(p => new { p.IsActive, p.CategoryId });

        b.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0");
            t.HasCheckConstraint("CK_Products_Stock_NonNegative", "\"StockQuantity\" >= 0");
        });
    }
}
