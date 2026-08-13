using LexTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexTime.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Client"/> to <c>dbo.Clients</c>. Column types follow docs/prd.md §3.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <summary>Applies the mapping.</summary>
    /// <param name="builder">Builder for the <see cref="Client"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Clients");
        builder.HasKey(c => c.ClientId);

        builder.Property(c => c.ClientCode).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnType("datetime2(3)").IsRequired();

        builder.HasIndex(c => c.ClientCode).IsUnique().HasDatabaseName("UX_Clients_ClientCode");
    }
}
