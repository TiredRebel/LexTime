namespace LexTime.IntegrationTests;

/// <summary>
/// Groups every test class that needs the database so they share one container.
/// </summary>
/// <remarks>
/// Without this, xUnit would start and migrate a fresh SQL Server container per test class,
/// which costs tens of seconds each. Sharing means test classes must not depend on the
/// database being empty — each one inserts the rows it needs with values distinct enough
/// not to collide with another class's.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    /// <summary>The collection name test classes reference in their <c>[Collection]</c> attribute.</summary>
    public const string Name = "SqlServer";
}
