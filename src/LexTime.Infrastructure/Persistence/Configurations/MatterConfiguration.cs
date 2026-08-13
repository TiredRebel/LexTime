using LexTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexTime.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Matter"/> to <c>dbo.Matters</c>. Column types follow docs/prd.md §3.
/// </summary>
public sealed class MatterConfiguration : IEntityTypeConfiguration<Matter>
{
    /// <summary>Applies the mapping.</summary>
    /// <param name="builder">Builder for the <see cref="Matter"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Matter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Matters");
        builder.HasKey(m => m.MatterId);

        builder.Property(m => m.MatterNumber).HasMaxLength(30).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(250).IsRequired();
        builder.Property(m => m.IsBillableByDefault).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.CreatedAtUtc).HasColumnType("datetime2(3)").IsRequired();

        builder.HasOne(m => m.Client)
            .WithMany(c => c.Matters)
            .HasForeignKey(m => m.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite, not global. A matter number is the firm's reference within one
        // client's file, so two clients may each have a matter numbered 001. A unique
        // index on MatterNumber alone would reject the second one, and would do it in
        // seeding rather than in a test — which is why UniquenessConstraintTests asserts
        // both halves of this.
        builder.HasIndex(m => new { m.ClientId, m.MatterNumber })
            .IsUnique()
            .HasDatabaseName("UX_Matters_ClientId_MatterNumber");
    }
}
