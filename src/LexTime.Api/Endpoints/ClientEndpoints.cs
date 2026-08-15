using LexTime.Application.Parties;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LexTime.Api.Endpoints;

/// <summary>Registers client creation, revision and read routes.</summary>
public static class ClientEndpoints
{
    /// <summary>Base route for clients.</summary>
    public const string BaseRoute = "/api/v1/clients";

    /// <summary>Maps the four client routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup(BaseRoute).WithTags("Clients");
        group.MapGet("/", ListAsync).WithSummary("Lists clients, optionally filtered by active state.");
        group.MapPost("/", RegisterAsync).WithSummary("Registers an active client.");
        group.MapGet("/{clientId:int}", GetAsync).WithSummary("Fetches one client.");
        group.MapPut("/{clientId:int}", ReviseAsync).WithSummary("Revises a client name or active state.");
        return app;
    }

    /// <summary>Lists clients.</summary>
    /// <param name="isActive">Optional active-state filter.</param>
    /// <param name="skip">Requested offset.</param>
    /// <param name="take">Requested page size.</param>
    /// <param name="handler">Client list use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A bounded page.</returns>
    private static async Task<Ok<ClientPage>> ListAsync(
        bool? isActive,
        int? skip,
        int? take,
        ListClientsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = await handler.HandleAsync(
            new ListClientsQuery(isActive, skip ?? 0, take ?? 0), cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(page);
    }

    /// <summary>Registers a client after validating required text.</summary>
    /// <param name="command">Client code and name.</param>
    /// <param name="handler">Client registration use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>201, 409 or 400.</returns>
    private static async Task<IResult> RegisterAsync(
        RegisterClientCommand command,
        RegisterClientHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ClientCode) || string.IsNullOrWhiteSpace(command.Name))
        {
            return TypedResults.BadRequest("Client code and name must not be empty.");
        }

        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == PartyWriteOutcome.Conflict)
        {
            return TypedResults.Problem(
                title: "Client code already in use",
                detail: $"A client with code '{result.Conflict!.Value}' already exists.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["conflictingField"] = result.Conflict.Field,
                    ["conflictingValue"] = result.Conflict.Value,
                });
        }

        var client = result.Client!;
        return TypedResults.Created($"{BaseRoute}/{client.ClientId}", client);
    }

    /// <summary>Fetches a client.</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="handler">Client read use case.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>200 or 404.</returns>
    private static async Task<IResult> GetAsync(
        int clientId,
        GetClientHandler handler,
        CancellationToken cancellationToken)
    {
        var client = await handler.HandleAsync(clientId, cancellationToken).ConfigureAwait(false);
        return client is null ? TypedResults.NotFound() : TypedResults.Ok(client);
    }

    /// <summary>Revises a client.</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="command">Mutable replacement values.</param>
    /// <param name="handler">Client revision use case.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>200, 404 or 400.</returns>
    private static async Task<IResult> ReviseAsync(
        int clientId,
        ReviseClientCommand command,
        ReviseClientHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return TypedResults.BadRequest("Client name must not be empty.");
        }

        var result = await handler.HandleAsync(clientId, command, cancellationToken).ConfigureAwait(false);
        return result.Outcome == PartyWriteOutcome.NotFound
            ? TypedResults.NotFound()
            : TypedResults.Ok(result.Client!);
    }
}
