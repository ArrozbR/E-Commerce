namespace KeycapStore.UnitTests.Architecture;

public class DomainDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "KeycapStore.",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Stripe",
        "System.ComponentModel.Annotations",
    ];

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructureOrFrameworks()
    {
        var domainAssembly = typeof(KeycapStore.Domain.AssemblyReference).Assembly;

        var referencedNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? "")
            .ToList();

        var violations = referencedNames
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"O Domain usa DLLs proibidas: {string.Join(", ", violations)}");
    }
}
