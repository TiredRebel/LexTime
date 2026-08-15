using LexTime.Application.Parties;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LexTime.Api.Endpoints;

/// <summary>Registers matter creation, revision and read routes.</summary>
public static class MatterEndpoints
{
    /// <summary>Base route for matters.</summary>
    public const string BaseRoute = "/api/v1/matters";

    /// <summary>Maps the four matter routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IEndpointRouteBuilder MapMatterEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup(BaseRoute).WithTags("Matters");
        group.MapGet("/{matterId:int}", GetAsync).WithSummary("Fetches one matter.");
        group.MapPut("/{matterId:int}", ReviseAsync).WithSummary("Revises a matter or its active state.");
        app.MapGet("/api/v1/clients/{clientId:int}/matters", ListAsync)
            .WithTags("Matters")
            .WithSummary("Lists matters under a client.");
        app.MapPost("/api/v1/clients/{clientId:int}/matters", OpenAsync)
            .WithTags("Matters")
            .WithSummary("Opens an active matter.");
        return app;
    }

    /// <summary>Lists matters for a client.</summary>
    /// <param name="clientId">Owning client identifier.</param>
    /// <param name="skip">Requested offset.</param>
    /// <param name="take">Requested page size.</param>
    /// <param name="handler">Matter list use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A page or 404 when the client is missing.</returns>
    private static async Task<IResult> ListAsync(
        int clientId,
        int? skip,
        int? take,
        ListMattersHandler handler,
        CancellationToken cancellationToken)
    {
        var page = await handler.HandleAsync(
            new ListMattersQuery(clientId, skip ?? 0, take ?? 0), cancellationToken).ConfigureAwait(false);
        return page is null ? TypedResults.NotFound() : TypedResults.Ok(page);
    }

    /// <summary>Opens a matter after validating required text.</summary>
    /// <param name="clientId">Owning client identifier.</param>
    /// <param name="command">Matter values.</param>
    /// <param name="handler">Matter opening use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>201, 404, 409 or 400.</returns>
    private static async Task<IResult> OpenAsync(
        int clientId,
        OpenMatterCommand command,
        OpenMatterHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.MatterNumber) || string.IsNullOrWhiteSpace(command.Name))
        {
            return TypedResults.BadRequest("Matter number and name must not be empty.");
        }

        var result = await handler.HandleAsync(clientId, command, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == PartyWriteOutcome.NotFound)
        {
            return TypedResults.NotFound();
        }

        if (result.Outcome == PartyWriteOutcome.Conflict)
        {
            return TypedResults.Problem(
                title: "Matter number already in use for this client",
                detail: $"Client {clientId} already has a matter numbered '{result.Conflict!.Value}'.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["conflictingField"] = result.Conflict.Field,
                    ["conflictingValue"] = result.Conflict.Value,
                });
        }

        var matter = result.Matter!;
        return TypedResults.Created($"{BaseRoute}/{matter.MatterId}", matter);
    }

    /// <summary>Fetches a matter.</summary>
    /// <param name="matterId">Matter identifier.</param>
    /// <param name="handler">Matter read use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>200 or 404.</returns>
    private static async Task<IResult> GetAsync(
        int matterId,
        GetMatterHandler handler,
        CancellationToken cancellationToken)
    {
        var matter = await handler.HandleAsync(matterId, cancellationToken).ConfigureAwait(false);
        return matter is null ? TypedResults.NotFound() : TypedResults.Ok(matter);
    }

    /// <summary>Revises a matter.</summary>
    /// <param name="matterId">Matter identifier.</param>
    /// <param name="command">Mutable replacement values.</param>
    /// <param name="handler">Matter revision use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>200, 404 or 400.</returns>
    private static async Task<IResult> ReviseAsync(
        int matterId,
        ReviseMatterCommand command,
        ReviseMatterHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return TypedResults.BadRequest("Matter name must not be empty.");
        }

        var result = await handler.HandleAsync(matterId, command, cancellationToken).ConfigureAwait(false);
        return result.Outcome == PartyWriteOutcome.NotFound
            ? TypedResults.NotFound()
            : TypedResults.Ok(result.Matter!);
    }
}
