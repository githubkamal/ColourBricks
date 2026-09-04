namespace ColourBricks.IntegrationTests.Support;

/// <summary>
/// Base for every integration test: shares the assembly-wide factory and resets the
/// database before each test (plan.md P0-T09). All derived classes join the
/// <c>Integration</c> collection, so they run serially against the one test database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase(IntegrationFixture fixture) : IAsyncLifetime
{
    protected IntegrationFixture Fixture => fixture;

    protected ColourBricksApiFactory Factory => fixture.Factory;

    public Task InitializeAsync() => fixture.ResetAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
