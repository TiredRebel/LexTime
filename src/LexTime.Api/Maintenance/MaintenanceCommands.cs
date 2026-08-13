using System.Data.Common;
using System.Globalization;
using LexTime.Infrastructure.Maintenance;
using LexTime.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.Api.Maintenance;

/// <summary>
/// The application's command-line surface: the verbs the bootstrap script calls instead of
/// installing tools or adding endpoints.
/// </summary>
/// <remarks>
/// This is what keeps seeding out of a fifth project (constitution P4) and
/// <c>dotnet-ef</c> out of the quickstart (P18). Contract:
/// <c>specs/002-bootstrap-and-seed/contracts/host-cli.md</c>.
/// </remarks>
public static class MaintenanceCommands
{
    /// <summary>Verbs this class recognises, for the usage message.</summary>
    private static readonly string[] KnownVerbs =
        ["migrate", "apply-procedures", "seed", "verify-seed", "state", "mint-token"];

    /// <summary>
    /// Whether these arguments ask for a maintenance verb rather than a web host.
    /// </summary>
    /// <remarks>
    /// Deliberately stricter than "were any arguments supplied". The test host supplies
    /// arguments of its own, so treating a non-empty array as a maintenance invocation
    /// makes every <c>WebApplicationFactory</c> test start no server and fail with "no web
    /// application was configured". The first argument must be a verb this class owns.
    /// </remarks>
    /// <param name="args">Process arguments.</param>
    /// <returns><see langword="true"/> when the first argument is a known verb.</returns>
    public static bool IsMaintenanceInvocation(string[] args) =>
        args is { Length: > 0 } && KnownVerbs.Contains(args[0], StringComparer.Ordinal);

    /// <summary>
    /// Runs a maintenance verb against the built host's services.
    /// </summary>
    /// <param name="services">The host's root service provider.</param>
    /// <param name="args">Process arguments, the first of which is the verb.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A process exit code from <see cref="ExitCodes"/>.</returns>
    public static async Task<int> RunAsync(
        IServiceProvider services,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);

        var verb = args[0];
        if (!KnownVerbs.Contains(verb, StringComparer.Ordinal))
        {
            Console.Error.WriteLine(
                $"Unknown verb '{verb}'. Expected one of: {string.Join(", ", KnownVerbs)}.");
            return ExitCodes.OperationFailed;
        }

        using var scope = services.CreateScope();

        try
        {
            return verb switch
            {
                "migrate" => await MigrateAsync(scope.ServiceProvider, args, cancellationToken)
                    .ConfigureAwait(false),
                "state" => await StateAsync(scope.ServiceProvider, args, cancellationToken)
                    .ConfigureAwait(false),
                "seed" => await SeedAsync(scope.ServiceProvider, args, cancellationToken)
                    .ConfigureAwait(false),
                "apply-procedures" => await ApplyProceduresAsync(
                    scope.ServiceProvider, cancellationToken).ConfigureAwait(false),
                "verify-seed" => await VerifySeedAsync(scope.ServiceProvider, args, cancellationToken)
                    .ConfigureAwait(false),
                "mint-token" => MintToken(scope.ServiceProvider),
                _ => NotYetImplemented(verb),
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured", StringComparison.Ordinal))
        {
            // Thrown by AddLexTimeInfrastructure and AddLexTimeAuthentication when a
            // required setting is absent. Distinct from a connectivity failure, and the
            // script branches on the difference.
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.ConfigurationError;
        }
        catch (DbException ex)
        {
            Console.Error.WriteLine($"The database could not be reached: {ex.Message}");
            return ExitCodes.DatabaseUnreachable;
        }
    }

    /// <summary>
    /// Reads the seed volumes to use, applying the <c>--entries</c> override when present.
    /// </summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>Options with the requested entry count.</returns>
    /// <exception cref="FormatException">The value after <c>--entries</c> is not a number.</exception>
    internal static SeedOptions ResolveOptions(string[] args)
    {
        var options = new SeedOptions();

        var index = Array.IndexOf(args, "--entries");
        if (index >= 0 && index + 1 < args.Length)
        {
            options = options with
            {
                TimeEntryCount = int.Parse(args[index + 1], CultureInfo.InvariantCulture),
            };
        }

        return options;
    }

    /// <summary>Applies migrations, optionally dropping the database first.</summary>
    /// <param name="scoped">Scoped service provider.</param>
    /// <param name="args">Process arguments; <c>--reset</c> is honoured here.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A process exit code.</returns>
    private static async Task<int> MigrateAsync(
        IServiceProvider scoped,
        string[] args,
        CancellationToken cancellationToken)
    {
        var runner = scoped.GetRequiredService<MigrationRunner>();

        if (args.Contains("--reset", StringComparer.Ordinal))
        {
            var rebuilt = await runner.ResetAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"database dropped and rebuilt, {rebuilt} migrations applied");
            return ExitCodes.Success;
        }

        var applied = await runner.MigrateAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine(applied == 0
            ? "up to date, 0 applied"
            : $"{applied} migrations applied");

        return ExitCodes.Success;
    }

