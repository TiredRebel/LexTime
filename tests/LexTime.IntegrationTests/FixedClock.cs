namespace LexTime.IntegrationTests;

/// <summary>
/// A clock that always reports the same instant.
/// </summary>
/// <remarks>
/// Rule 4 is a rule about <em>today</em>: a work date may not be in the future and may not be
/// more than 90 days in the past. A test that asserts a literal date sits inside that window
/// passes today and fails in three months, and a suite that rots on a date is worse than no
/// suite — it fails while nothing is wrong, and people learn to ignore it. Every date in the
/// rule tests is computed relative to this clock instead (FR-026, SC-009).
/// <para>
/// Five lines rather than a package reference. <c>Microsoft.Extensions.TimeProvider.Testing</c>
/// supplies <c>FakeTimeProvider</c> and would be the conventional choice, but adding a
/// dependency to a repository whose quality argument is that a reviewer needs nothing installed
/// is a poor trade for one overridden method.
/// </para>
/// </remarks>
/// <param name="now">The instant this clock reports, for as long as it exists.</param>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>A clock fixed to an arbitrary but stated date, for tests with no date of their own.</summary>
    /// <remarks>
    /// Chosen to be far from any month or year boundary, so a test that accidentally depends on
    /// one fails here rather than only in January.
    /// </remarks>
    public static FixedClock Default { get; } = new(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The date this clock reports, which is what rule 4 measures against.</summary>
    public DateOnly Today => DateOnly.FromDateTime(this.GetUtcNow().UtcDateTime);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
