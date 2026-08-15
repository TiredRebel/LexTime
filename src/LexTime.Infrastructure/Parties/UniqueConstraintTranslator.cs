using LexTime.Application.Parties;
using Microsoft.Data.SqlClient;

namespace LexTime.Infrastructure.Parties;

/// <summary>Translates the two party uniqueness constraints into API-facing conflicts.</summary>
public static class UniqueConstraintTranslator
{
    private const string ClientIndex = "UX_Clients_ClientCode";
    private const string MatterIndex = "UX_Matters_ClientId_MatterNumber";

    /// <summary>
    /// Finds a known party uniqueness violation in a SQL Server exception.
    /// </summary>
    /// <param name="exception">SQL exception from the failed insert.</param>
    /// <param name="conflict">Translated conflict when the index is known.</param>
    /// <returns>True only for error 2601 or 2627 involving a known index.</returns>
    public static bool TryTranslate(SqlException exception, out PartyConflict? conflict)
    {
        ArgumentNullException.ThrowIfNull(exception);
        conflict = null;

        foreach (SqlError error in exception.Errors)
        {
            if (error.Number is not (2601 or 2627))
            {
                continue;
            }

            if (error.Message.Contains(ClientIndex, StringComparison.OrdinalIgnoreCase))
            {
                conflict = new("clientCode", string.Empty);
                return true;
            }

            if (error.Message.Contains(MatterIndex, StringComparison.OrdinalIgnoreCase))
            {
                conflict = new("matterNumber", string.Empty);
                return true;
            }
        }

        return false;
    }

}
