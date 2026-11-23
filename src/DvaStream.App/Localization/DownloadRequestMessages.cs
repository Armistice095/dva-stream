using DvaStream.Core.Media;

namespace DvaStream.App.Localization;

/// <summary>Texte affiché quand une demande de téléchargement est refusée avant de démarrer (M3-09).</summary>
public static class DownloadRequestMessages
{
    public static string KeyFor(DownloadRequestError error) => error switch
    {
        DownloadRequestError.FolderMissing or DownloadRequestError.FolderInvalid => "Dl_FolderInvalid",
        _ => "Dl_RequestInvalid",
    };

    public static string MessageFor(DownloadRequestError error) => Strings.Get(KeyFor(error));
}
