using LexTime.Application.TimeEntries;
using LexTime.Domain.Rules;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LexTime.Api.Endpoints;

/// <summary>
/// Registers the five time-entry routes.
/// </summary>
/// <remarks>
/// One registration extension per group of routes (constitution P21). None carries
/// <c>AllowAnonymous</c>, so all five inherit the fallback-closed policy from feature 001.
/// <para>
/// <b>No rule is expressed in this file.</b> The endpoints validate shape, invoke a handler and
/// map an outcome to a status code; every limit and every threshold lives in
/// <see cref="TimeEntryRuleSet"/>. A number appearing here would be a second copy of a rule.
/// </para>
/// </remarks>
public static class TimeEntryEndpoints
{
    /// <summary>Base route for the collection.</summary>
    public const string BaseRoute = "/api/v1/time-entries";

    /// <summary>Maps the five routes.</summary>
    /// <param name="app">The route builder to register on.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IEndpointRouteBuilder MapTimeEntryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(BaseRoute).WithTags("Time entries");

        group.MapGet("/", ListAsync)
            .WithSummary("Lists time entries, filtered and paged.");

        group.MapGet("/{id:long}", GetAsync)
            .WithName("GetTimeEntry")
            .WithSummary("Fetches one time entry.");

        group.MapPost("/", RecordAsync)
            .WithSummary("Records new time. Every domain rule applies.");

        group.MapPut("/{id:long}", ReviseAsync)
            .WithSummary("Corrects an existing entry. The backdating and active-matter rules apply only to changed fields.");

        group.MapDelete("/{id:long}", DeleteAsync)
            .WithSummary("Removes an entry outright.");

        return app;
    }

    /// <summary>Lists entries matching the filters.</summary>
    /// <param name="userId">Restrict to one timekeeper.</param>
    /// <param name="matterId">Restrict to one matter.</param>
    /// <param name="from">Inclusive lower bound on work date.</param>
    /// <param name="to">Inclusive upper bound on work date.</param>
    /// <param name="skip">Entries to pass over; negative is treated as zero.</param>
    /// <param name="take">Page size; bounded rather than honoured literally.</param>
    /// <param name="handler">The use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One page, with the total matching the filters.</returns>
    private static async Task<Ok<TimeEntryPage>> ListAsync(
        int? userId,
        int? matterId,
        DateOnly? from,
        DateOnly? to,
        int? skip,
        int? take,
        ListTimeEntriesHandler handler,
        CancellationToken cancellationToken)
    {
        var page = await handler
            .HandleAsync(
                new ListTimeEntriesQuery(userId, matterId, from, to, skip ?? 0, take ?? 0),
                cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(page);
    }

    /// <summary>Fetches one entry.</summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="handler">The use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry, or 404.</returns>
    private static async Task<Results<Ok<TimeEntryDto>, NotFound>> GetAsync(
        long id,
        GetTimeEntryHandler handler,
        CancellationToken cancellationToken)
    {
        var entry = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);

        return entry is null ? TypedResults.NotFound() : TypedResults.Ok(entry);
    }

    /// <summary>Records new time.</summary>
    /// <param name="command">What to record. Carries no rate — that is captured, not supplied.</param>
    /// <param name="handler">The use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>201 with the recorded entry, 400 with the rules that refused it, or 404.</returns>
    private static async Task<Results<Created<TimeEntryDto>, ProblemHttpResult, NotFound>> RecordAsync(
        RecordTimeEntryCommand command,
        RecordTimeEntryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            WriteOutcome.Succeeded => TypedResults.Created(
                $"{BaseRoute}/{result.Entry!.TimeEntryId}", result.Entry),
            WriteOutcome.RuleViolation => RuleViolationResults.Problem(result.Violations),
            _ => TypedResults.NotFound(),
        };
    }

    /// <summary>Corrects an existing entry.</summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="command">The revised values. Carries no timekeeper and no rate.</param>
    /// <param name="handler">The use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>200 with the revised entry, 400 with the rules that refused it, or 404.</returns>
    private static async Task<Results<Ok<TimeEntryDto>, ProblemHttpResult, NotFound>> ReviseAsync(
        long id,
        ReviseTimeEntryCommand command,
        ReviseTimeEntryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, command, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            WriteOutcome.Succeeded => TypedResults.Ok(result.Entry!),
            WriteOutcome.RuleViolation => RuleViolationResults.Problem(result.Violations),
            _ => TypedResults.NotFound(),
        };
    }

    /// <summary>Removes an entry.</summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="handler">The use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>204, or 404 when there is nothing there.</returns>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        long id,
        DeleteTimeEntryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Outcome == WriteOutcome.Succeeded
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}
