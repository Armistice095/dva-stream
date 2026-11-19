using System.Reflection;

namespace DvaStream.Infrastructure.Tests;

/// <summary>Règles de dépendances du TDD §3, vérifiées sur les assemblies compilées (M0-07).</summary>
public class ArchitectureTests
{
    private static IReadOnlyList<string> ReferencedAssemblyNames(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

    private static bool IsBaseClassLibrary(string name) =>
        name is "mscorlib" or "netstandard" || name.StartsWith("System", StringComparison.Ordinal);

    [Fact]
    public void Core_ne_depend_que_de_la_bibliotheque_de_base()
    {
        var illegal = ReferencedAssemblyNames("DvaStream.Core")
            .Where(name => !IsBaseClassLibrary(name))
            .ToList();

        Assert.True(illegal.Count == 0, "Core référence : " + string.Join(", ", illegal));
    }

    [Fact]
    public void Infrastructure_ne_depend_ni_de_App_ni_d_Avalonia()
    {
        var illegal = ReferencedAssemblyNames("DvaStream.Infrastructure")
            .Where(name => name.StartsWith("DvaStream.App", StringComparison.Ordinal)
                        || name.StartsWith("Avalonia", StringComparison.Ordinal))
            .ToList();

        Assert.True(illegal.Count == 0, "Infrastructure référence : " + string.Join(", ", illegal));
    }

    [Fact]
    public void Core_ne_depend_pas_d_Infrastructure()
    {
        var names = ReferencedAssemblyNames("DvaStream.Core");

        Assert.DoesNotContain("DvaStream.Infrastructure", names);
    }
}
