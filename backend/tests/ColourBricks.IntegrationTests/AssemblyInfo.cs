using Xunit;

// Every integration test shares one local database, reset by Respawn before each
// test (see IntegrationFixture). They run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
