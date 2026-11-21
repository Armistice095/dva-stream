using DvaStream.Infrastructure.Processes;

namespace DvaStream.Infrastructure.Tests.Processes;

/// <summary>Lance le petit exécutable <c>DvaStream.TestProcess</c> avec l'hôte <c>dotnet</c> (même commande sur les 3 OS).</summary>
internal static class TestProcessHost
{
    private static string DotnetHost =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";

    private static string HelperDll => Path.Combine(AppContext.BaseDirectory, "DvaStream.TestProcess.dll");

    public static ProcessSpec Spec(string mode, params string[] arguments) => SpecWith(null, null, mode, arguments);

    public static ProcessSpec SpecWith(TimeSpan? timeout, string? workingDirectory, string mode, params string[] arguments)
    {
        Assert.True(File.Exists(HelperDll), $"Exécutable de test introuvable : {HelperDll}");
        return new ProcessSpec(DotnetHost, [HelperDll, mode, .. arguments], timeout, workingDirectory);
    }
}
