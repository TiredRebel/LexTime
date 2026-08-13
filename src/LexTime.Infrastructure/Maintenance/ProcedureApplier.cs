using System.Data;
using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Maintenance;

/// <summary>
/// Applies source-controlled stored procedures from <c>db/programmability</c>.
/// </summary>
/// <remarks>
/// Constitution P7: procedures live as <c>CREATE OR ALTER</c> files and are never created by
/// an EF migration, so their history is a readable diff of the procedure itself rather than
/// a sequence of opaque <c>Sql("...")</c> calls in migration files.
/// </remarks>
/// <param name="context">Supplies the connection.</param>
public sealed class ProcedureApplier(LexTimeDbContext context)
{
    /// <summary>Directory holding the procedure files, relative to the repository root.</summary>
    public const string DirectoryName = "db/programmability";

    /// <summary>
    /// Executes every <c>.sql</c> file in the directory, in sorted filename order.
    /// </summary>
    /// <remarks>
    /// An empty or absent directory is success, not failure. It is the state this feature
    /// ships in — the first procedure arrives with feature 003 — so it is the default path
    /// rather than an edge case.
    /// </remarks>
    /// <param name="repositoryRoot">Directory containing <c>db/programmability</c>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of files applied.</returns>
    public async Task<int> ApplyAllAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        var directory = Path.Combine(repositoryRoot, DirectoryName);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var files = Directory.GetFiles(directory, "*.sql");
        Array.Sort(files, StringComparer.Ordinal);

        if (files.Length == 0)
        {
            return 0;
        }

        var connection = (SqlConnection)context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);

            // CA2100 is set to `error` in .editorconfig, and this command text genuinely is
            // not a literal — it is the contents of a file. The input is source-controlled
            // and applied by a developer against their own database; it is not user input
            // and never crosses a trust boundary. Reviewed as a P24 item and recorded in
            // docs/agent-log.md. Suppressed here rather than repository-wide, so that any
            // other non-literal SQL still fails the build.
#pragma warning disable CA2100
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
#pragma warning restore CA2100

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return files.Length;
    }
}
