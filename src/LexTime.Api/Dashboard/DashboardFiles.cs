namespace LexTime.Api.Dashboard;

/// <summary>
/// Adds the committed billing-dashboard export to the API host.
/// </summary>
/// <remarks>
/// The editable Next.js source lives under <c>web/</c>. Its committed static export lives
/// under <c>wwwroot</c> so the reviewer quickstart remains the existing two commands and
/// does not acquire a Node.js prerequisite.
/// </remarks>
public static class DashboardFiles
{
    /// <summary>
    /// Serves the dashboard's default document and static assets before authorization handles
    /// the protected API routes.
    /// </summary>
    /// <param name="app">The composed web application whose content root contains the export.</param>
    /// <returns>The same application so composition calls remain chainable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static WebApplication MapDashboardFiles(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseDefaultFiles();
        app.UseStaticFiles();

        return app;
    }
}