    /// <summary>
    /// Reports the database state and exits 0 in every case — it reports, it does not
    /// judge. Judging belongs to the script, which is the only thing that knows whether a
    /// reset was requested.
    /// </summary>
    /// <param name="scoped">Scoped service provider.</param>
    /// <param name="args">Process arguments; <c>--entries</c> is honoured here.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Always <see cref="ExitCodes.Success"/>.</returns>
    private static async Task<int> StateAsync(
        IServiceProvider scoped,
        string[] args,
        CancellationToken cancellationToken)
    {
        var inspector = scoped.GetRequiredService<DatabaseStateInspector>();
        var report = await inspector
            .InspectAsync(ResolveOptions(args), cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(report.State.ToString());
        Console.WriteLine(
            $"users={report.Users} clients={report.Clients} " +
            $"matters={report.Matters} entries={report.TimeEntries}");

        return ExitCodes.Success;
    }

    /// <summary>
    /// Generates and loads the dataset, refusing when the database already holds data.
    /// </summary>
    /// <remarks>
    /// Refusing rather than topping up (FR-003) is what keeps seeding from being quietly
    /// additive. The host reports; the script decides whether a reset is warranted, because
    /// only the script knows whether the caller asked for one.
    /// </remarks>
    /// <param name="scoped">Scoped service provider.</param>
    /// <param name="args">Process arguments; <c>--entries</c> is honoured here.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A process exit code.</returns>
    private static async Task<int> SeedAsync(
        IServiceProvider scoped,
        string[] args,
        CancellationToken cancellationToken)
    {
        var options = ResolveOptions(args);
        var inspector = scoped.GetRequiredService<DatabaseStateInspector>();
        var report = await inspector.InspectAsync(options, cancellationToken).ConfigureAwait(false);

        if (report.State != SeedState.Empty)
        {
            Console.Error.WriteLine(
                $"The database is not empty ({report.State.ToString().ToLowerInvariant()}: " +
                $"{report.TimeEntries} entries). Re-run with a reset to rebuild it.");
            return ExitCodes.DatabaseNotEmpty;
        }

        var seeder = scoped.GetRequiredService<BulkSeeder>();
        var loaded = await seeder.SeedAsync(options, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"{loaded} entries loaded");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Measures the seeded data against its declared bands and reports each result.
    /// </summary>
    /// <remarks>
    /// A band miss is a non-zero exit, not a warning. A seed that quietly falls outside its
    /// own stated shape is worse than one that fails, because feature 003 will report on it
    /// as though it were sound.
    /// </remarks>
    /// <param name="scoped">Scoped service provider.</param>
    /// <param name="args">Process arguments; <c>--entries</c> is honoured here.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A process exit code.</returns>
    private static async Task<int> VerifySeedAsync(
        IServiceProvider scoped,
        string[] args,
        CancellationToken cancellationToken)
    {
        var verifier = scoped.GetRequiredService<SeedVerifier>();
        var checks = await verifier
            .VerifyAsync(ResolveOptions(args), cancellationToken)
            .ConfigureAwait(false);

        foreach (var check in checks)
        {
            var label = (check.Name + " ").PadRight(30, '.');
            var measured = check.Measured.ToString("0.##", CultureInfo.InvariantCulture);

            Console.WriteLine(
                $"{label} {measured,-8} (band {check.Band,-8}) {(check.Passed ? "ok" : "FAILED")}");
        }

        var passed = checks.Count(c => c.Passed);
        Console.WriteLine($"{passed}/{checks.Count} checks passed");

        return passed == checks.Count ? ExitCodes.Success : ExitCodes.VerificationFailed;
    }

    /// <summary>Applies every source-controlled stored procedure.</summary>
    /// <param name="scoped">Scoped service provider.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A process exit code.</returns>
    private static async Task<int> ApplyProceduresAsync(
        IServiceProvider scoped,
        CancellationToken cancellationToken)
    {
        var applier = scoped.GetRequiredService<ProcedureApplier>();
        var applied = await applier
            .ApplyAllAsync(FindRepositoryRoot(), cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(applied == 0 ? "no procedures to apply" : $"{applied} applied");
        return ExitCodes.Success;
    }

    /// <summary>Prints a development bearer token.</summary>
    /// <param name="scoped">Scoped service provider.</param>
    /// <returns>A process exit code.</returns>
    private static int MintToken(IServiceProvider scoped)
    {
        var minter = scoped.GetRequiredService<DevelopmentTokenMinter>();
        Console.WriteLine(minter.Mint(TimeSpan.FromDays(7), DateTime.UtcNow));
        return ExitCodes.Success;
    }

    /// <summary>
    /// Walks upwards from the running assembly until it finds the repository root.
    /// </summary>
    /// <remarks>
    /// The procedure directory is a repository path, and the process runs from
    /// <c>bin/Debug/net9.0</c>. Anchored on <c>LexTime.sln</c> rather than a fixed number of
    /// parent hops, so it does not silently break when the output path changes.
    /// </remarks>
    /// <returns>The repository root, or the current directory if no marker was found.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LexTime.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>Placeholder for verbs whose tasks have not landed yet.</summary>
    /// <param name="verb">The verb requested.</param>
    /// <returns><see cref="ExitCodes.OperationFailed"/>.</returns>
    private static int NotYetImplemented(string verb)
    {
        Console.Error.WriteLine($"Verb '{verb}' is not implemented yet.");
        return ExitCodes.OperationFailed;
    }
}
