using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexTime.Api.HealthChecks;

/// <summary>
/// Writes the health response body defined in
/// <c>specs/001-solution-and-schema/contracts/health.md</c>.
/// </summary>
/// <remarks>
/// The framework's default writer emits the overall status as a bare string. FR-025 requires
/// each check to be named individually with its own status and duration, so that a caller
/// can tell which component failed without access to logs.
/// </remarks>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Serialises the report to the response body as JSON.
    /// </summary>
    /// <param name="httpContext">The response being written to.</param>
    /// <param name="report">The result of running every registered check.</param>
    /// <returns>A task that completes once the body has been written.</returns>
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(report);

        httpContext.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                description = entry.Value.Description,
            }),
        };

        return httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
