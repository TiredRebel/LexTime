using LexTime.Application.Parties;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LexTime.Api.Endpoints;

/// <summary>Registers the read-only timekeeper routes.</summary>
public static class TimekeeperEndpoints
{
    /// <summary>Base route for timekeepers.</summary>
    public const string BaseRoute = "/api/v1/users";

    /// <summary>Maps list and get routes only; no write route exists.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IEndpointRouteBuilder MapTimekeeperEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup(BaseRoute).WithTags("Timekeepers");
        group.MapGet("/", ListAsync).WithSummary("Lists seeded timekeepers.");
        group.MapGet("/{userId:int}", GetAsync).WithSummary("Fetches one seeded timekeeper.");
        return app;
    }

    /// <summary>Lists timekeepers.</summary>
    /// <param name="skip">Requested offset.</param>
    /// <param name="take">Requested page size.</param>
    /// <param name="handler">Timekeeper list use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A bounded page.</returns>
    private static async Task<Ok<TimekeeperPage>> ListAsync(
        int? skip,
        int? take,
        ListTimekeepersHandler handler,
        CancellationToken cancellationToken)
    {
        var page = await handler.HandleAsync(
            new ListTimekeepersQuery(skip ?? 0, take ?? 0), cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(page);
    }

    /// <summary>Fetches a timekeeper.</summary>
    /// <param name="userId">Timekeeper identifier.</param>
    /// <param name="handler">Timekeeper read use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>200 or 404.</returns>
    private static async Task<IResult> GetAsync(
        int userId,
        GetTimekeeperHandler handler,
        CancellationToken cancellationToken)
    {
        var timekeeper = await handler.HandleAsync(userId, cancellationToken).ConfigureAwait(false);
        return timekeeper is null ? TypedResults.NotFound() : TypedResults.Ok(timekeeper);
    }
}
