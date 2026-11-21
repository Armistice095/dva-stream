namespace DvaStream.Core.Settings;

/// <summary>Thème choisi par l'utilisateur (FR-61).</summary>
public enum ThemeChoice
{
    /// <summary>Suit le thème du système.</summary>
    System,

    /// <summary>Thème clair.</summary>
    Light,

    /// <summary>Thème sombre.</summary>
    Dark,
}

/// <summary>Format de sortie audio (FR-22).</summary>
public enum AudioFormat
{
    /// <summary>M4A (AAC), sans ré-encodage : valeur par défaut.</summary>
    M4a,

    /// <summary>MP3 : ré-encodage, disponible en M7-01.</summary>
    Mp3,
}
