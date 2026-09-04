using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;

namespace ColourBricks.UnitTests.Architecture;

/// <summary>
/// Enforces the backend layering rule in plan.md §4:
/// "Domain references nothing. No EF Core types in Domain or Api."
/// </summary>
public class ArchitectureTests
{
    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Pomelo.EntityFrameworkCore",
        "ColourBricks.Infrastructure",
        "ColourBricks.Application",
        "ColourBricks.Api",
    ];

    [Fact]
    public void Architecture_DomainHasNoInfrastructureReferences()
    {
        Assembly domain = typeof(ColourBricks.Domain.AssemblyMarker).Assembly;

        string[] referenced = domain
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (string forbidden in ForbiddenInDomain)
        {
            referenced
                .Should()
                .NotContain(
                    name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase),
                    "Domain must not reference {0} (plan.md §4)",
                    forbidden);
        }
    }
}
