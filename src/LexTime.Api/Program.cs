using LexTime.Api.Authentication;
using LexTime.Api.HealthChecks;
using LexTime.Application;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// One registration extension per layer, so this file reads as a table of contents
// rather than a wall (constitution P21).
builder.Services.AddLexTimeApplication();
builder.Services.AddLexTimeInfrastructure(builder.Configuration);
builder.Services.AddLexTimeAuthentication(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

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

// Temporary. Exists only so the auth boundary can be shown to both reject and accept:
// a boundary that only ever returns 401 is indistinguishable from a broken service.
// Removed when the first real endpoint lands, and not one of the seventeen in
// docs/prd.md §4 (see research.md R5).
app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "authenticated" }));

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point. Declared explicitly so the integration tests can host the application
/// through <c>WebApplicationFactory</c>; top-level statements otherwise generate an
/// internal class the test project cannot name.
/// </summary>
public partial class Program;
