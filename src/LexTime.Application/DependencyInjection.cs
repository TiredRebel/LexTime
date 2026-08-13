using LexTime.Application.Reporting;
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

        return services;
    }
}
