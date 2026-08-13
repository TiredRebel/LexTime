using Microsoft.Extensions.DependencyInjection;

namespace LexTime.Application;

/// <summary>
/// Registration entry point for the application layer. One extension method per layer, so
/// that composition in <c>Program.cs</c> reads as a table of contents (constitution P21).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer's services.
    /// </summary>
    /// <remarks>
    /// Registers nothing today. This layer holds one handler class per use case, and
    /// feature 001 has no use cases — its endpoints are a health check and a placeholder.
    /// The project exists now because constitution P4 requires the four-project layering
    /// unconditionally, and the reporting interface lands here with feature 003. The
    /// method exists so that <c>Program.cs</c> does not change shape when it does.
    /// </remarks>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddLexTimeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
