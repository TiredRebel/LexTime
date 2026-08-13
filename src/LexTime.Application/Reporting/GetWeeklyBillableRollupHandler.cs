namespace LexTime.Application.Reporting;

/// <summary>
/// The weekly billable rollup use case.
/// </summary>
/// <remarks>
/// One handler class for one use case (constitution P4). It reads and wraps; it computes
/// nothing. If a figure ever needs deriving here, that is a sign the procedure is returning
/// less than its contract says it does.
/// </remarks>
/// <param name="reader">Supplies the rows. Implemented in the infrastructure layer.</param>
public sealed class GetWeeklyBillableRollupHandler(IWeeklyBillableRollupReader reader)
{
    /// <summary>
    /// Runs the rollup and packages it with the range it was computed over.
    /// </summary>
    /// <param name="query">
    /// The range and optional client filter. Assumed already validated: the endpoint refuses an
    /// inverted or incomplete range before this is reached, so a caller here has nothing left
    /// to reject.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rollup, with an empty row list when nothing matched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public async Task<WeeklyBillableRollupResponse> HandleAsync(
        WeeklyBillableRollupQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await reader.ReadAsync(query, cancellationToken).ConfigureAwait(false);

        return new WeeklyBillableRollupResponse(query.From, query.To, rows);
    }
}
