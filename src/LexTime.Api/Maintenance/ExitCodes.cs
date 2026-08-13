namespace LexTime.Api.Maintenance;

/// <summary>
/// Process exit codes for the maintenance verbs, as specified in
/// <c>specs/002-bootstrap-and-seed/contracts/host-cli.md</c>.
/// </summary>
/// <remarks>
/// Distinct codes exist so the bootstrap script can branch on them. Collapsing every
/// failure to 1 would force the script to parse messages, which breaks the moment a message
/// is reworded.
/// </remarks>
public static class ExitCodes
{
    /// <summary>The verb completed.</summary>
    public const int Success = 0;

    /// <summary>Configuration is missing or invalid — no connection string, no signing key.</summary>
    public const int ConfigurationError = 1;

    /// <summary>The database could not be reached.</summary>
    public const int DatabaseUnreachable = 2;

    /// <summary>The verb ran and failed — a migration error, a procedure error, a load error.</summary>
    public const int OperationFailed = 3;

    /// <summary>Verification found a measured value outside its declared band.</summary>
    public const int VerificationFailed = 4;

    /// <summary>Seeding refused because the database was not empty.</summary>
    public const int DatabaseNotEmpty = 5;
}
