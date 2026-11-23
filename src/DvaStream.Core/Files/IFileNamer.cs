namespace DvaStream.Core.Files;

/// <summary>Construit le nom du fichier final (M4-06, TDD §8.5, D-007 : le titre seul, sans nom de chaîne).</summary>
public interface IFileNamer
{
    /// <summary>
    /// Nom de fichier <c>Titre.ext</c> valide sur Windows, macOS et Linux, qui n'existe pas déjà dans
    /// <paramref name="folder"/> (suffixe <c>Titre (1).ext</c>, <c>Titre (2).ext</c>… ; jamais d'écrasement).
    /// Renvoie un nom, pas un chemin.
    /// </summary>
    string BuildSafeName(string title, string extension, string folder);
}
