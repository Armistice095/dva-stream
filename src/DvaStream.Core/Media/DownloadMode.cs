namespace DvaStream.Core.Media;

/// <summary>Ce que l'utilisateur veut obtenir : la vidéo ou seulement le son (FR-20).</summary>
public enum DownloadMode
{
    /// <summary>Vidéo avec son.</summary>
    Video,

    /// <summary>Audio seul.</summary>
    Audio,
}
