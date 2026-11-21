using DvaStream.Core.Settings;
using DvaStream.Core.Urls;

namespace DvaStream.Core.Media;

/// <summary>Raison pour laquelle une demande de téléchargement est refusée (M3-09).</summary>
public enum DownloadRequestError
{
    /// <summary>Aucun dossier de destination.</summary>
    FolderMissing,

    /// <summary>Le dossier n'est pas un chemin absolu valide.</summary>
    FolderInvalid,

    /// <summary>Mode vidéo sans résolution choisie.</summary>
    HeightMissing,

    /// <summary>Mode vidéo alors que la vidéo n'offre aucun flux avec image.</summary>
    NoVideoStream,

    /// <summary>La résolution choisie n'est pas offerte par la vidéo.</summary>
    ResolutionNotOffered,

    /// <summary>Mode audio alors que la vidéo n'offre aucun flux sonore.</summary>
    NoAudioStream,
}

/// <summary>Résultat de <see cref="DownloadRequest.Create"/> : une demande valide ou la raison du refus.</summary>
/// <param name="Request">Demande valide, ou <c>null</c>.</param>
/// <param name="Error">Raison du refus, ou <c>null</c>.</param>
public sealed record DownloadRequestResult(DownloadRequest? Request, DownloadRequestError? Error)
{
    /// <summary>Vrai si la demande est valide.</summary>
    public bool IsValid => Request is not null;
}

/// <summary>Ce qu'il faut télécharger, déjà validé (M3-09). Pas de texte utilisateur : l'interface traduit les erreurs.</summary>
/// <param name="Id">Vidéo concernée.</param>
/// <param name="Title">Titre, base du nom de fichier.</param>
/// <param name="Mode">Vidéo ou audio seul.</param>
/// <param name="Height">Hauteur demandée (mode vidéo uniquement, <c>null</c> en audio).</param>
/// <param name="AudioFormat">Format audio (mode audio).</param>
/// <param name="Folder">Dossier de destination (chemin absolu).</param>
public sealed record DownloadRequest(
    VideoId Id,
    string Title,
    DownloadMode Mode,
    int? Height,
    AudioFormat AudioFormat,
    string Folder)
{
    /// <summary>
    /// Construit une demande à partir de la vidéo analysée et des choix de l'utilisateur, ou explique pourquoi c'est
    /// impossible. Ne touche pas au disque : l'existence et les droits d'écriture du dossier sont vérifiés au moment
    /// du téléchargement.
    /// </summary>
    public static DownloadRequestResult Create(
        MediaInfo info,
        DownloadMode mode,
        int? height,
        AudioFormat audioFormat,
        string? folder)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (string.IsNullOrWhiteSpace(folder))
        {
            return Fail(DownloadRequestError.FolderMissing);
        }

        if (folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathRooted(folder))
        {
            return Fail(DownloadRequestError.FolderInvalid);
        }

        if (mode == DownloadMode.Audio)
        {
            return info.HasAudio()
                ? Ok(info, mode, null, audioFormat, folder)
                : Fail(DownloadRequestError.NoAudioStream);
        }

        var available = info.GetAvailableResolutions();
        if (available.Count == 0)
        {
            return Fail(DownloadRequestError.NoVideoStream);
        }

        if (height is null)
        {
            return Fail(DownloadRequestError.HeightMissing);
        }

        return available.Contains(height.Value)
            ? Ok(info, mode, height, audioFormat, folder)
            : Fail(DownloadRequestError.ResolutionNotOffered);
    }

    private static DownloadRequestResult Ok(
        MediaInfo info, DownloadMode mode, int? height, AudioFormat audio, string folder) =>
        new(new DownloadRequest(info.Id, info.Title, mode, height, audio, folder), null);

    private static DownloadRequestResult Fail(DownloadRequestError error) => new(null, error);
}
