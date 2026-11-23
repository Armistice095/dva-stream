using DvaStream.Core.Errors;

namespace DvaStream.Core.Files;

/// <summary>Le fichier n'a pas pu être placé dans le dossier de destination.</summary>
public sealed class FileFinalizeException : Exception
{
    public FileFinalizeException(AppError error)
        : base($"{error.Kind} : {error.TechnicalDetails}", error.Inner) => Error = error;

    /// <summary>Erreur typée.</summary>
    public AppError Error { get; }
}

/// <summary>Place le fichier terminé dans le dossier de destination (M4-07, TDD §8.5).</summary>
public interface IFileFinalizer
{
    /// <summary>
    /// Déplace <paramref name="tempFile"/> vers <paramref name="folder"/> sous un nom bâti avec <see cref="IFileNamer"/>
    /// et renvoie le chemin final. Le nom définitif n'apparaît qu'une fois le fichier complet.
    /// </summary>
    /// <exception cref="FileFinalizeException">Échec classé ; le fichier d'origine est conservé (ou remis à sa place).</exception>
    string MoveToFolder(string tempFile, string folder, string title, string extension);
}
