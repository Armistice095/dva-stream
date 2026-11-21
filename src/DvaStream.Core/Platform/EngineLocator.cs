namespace DvaStream.Core.Platform;

/// <summary>
/// Emplacement du binaire du moteur (yt-dlp) : <c>&lt;DataDirectory&gt;/engine/yt-dlp[.exe]</c>, hors du dossier
/// d'installation pour pouvoir être mis à jour sans droits administrateur (TDD §6.4, M2-03). Fonction pure.
/// </summary>
public static class EngineLocator
{
    /// <summary>Nom du fichier exécutable selon l'OS.</summary>
    public static string GetExecutableName(HostOs os) => os == HostOs.Windows ? "yt-dlp.exe" : "yt-dlp";

    /// <summary>Chemin complet attendu du binaire du moteur.</summary>
    public static string GetExecutablePath(AppDirectories directories, HostOs os)
    {
        ArgumentNullException.ThrowIfNull(directories);
        return Path.Combine(directories.EngineDirectory, GetExecutableName(os));
    }
}
