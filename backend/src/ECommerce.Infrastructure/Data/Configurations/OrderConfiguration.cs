using ECommerce.Core.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(o => o.Id).ValueGeneratedNever();
        b.Property(o => o.OrderNumber).HasMaxLength(32).IsRequired();
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(o => o.OrderNumber).IsUnique();
        b.HasIndex(o => new { o.UserId, o.CreatedAt });

        b.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(x => x.FullName).HasMaxLength(100).HasColumnName("ShipFullName");
            a.Property(x => x.Line1).HasMaxLength(200).HasColumnName("ShipLine1");
            a.Property(x => x.Line2).HasMaxLength(200).HasColumnName("ShipLine2");
            a.Property(x => x.PostalCode).HasMaxLength(16).HasColumnName("ShipPostalCode");
            a.Property(x => x.City).HasMaxLength(100).HasColumnName("ShipCity");
            a.Property(x => x.Country).HasMaxLength(2).HasColumnName("ShipCountry");
        });
        b.Navigation(o => o.ShippingAddress).IsRequired();

        b.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasOne<Core.Users.User>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(i => i.ProductName).HasMaxLength(120).IsRequired();
        b.Property(i => i.Sku).HasMaxLength(32).IsRequired();
        b.Ignore(i => i.LineTotal);
        b.HasIndex(i => i.ProductId);

        b.HasOne<Core.Catalog.Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
