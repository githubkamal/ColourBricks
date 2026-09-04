using FluentAssertions;

namespace ColourBricks.IntegrationTests;

/// <summary>
/// Placeholder until P0-T09 provides the Testcontainers + Respawn integration harness.
/// Keeps the project compiling and part of the test run.
/// </summary>
public class ScaffoldingSmokeTests
{
    [Fact]
    public void ApiAssembly_IsReferenced()
    {
        typeof(Program).Assembly.GetName().Name.Should().Be("ColourBricks.Api");
    }
}
