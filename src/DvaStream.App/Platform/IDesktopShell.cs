namespace DvaStream.App.Platform;

/// <summary>Actions de fin de téléchargement sur le bureau (M4-12, FR-37). Aucune méthode ne lève d'exception.</summary>
public interface IDesktopShell
{
    /// <summary>Ouvre le fichier avec l'application par défaut. Sans effet si le fichier n'existe plus.</summary>
    void OpenFile(string path);

    /// <summary>Affiche le fichier dans le gestionnaire de fichiers ; ouvre le dossier si le fichier n'existe plus.</summary>
    void RevealInFolder(string path);

    /// <summary>Notification système, seulement si la fenêtre de l'application n'est pas active.</summary>
    void Notify(string title, string message);
}
