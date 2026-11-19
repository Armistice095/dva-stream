namespace DvaStream.Core.Platform;

/// <summary>Dossiers de l'application pour un utilisateur donné (TDD §11).</summary>
/// <param name="ConfigDirectory">Paramètres (settings.json).</param>
/// <param name="DataDirectory">Données : moteur, temporaires, journaux.</param>
public sealed record AppDirectories(string ConfigDirectory, string DataDirectory)
{
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string EngineDirectory => Path.Combine(DataDirectory, "engine");

    public string TempDirectory => Path.Combine(DataDirectory, "temp");
}
