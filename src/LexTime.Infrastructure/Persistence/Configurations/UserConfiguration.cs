using LexTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexTime.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="User"/> to <c>dbo.Users</c>. Column types follow docs/prd.md §3.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>Applies the mapping.</summary>
    /// <param name="builder">Builder for the <see cref="User"/> entity type.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();

        // Money, not measurement: decimal rather than a floating-point type, so that a
        // rate multiplied across 400,000 rows does not accumulate representation error.
        builder.Property(u => u.DefaultHourlyRate).HasColumnType("decimal(10,2)");

        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAtUtc).HasColumnType("datetime2(3)").IsRequired();

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UX_Users_Email");
    }
}
