using System.Data;
using LexTime.Application.Reporting;
using Microsoft.Data.SqlClient;

namespace LexTime.Infrastructure.Reporting;

/// <summary>
/// Invokes <c>dbo.usp_WeeklyBillableRollup</c> and maps its result set.
/// </summary>
/// <remarks>
/// The one place raw ADO.NET appears in the solution's read path, and the point constitution
/// P5 is making: EF Core owns writes and simple entity reads, reporting goes to a procedure
/// invoked directly. No <c>DbContext</c>, no <c>FromSqlRaw</c>, no mapping onto entities.
/// <para>
/// This type does no arithmetic. Every figure arrives computed, including the three that only
/// have meaning across rows. A running total accumulated here instead would be invisible to a
/// reviewer reading the SQL and would defeat the feature.
/// </para>
/// </remarks>
/// <param name="connectionString">
/// The database to read from. Supplied by the registration in
/// <see cref="DependencyInjection"/>, which has already resolved and validated it. Taken
/// directly rather than through <c>LexTimeDbContext</c>: routing the deliberately-not-EF path
/// through EF to obtain a connection string would undo the separation it exists to show.
/// </param>
public sealed class SqlWeeklyBillableRollupReader(string connectionString)
    : IWeeklyBillableRollupReader
{
    /// <summary>The procedure this reader invokes. A constant — the command text never varies.</summary>
    public const string ProcedureName = "dbo.usp_WeeklyBillableRollup";

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public async Task<IReadOnlyList<WeeklyBillableRollupRow>> ReadAsync(
        WeeklyBillableRollupQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var connection = new SqlConnection(connectionString);
        await using var command = new SqlCommand(ProcedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
        };

        // Typed parameters, nothing concatenated, and a command text that is a constant. That
        // combination is what keeps CA2100 quiet without a suppression — a suppression
        // appearing here would mean the design had gone wrong, not that it needed justifying.
        //
        // Dates cross as DateTime at midnight against a SqlDbType.Date parameter. Passing them
        // as strings would reintroduce culture dependence at the boundary, which the procedure
        // works to avoid on its own side by anchoring week arithmetic on a fixed date.
        command.Parameters.Add("@FromDate", SqlDbType.Date).Value =
            query.From.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@ToDate", SqlDbType.Date).Value =
            query.To.ToDateTime(TimeOnly.MinValue);

        // Absent means every client, and is expressed as a null argument to the procedure
        // rather than as a different command. One procedure, one plan, one thing to review.
        command.Parameters.Add("@ClientId", SqlDbType.Int).Value =
            (object?)query.ClientId ?? DBNull.Value;

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ordinals resolved by name once, rather than hard-coded per row. The column order is
        // part of the procedure's contract, but two of the twelve columns are ints that would
        // map onto each other silently if that order ever changed: IsoYear and ClientId. A
        // wrong number is worse than an exception, so the names do the binding.
        var isoYear = reader.GetOrdinal("IsoYear");
        var isoWeek = reader.GetOrdinal("IsoWeek");
        var weekStartDate = reader.GetOrdinal("WeekStartDate");
        var clientId = reader.GetOrdinal("ClientId");
        var clientCode = reader.GetOrdinal("ClientCode");
        var clientName = reader.GetOrdinal("ClientName");
        var billableHours = reader.GetOrdinal("BillableHours");
        var nonBillableHours = reader.GetOrdinal("NonBillableHours");
        var billableAmount = reader.GetOrdinal("BillableAmount");
        var cumulativeBillableHours = reader.GetOrdinal("CumulativeBillableHours");
        var hoursDeltaVsPriorWeek = reader.GetOrdinal("HoursDeltaVsPriorWeek");
        var clientRankInWeek = reader.GetOrdinal("ClientRankInWeek");

        var rows = new List<WeeklyBillableRollupRow>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new WeeklyBillableRollupRow(
                reader.GetInt32(isoYear),
                reader.GetInt32(isoWeek),
                DateOnly.FromDateTime(reader.GetDateTime(weekStartDate)),
                reader.GetInt32(clientId),
                reader.GetString(clientCode),
                reader.GetString(clientName),
                reader.GetDecimal(billableHours),
                reader.GetDecimal(nonBillableHours),
                reader.GetDecimal(billableAmount),
                reader.GetDecimal(cumulativeBillableHours),

                // The null is preserved, never coalesced. It means "the prior week is outside
                // the requested range", which is a different statement from "the client billed
                // nothing last week" — and that second case arrives here as a number.
                reader.IsDBNull(hoursDeltaVsPriorWeek)
                    ? null
                    : reader.GetDecimal(hoursDeltaVsPriorWeek),

                reader.GetInt32(clientRankInWeek)));
        }

        return rows;
    }
}
