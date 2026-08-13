using LexTime.Application.Reporting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LexTime.Api.Endpoints;

/// <summary>
/// Registers the reporting routes.
/// </summary>
/// <remarks>
/// One registration extension per group of routes, so <c>Program.cs</c> stays a table of
/// contents rather than a wall of lambdas (constitution P21).
/// </remarks>
public static class ReportEndpoints
{
    /// <summary>Route the rollup is served from.</summary>
    public const string WeeklyBillableRollupRoute = "/api/v1/reports/weekly-billable-rollup";

    /// <summary>
    /// Maps the weekly billable rollup.
    /// </summary>
    /// <remarks>
    /// No <c>AllowAnonymous</c>, deliberately: the route inherits the fallback-closed
    /// authorization policy established in feature 001, which makes health and the API
    /// documentation the only open endpoints.
    /// </remarks>
    /// <param name="app">The route builder to register on.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(WeeklyBillableRollupRoute, HandleWeeklyBillableRollupAsync)
            .WithName("GetWeeklyBillableRollup")
            .WithSummary("Billable hours, amounts, running totals and client standings by ISO week.")
            .Produces<WeeklyBillableRollupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>
    /// Validates the requested range and returns the rollup over it.
    /// </summary>
    /// <remarks>
    /// The endpoint validates and delegates; it holds no reporting logic and touches no
    /// database (constitution P4). The procedure itself does not reject an inverted range —
    /// there it simply matches nothing, which is coherent but tells a caller nothing. The
    /// actionable refusal belongs at the boundary, which is here.
    /// </remarks>
    /// <param name="from">
    /// Inclusive first billing date. Required; the report assumes no default range, because a
    /// report that quietly picks its own answers a question nobody asked.
    /// </param>
    /// <param name="to">Inclusive last billing date. Required for the same reason.</param>
    /// <param name="clientId">
    /// Optional single-client filter. Narrows the rows returned and changes no figure inside
    /// one — the standing in particular stays the client's position among all clients.
    /// </param>
    /// <param name="handler">The use case, injected from the application layer.</param>
    /// <param name="cancellationToken">Cancels the read when the caller disconnects.</param>
    /// <returns>
    /// The rollup with 200, or a problem response with 400 when the range is missing or
    /// inverted. A range that simply matches nothing is a 200 with no rows, not an error.
    /// </returns>
    private static async Task<Results<Ok<WeeklyBillableRollupResponse>, ProblemHttpResult>>
        HandleWeeklyBillableRollupAsync(
            DateOnly? from,
            DateOnly? to,
            int? clientId,
            GetWeeklyBillableRollupHandler handler,
            CancellationToken cancellationToken)
    {
        if (from is not { } fromDate || to is not { } toDate)
        {
            return TypedResults.Problem(
                title: "Incomplete reporting range",
                detail: "Both 'from' and 'to' are required. The report has no default range.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (fromDate > toDate)
        {
            // Both values are named, so the caller can see which one to change without
            // re-reading their own request.
            return TypedResults.Problem(
                title: "Invalid reporting range",
                detail: $"'from' ({fromDate:yyyy-MM-dd}) must not be later than 'to' ({toDate:yyyy-MM-dd}).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var response = await handler
            .HandleAsync(new WeeklyBillableRollupQuery(fromDate, toDate, clientId), cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(response);
    }
}
