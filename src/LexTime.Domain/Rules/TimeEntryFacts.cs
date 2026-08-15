namespace LexTime.Domain.Rules;

/// <summary>
/// Everything the rules must be told in order to be evaluated.
/// </summary>
/// <remarks>
/// Four of the seven checks cannot be answered from a submission alone — they need the day's
/// other minutes, today's date, and three active flags. Constitution P6 puts the rules in the
/// domain and P4 forbids the domain any persistence reference, so the domain states what it
/// needs and is told, rather than reaching for it.
/// <para>
/// The consequence worth having: evaluation is a pure function of this record. Every rule, every
/// boundary, refusing and accepting, can be tested exhaustively in milliseconds with no database
/// anywhere. A rule suite that needed a container per case would be slow enough that nobody
/// would grow it.
/// </para>
/// </remarks>
/// <param name="DurationMinutes">The submitted duration. Rules 1, 2 and 3 read it.</param>
/// <param name="WorkDate">The submitted billing date. Rule 4 reads it.</param>
/// <param name="Today">
/// The current date, supplied rather than read. A rule that called a clock itself could not be
/// tested without waiting for the calendar.
/// </param>
/// <param name="OtherMinutesOnDate">
/// The timekeeper's total minutes for <paramref name="WorkDate"/>, <b>excluding the entry being
/// revised</b>. The word <em>other</em> is load-bearing: counting an entry against itself makes
/// a duration impossible to reduce, because the day's total would already include the value
/// being replaced.
/// </param>
/// <param name="MatterIsActive">Whether the target matter is active. Half of rule 5.</param>
/// <param name="ClientIsActive">
/// Whether that matter's client is active. Carried separately from the matter's flag so the
/// refusal can name which of the two failed.
/// </param>
/// <param name="TimekeeperIsActive">Whether the timekeeper is active (FR-013).</param>
/// <param name="EvaluateWorkDate">
/// Whether rule 4 applies. False when an update leaves the work date untouched — an update that
/// does not change a field is not a submission of that field, so an entry recorded 200 days ago
/// may still have its narrative corrected but may not have its date moved.
/// </param>
/// <param name="EvaluateMatter">
/// Whether rule 5 applies, on the same reasoning as <paramref name="EvaluateWorkDate"/>.
/// </param>
public sealed record TimeEntryFacts(
    int DurationMinutes,
    DateOnly WorkDate,
    DateOnly Today,
    int OtherMinutesOnDate,
    bool MatterIsActive,
    bool ClientIsActive,
    bool TimekeeperIsActive,
    bool EvaluateWorkDate = true,
    bool EvaluateMatter = true);
