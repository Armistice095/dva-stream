namespace DvaStream.Core.Platform;

/// <summary>
/// Emplacement de FFmpeg et ffprobe : dans le dossier du moteur, à côté de yt-dlp (M4-02). yt-dlp reçoit ce dossier
/// par <c>--ffmpeg-location</c>. Fonction pure.
/// </summary>
public static class FfmpegLocator
{
    /// <summary>Nom du fichier FFmpeg selon l'OS.</summary>
    public static string GetExecutableName(HostOs os) => os == HostOs.Windows ? "ffmpeg.exe" : "ffmpeg";

    /// <summary>Nom du fichier ffprobe selon l'OS.</summary>
    public static string GetProbeName(HostOs os) => os == HostOs.Windows ? "ffprobe.exe" : "ffprobe";

    /// <summary>Dossier à donner à <c>--ffmpeg-location</c>.</summary>
    public static string GetDirectory(AppDirectories directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        return directories.EngineDirectory;
    }

    /// <summary>Chemin complet attendu de FFmpeg.</summary>
    public static string GetExecutablePath(AppDirectories directories, HostOs os) =>
        Path.Combine(GetDirectory(directories), GetExecutableName(os));
}
