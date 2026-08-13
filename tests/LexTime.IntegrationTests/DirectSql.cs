using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.IntegrationTests;

/// <summary>
/// Helpers for writing to the database without going through EF Core's entity tracking or
/// any application-layer validation.
/// </summary>
/// <remarks>
/// Constitution P6 requires the database to enforce its rules independently, and feature
/// 002 will load 400,000 rows through a bulk path that bypasses application validation
/// entirely. Testing constraints through EF entities would prove only that EF agrees with
/// itself; these helpers put the SQL in front of the engine directly.
/// </remarks>
internal static class DirectSql
{
    /// <summary>
    /// Inserts a client and returns its generated identifier.
    /// </summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="clientCode">Code for the new client; must be unique.</param>
    /// <returns>The generated <c>ClientId</c>.</returns>
    public static async Task<int> InsertClientAsync(LexTimeDbContext context, string clientCode)
    {
        var id = await ScalarAsync(
            context,
            """
            INSERT INTO dbo.Clients (ClientCode, Name, IsActive, CreatedAtUtc)
            OUTPUT INSERTED.ClientId
            VALUES (@code, @name, 1, SYSUTCDATETIME());
            """,
            new SqlParameter("@code", clientCode),
            new SqlParameter("@name", $"Client {clientCode}")).ConfigureAwait(false);

        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Inserts a timekeeper and returns its generated identifier.
    /// </summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="email">Email for the new user; must be unique.</param>
    /// <returns>The generated <c>UserId</c>.</returns>
    public static async Task<int> InsertUserAsync(LexTimeDbContext context, string email)
    {
        var id = await ScalarAsync(
            context,
            """
            INSERT INTO dbo.Users (Email, FullName, DefaultHourlyRate, IsActive, CreatedAtUtc)
            OUTPUT INSERTED.UserId
            VALUES (@email, @name, 350.00, 1, SYSUTCDATETIME());
            """,
            new SqlParameter("@email", email),
            new SqlParameter("@name", "Test Timekeeper")).ConfigureAwait(false);

        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Inserts a matter for a client and returns its generated identifier.
    /// </summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="clientId">Owning client.</param>
    /// <param name="matterNumber">Matter number, unique within that client only.</param>
    /// <returns>The generated <c>MatterId</c>.</returns>
    public static async Task<int> InsertMatterAsync(
        LexTimeDbContext context,
        int clientId,
        string matterNumber)
    {
        var id = await ScalarAsync(
            context,
            """
            INSERT INTO dbo.Matters (ClientId, MatterNumber, Name, IsBillableByDefault, IsActive, CreatedAtUtc)
            OUTPUT INSERTED.MatterId
            VALUES (@clientId, @number, @name, 1, 1, SYSUTCDATETIME());
            """,
            new SqlParameter("@clientId", clientId),
            new SqlParameter("@number", matterNumber),
            new SqlParameter("@name", $"Matter {matterNumber}")).ConfigureAwait(false);

        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Inserts a time entry with the given duration and work date.
    /// </summary>
    /// <remarks>
    /// <paramref name="isBillable"/> and <paramref name="hourlyRate"/> default to the values
    /// this helper hard-coded before feature 003, so the constraint tests written against it
    /// are unaffected. The rollup fixture needs both: a report that never sees a non-billable
    /// entry or two clients at different rates cannot exercise its own arithmetic.
    /// </remarks>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="userId">Timekeeper the entry belongs to.</param>
    /// <param name="matterId">Matter the entry is recorded against.</param>
    /// <param name="durationMinutes">Duration to attempt, valid or otherwise.</param>
    /// <param name="workDate">Billing date to attempt.</param>
    /// <param name="isBillable">Whether the entry counts toward billable totals.</param>
    /// <param name="hourlyRate">
    /// Rate snapshotted onto the entry. A snapshot, not a lookup — the report multiplies by
    /// this value and never by the timekeeper's current rate.
    /// </param>
    /// <returns>A task that completes when the insert has been attempted.</returns>
    public static Task InsertTimeEntryAsync(
        LexTimeDbContext context,
        int userId,
        int matterId,
        int durationMinutes,
        DateOnly workDate,
        bool isBillable = true,
        decimal hourlyRate = 350.00m) =>
        context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO dbo.TimeEntries
                (UserId, MatterId, WorkDate, DurationMinutes, IsBillable,
                 HourlyRateSnapshot, Narrative, CreatedAtUtc)
            VALUES (@userId, @matterId, @workDate, @duration, @isBillable,
                    @rate, 'Test entry', SYSUTCDATETIME());
            """,
            new SqlParameter("@userId", userId),
            new SqlParameter("@matterId", matterId),
            new SqlParameter("@workDate", workDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@duration", durationMinutes),
            new SqlParameter("@isBillable", isBillable),
            new SqlParameter("@rate", hourlyRate));

    /// <summary>
    /// Executes a statement that returns a single value.
    /// </summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="sql">The statement to run. A literal in every call site.</param>
    /// <param name="parameters">Parameters for the statement.</param>
    /// <returns>The scalar the statement produced.</returns>
    private static async Task<object> ScalarAsync(
        LexTimeDbContext context,
        string sql,
        params SqlParameter[] parameters)
    {
        var connection = (SqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync().ConfigureAwait(false);
        }

#pragma warning disable CA2100 // sql is a compile-time literal at every call site in this file.
        await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
        command.Parameters.AddRange(parameters);

        return await command.ExecuteScalarAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Insert returned no identity value.");
    }
}
