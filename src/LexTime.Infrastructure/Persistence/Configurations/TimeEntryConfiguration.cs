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

        // The covering index for dbo.usp_WeeklyBillableRollup, and the only index this schema
        // carries beyond its keys.
        //
        // Key columns are what the report filters and splits on: a WorkDate range in the
        // WHERE, and IsBillable deciding which side of every figure each row lands on.
        // Included columns are the only other columns the report reads from this table —
        // MatterId reaches the client, DurationMinutes feeds both hour totals,
        // HourlyRateSnapshot feeds the amount. Together they make the index covering, so the
        // query can be answered without returning to the clustered index per row.
        //
        // UserId and Narrative are deliberately not included. The report never reads them,
        // and every included column is paid for on every write.
        //
        // This arrived in feature 004 rather than with the schema, on purpose. Feature 003
        // shipped the rollup against the un-indexed table so that the before/after
        // measurement constitution P8 requires had a real "before" to measure rather than a
        // manufactured one. docs/performance.md holds the numbers.
        builder.HasIndex(e => new { e.WorkDate, e.IsBillable })
            .HasDatabaseName("IX_TimeEntries_WorkDate_Billable")
            .IncludeProperties(e => new { e.MatterId, e.DurationMinutes, e.HourlyRateSnapshot });

        // Still deliberately absent: any constraint on WorkDate. The 90-day backdating limit
        // governs what may be submitted through the API, not what may exist. Enforcing it in
        // the schema would reject the 24 months of history feature 002 seeds, and would make
        // the database progressively reject its own contents as time passed.
        // WorkDateConstraintTests asserts a three-year-old date is accepted.
    }
}
