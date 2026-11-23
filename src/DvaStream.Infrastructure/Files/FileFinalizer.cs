using DvaStream.Core.Errors;
using DvaStream.Core.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.Infrastructure.Files;

/// <summary>
/// Le fichier est d'abord déplacé sous un nom provisoire <c>.dva-&lt;id&gt;.part</c> <b>dans le dossier de destination</b>
/// (renommage instantané sur le même volume, copie sinon), puis renommé sous son vrai nom : un renommage dans un
/// même dossier est atomique, donc le nom final n'existe jamais à moitié écrit, même entre deux volumes. Si le nom
/// est pris entre-temps, un autre est choisi ; un fichier existant n'est jamais écrasé.
/// </summary>
public sealed class FileFinalizer : IFileFinalizer
{
    private const int MaxAttempts = 20;

    private readonly IFileNamer _namer;
    private readonly ILogger<FileFinalizer> _logger;

    public FileFinalizer(IFileNamer namer, ILogger<FileFinalizer>? logger = null)
    {
        _namer = namer;
        _logger = logger ?? NullLogger<FileFinalizer>.Instance;
    }

    /// <inheritdoc />
    public string MoveToFolder(string tempFile, string folder, string title, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        if (!File.Exists(tempFile))
        {
            throw new FileFinalizeException(new AppError(ErrorKind.Unknown, $"Fichier téléchargé introuvable : {tempFile}"));
        }

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FileFinalizeException(new AppError(ErrorKind.FolderNotWritable, $"Dossier inaccessible : {folder} ({ex.Message})", ex));
        }

        var partial = Path.Combine(folder, $".dva-{Guid.NewGuid():N}.part");
        try
        {
            File.Move(tempFile, partial);

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var final = Path.Combine(folder, _namer.BuildSafeName(title, extension, folder));
                try
                {
                    File.Move(partial, final, overwrite: false);
                    _logger.LogInformation("Fichier placé : {File}", Path.GetFileName(final));
                    return final;
                }
                catch (IOException) when (File.Exists(final))
                {
                    // Un autre fichier a pris ce nom entre-temps : on en choisit un autre.
                }
            }

            throw new FileFinalizeException(new AppError(ErrorKind.Unknown, "Aucun nom de fichier libre n'a pu être trouvé."));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FileFinalizeException(new AppError(ErrorKind.FolderNotWritable, $"Écriture refusée dans {folder} ({ex.Message})", ex));
        }
        catch (IOException ex)
        {
            var kind = IsDiskFull(ex) ? ErrorKind.DiskFull : ErrorKind.Unknown;
            throw new FileFinalizeException(new AppError(kind, $"Déplacement impossible vers {folder} ({ex.Message})", ex));
        }
        finally
        {
            RestoreOrDelete(partial, tempFile);
        }
    }

    private static bool IsDiskFull(IOException ex) =>
        (ex.HResult & 0xFFFF) is 0x70 or 0x27
        || ex.Message.Contains("No space left", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Après un échec : si le fichier provisoire est tout ce qui reste du téléchargement (déplacement déjà fait), on le
    /// remet à sa place d'origine pour ne rien perdre ; sinon c'est une copie inachevée, on la supprime.
    /// </summary>
    private void RestoreOrDelete(string partial, string tempFile)
    {
        try
        {
            if (!File.Exists(partial))
            {
                return;
            }

            if (File.Exists(tempFile))
            {
                File.Delete(partial);
            }
            else
            {
                File.Move(partial, tempFile);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Fichier provisoire non nettoyé : {Path}", partial);
        }
    }
}
