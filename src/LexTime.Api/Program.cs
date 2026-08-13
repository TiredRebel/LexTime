using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Api.HealthChecks;
using LexTime.Api.Maintenance;
using LexTime.Application;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// A maintenance run's output is read by a human and parsed by no one, but EF Core logs
// every statement it executes at Information. Left alone, one `state` call buries its
// two lines of answer under a page of SQL and the script's step output stops being
// readable. The web host keeps its normal logging.
// ClearProviders rather than SetMinimumLevel: the level configured in
// appsettings.Development.json wins over a minimum set in code, so raising the minimum
// here changes nothing. The verbs report through Console directly, and failures are
// written to stderr by their handlers, so nothing is lost by silencing the providers.
if (MaintenanceCommands.IsMaintenanceInvocation(args))
{
    builder.Logging.ClearProviders();
}

// One registration extension per layer, so this file reads as a table of contents
// rather than a wall (constitution P21).
//
// Both AddLexTime* extensions validate their configuration at registration time, which is
// earlier than the maintenance verbs can catch. Without this guard a missing connection
// string kills the process with a stack trace and an unmapped exit code, and the script
// cannot tell a misconfiguration from an unreachable database. The web path is left to
// fail loudly, because a misconfigured server should not start quietly.
try
{
    builder.Services.AddLexTimeApplication();
    builder.Services.AddLexTimeInfrastructure(builder.Configuration);
    builder.Services.AddLexTimeAuthentication(builder.Configuration);
}
catch (InvalidOperationException ex) when (MaintenanceCommands.IsMaintenanceInvocation(args))
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = ExitCodes.ConfigurationError;
    return;
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Maintenance verbs run and exit without starting the web host. The no-argument path below
// is deliberately untouched: every integration test hosts this file through
// WebApplicationFactory with no arguments, and they are the regression check for this
// branch. Contract: specs/002-bootstrap-and-seed/contracts/host-cli.md.
//
// The exit code goes through Environment.ExitCode rather than being returned. A `return
// <int>` anywhere in top-level statements makes the generated Main return Task<int>, which
// WebApplicationFactory's entry-point interception does not support — it breaks all nine
// tests that host this file, with "no web application was configured".
if (MaintenanceCommands.IsMaintenanceInvocation(args))
{
    Environment.ExitCode = await MaintenanceCommands.RunAsync(app.Services, args)
        .ConfigureAwait(false);
    return;
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Unauthenticated by design: the health check and the API documentation are the two
// exceptions to the bearer-token requirement (FR-019). Everything else inherits the
// fallback policy set in AddLexTimeAuthentication and is closed unless it says otherwise.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync,

    // A degraded system must never answer 200. That shape is what makes an automated
    // probe report a dead service as healthy (FR-024).
    ResultStatusCodes = new Dictionary<HealthStatus, int>
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
}).AllowAnonymous();

// The reporting surface. Registered through an extension method so this file names what it
// composes rather than showing how (constitution P21). Everything here is closed by the
// fallback policy above; only the two routes marked AllowAnonymous are not.
//
// This replaces the /api/v1/ping placeholder feature 001 added, which existed only so the
// access boundary could be shown to accept as well as reject. The boundary tests now aim at
// this route instead, which makes them stronger: they guard something that returns data.
app.MapReportEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point. Declared explicitly so the integration tests can host the application
/// through <c>WebApplicationFactory</c>; top-level statements otherwise generate an
/// internal class the test project cannot name.
/// </summary>
public partial class Program;
