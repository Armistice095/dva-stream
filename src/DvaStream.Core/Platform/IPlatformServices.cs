namespace DvaStream.Core.Platform;

/// <summary>
/// Services dépendants de l'OS (TDD §5). Dossiers (M0) et presse-papiers (M1) ; ouverture de fichiers
/// et notifications sont ajoutés au jalon M4.
/// </summary>
public interface IPlatformServices
{
    AppDirectories Directories { get; }

    /// <summary>Chemin attendu du binaire du moteur (yt-dlp) pour l'OS courant ; le fichier peut ne pas exister.</summary>
    string EngineExecutablePath { get; }

    /// <summary>
    /// Lit le texte du presse-papiers. Renvoie <c>null</c> s'il est vide, non textuel ou inaccessible
    /// (verrouillé par une autre application, session sans presse-papiers…) ; ne lève pas d'exception.
    /// </summary>
    Task<string?> ReadClipboardAsync();
}
