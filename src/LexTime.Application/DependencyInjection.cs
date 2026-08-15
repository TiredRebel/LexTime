using LexTime.Application.Reporting;
using LexTime.Application.Parties;
using LexTime.Application.TimeEntries;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.Application;

/// <summary>
/// Registration entry point for the application layer. One extension method per layer, so
/// that composition in <c>Program.cs</c> reads as a table of contents (constitution P21).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer's use cases.
    /// </summary>
    /// <remarks>
    /// One handler class per use case (constitution P4), registered here and injected into the
    /// endpoint that invokes it. Handlers are scoped because the interfaces they depend on are
    /// bound to per-request resources.
    /// <para>
    /// This method registered nothing through features 001 and 002 — the project existed
    /// because P4 requires the layering unconditionally, and so that <c>Program.cs</c> would
    /// not change shape when the first use case arrived. It has arrived.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddLexTimeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<GetWeeklyBillableRollupHandler>();

        // The time-entry use cases. Five endpoints, five handlers — P4 makes that one-to-one
        // rather than a service class with five methods, so a reader can find a use case by name.
        services.AddScoped<RecordTimeEntryHandler>();
        services.AddScoped<ReviseTimeEntryHandler>();
        services.AddScoped<DeleteTimeEntryHandler>();
        services.AddScoped<GetTimeEntryHandler>();
        services.AddScoped<ListTimeEntriesHandler>();

        services.AddScoped<RegisterClientHandler>();
        services.AddScoped<GetClientHandler>();
        services.AddScoped<ReviseClientHandler>();
        services.AddScoped<ListClientsHandler>();
        services.AddScoped<OpenMatterHandler>();
        services.AddScoped<GetMatterHandler>();
        services.AddScoped<ReviseMatterHandler>();
        services.AddScoped<ListMattersHandler>();
        services.AddScoped<GetTimekeeperHandler>();
        services.AddScoped<ListTimekeepersHandler>();

        return services;
    }
}
