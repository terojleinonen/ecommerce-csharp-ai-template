using ECommerce.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(u => u.DisplayName).HasMaxLength(80).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}
