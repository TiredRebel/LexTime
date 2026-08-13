using System.Data;
using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Seeding;

/// <summary>
/// Loads a generated dataset into the database with <c>SqlBulkCopy</c>.
/// </summary>
/// <remarks>
/// <para>
/// Not EF change tracking. Inserting 400,000 tracked entities takes minutes and would miss
/// FR-022's sub-minute target by an order of magnitude. <c>SqlBulkCopy</c> is bulk load,
/// which is neither the ORM writes nor the reporting reads constitution P5 governs, and it
/// lives here beside the context for that reason.
/// </para>
/// <para>
/// This path bypasses application validation entirely, so the schema's check and unique
/// constraints are the only thing standing between a generator bug and a corrupt dataset.
/// That is precisely what feature 001's User Story 2 was written for.
/// </para>
/// </remarks>
/// <param name="context">Supplies the connection and the transaction scope.</param>
public sealed class BulkSeeder(LexTimeDbContext context)
{
    /// <summary>
    /// Generates and loads the dataset.
    /// </summary>
    /// <param name="options">Volumes, shares, reference date and generator seed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of time entries loaded.</returns>
    public async Task<int> SeedAsync(
        SeedOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var data = SeedDataGenerator.Generate(options);

        var connection = (SqlConnection)context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        // Parents first: identity values have to exist before children can reference them.
        var userIds = await LoadUsersAsync(connection, data, cancellationToken).ConfigureAwait(false);
        var clientIds = await LoadClientsAsync(connection, data, cancellationToken).ConfigureAwait(false);
        var matterIds = await LoadMattersAsync(connection, data, clientIds, cancellationToken)
            .ConfigureAwait(false);

        await LoadEntriesAsync(connection, data, userIds, matterIds, cancellationToken)
            .ConfigureAwait(false);

        return data.Entries.Count;
    }

    /// <summary>Bulk-loads a table and returns the identity values it assigned, in insert order.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="table">Rows to load, with columns named as in the target table.</param>
    /// <param name="tableName">Fully qualified destination table.</param>
    /// <param name="keyColumn">Identity column to read back.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The assigned keys, ordered as the rows were supplied.</returns>
    private static async Task<int[]> LoadAndReadKeysAsync(
        SqlConnection connection,
        DataTable table,
        string tableName,
        string keyColumn,
        CancellationToken cancellationToken)
    {
        using (var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = tableName,
            BatchSize = 10_000,
            BulkCopyTimeout = 300,
        })
        {
            // Explicit, never positional. A positional mapping silently loads the wrong
            // column the first time one is added, and the failure surfaces as wrong data
            // rather than as an error.
            foreach (DataColumn column in table.Columns)
            {
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulk.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
        }

        var keys = new List<int>(table.Rows.Count);

#pragma warning disable CA2100 // tableName and keyColumn are compile-time literals at every call site.
        await using var command = new SqlCommand(
            $"SELECT [{keyColumn}] FROM {tableName} ORDER BY [{keyColumn}];", connection);
#pragma warning restore CA2100

        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            keys.Add(reader.GetInt32(0));
        }

        return [.. keys];
    }

    /// <summary>Loads timekeepers.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="data">The generated dataset.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Assigned user identifiers, in generation order.</returns>
    private static Task<int[]> LoadUsersAsync(
        SqlConnection connection,
        SeedDataSet data,
        CancellationToken cancellationToken)
    {
        var table = new DataTable("Users");
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("FullName", typeof(string));
        table.Columns.Add("DefaultHourlyRate", typeof(decimal));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        foreach (var user in data.Users)
        {
            table.Rows.Add(
                user.Email, user.FullName, user.DefaultHourlyRate, user.IsActive, user.CreatedAtUtc);
        }

        return LoadAndReadKeysAsync(connection, table, "[dbo].[Users]", "UserId", cancellationToken);
    }

    /// <summary>Loads clients.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="data">The generated dataset.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Assigned client identifiers, in generation order.</returns>
    private static Task<int[]> LoadClientsAsync(
        SqlConnection connection,
        SeedDataSet data,
        CancellationToken cancellationToken)
    {
        var table = new DataTable("Clients");
        table.Columns.Add("ClientCode", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        foreach (var client in data.Clients)
        {
            table.Rows.Add(client.ClientCode, client.Name, client.IsActive, client.CreatedAtUtc);
        }

        return LoadAndReadKeysAsync(
            connection, table, "[dbo].[Clients]", "ClientId", cancellationToken);
    }

    /// <summary>Loads matters, resolving client indices to keys.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="data">The generated dataset.</param>
    /// <param name="clientIds">Client keys, in generation order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Assigned matter identifiers, in generation order.</returns>
    private static Task<int[]> LoadMattersAsync(
        SqlConnection connection,
        SeedDataSet data,
        int[] clientIds,
        CancellationToken cancellationToken)
    {
        var table = new DataTable("Matters");
        table.Columns.Add("ClientId", typeof(int));
        table.Columns.Add("MatterNumber", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("IsBillableByDefault", typeof(bool));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        foreach (var matter in data.Matters)
        {
            table.Rows.Add(
                clientIds[matter.ClientIndex],
                matter.MatterNumber,
                matter.Name,
                matter.IsBillableByDefault,
                matter.IsActive,
                matter.CreatedAtUtc);
        }

        return LoadAndReadKeysAsync(
            connection, table, "[dbo].[Matters]", "MatterId", cancellationToken);
    }

    /// <summary>Loads time entries, resolving user and matter indices to keys.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="data">The generated dataset.</param>
    /// <param name="userIds">User keys, in generation order.</param>
    /// <param name="matterIds">Matter keys, in generation order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the load finishes.</returns>
    private static async Task LoadEntriesAsync(
        SqlConnection connection,
        SeedDataSet data,
        int[] userIds,
        int[] matterIds,
        CancellationToken cancellationToken)
    {
        var table = new DataTable("TimeEntries");
        table.Columns.Add("UserId", typeof(int));
        table.Columns.Add("MatterId", typeof(int));
        table.Columns.Add("WorkDate", typeof(DateTime));
        table.Columns.Add("DurationMinutes", typeof(int));
        table.Columns.Add("IsBillable", typeof(bool));
        table.Columns.Add("HourlyRateSnapshot", typeof(decimal));
        table.Columns.Add("Narrative", typeof(string));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        foreach (var entry in data.Entries)
        {
            table.Rows.Add(
                userIds[entry.UserIndex],
                matterIds[entry.MatterIndex],
                entry.WorkDate.ToDateTime(TimeOnly.MinValue),
                entry.DurationMinutes,
                entry.IsBillable,
                entry.HourlyRateSnapshot,
                entry.Narrative,
                entry.CreatedAtUtc);
        }

        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "[dbo].[TimeEntries]",
            BatchSize = 10_000,
            BulkCopyTimeout = 600,
        };

        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulk.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
    }
}
