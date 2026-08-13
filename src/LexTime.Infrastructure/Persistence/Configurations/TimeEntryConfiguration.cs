using LexTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexTime.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TimeEntry"/> to <c>dbo.TimeEntries</c>. Column types follow
/// docs/prd.md §3.
/// </summary>
public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    /// <summary>Applies the mapping.</summary>
    /// <param name="builder">Builder for the <see cref="TimeEntry"/> entity type.</param>
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TimeEntries", t => t.HasCheckConstraint(
            "CK_TimeEntries_DurationMinutes",
            "[DurationMinutes] > 0 AND [DurationMinutes] % 6 = 0 AND [DurationMinutes] <= 1440"));

        builder.HasKey(e => e.TimeEntryId);

        builder.Property(e => e.WorkDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.DurationMinutes).IsRequired();
        builder.Property(e => e.IsBillable).IsRequired();
        builder.Property(e => e.HourlyRateSnapshot).HasColumnType("decimal(10,2)");
        builder.Property(e => e.Narrative).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne(e => e.User)
            .WithMany(u => u.TimeEntries)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Matter)
            .WithMany(m => m.TimeEntries)
            .HasForeignKey(e => e.MatterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two things are deliberately absent here. Neither is an oversight.
        //
        // 1. No constraint on WorkDate. The 90-day backdating limit governs what may be
        //    submitted through the API, not what may exist. Enforcing it in the schema
        //    would reject the 24 months of history feature 002 seeds, and would make the
        //    database progressively reject its own contents as time passed.
        //    WorkDateConstraintTests asserts a three-year-old date is accepted.
        //
        // 2. No covering index on (WorkDate, IsBillable). Its absence is the baseline
        //    feature 003 measures the rollup against; adding it here destroys the
        //    before/after comparison constitution P8 requires.
    }
}
