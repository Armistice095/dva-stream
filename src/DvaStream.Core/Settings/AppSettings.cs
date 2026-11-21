namespace DvaStream.Core.Settings;

/// <summary>Paramètres de l'utilisateur (TDD §11). Immuable ; <see cref="ISettingsStore"/> les enregistre.</summary>
/// <param name="SchemaVersion">Version du format du fichier.</param>
/// <param name="DefaultFolder">Dossier de téléchargement par défaut.</param>
/// <param name="Theme">Thème de l'interface.</param>
/// <param name="PreferredHeight">Hauteur vidéo préférée en pixels (1080 par défaut).</param>
/// <param name="PreferredAudio">Format audio préféré.</param>
/// <param name="ClipboardDetection">Détection d'un lien dans le presse-papiers (FR-02).</param>
/// <param name="Language"><c>auto</c> (langue du système), <c>fr</c> ou <c>en</c>.</param>
public sealed record AppSettings(
    int SchemaVersion,
    string DefaultFolder,
    ThemeChoice Theme,
    int PreferredHeight,
    AudioFormat PreferredAudio,
    bool ClipboardDetection,
    string Language)
{
    /// <summary>Version courante du format de fichier.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Langues acceptées.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["auto", "fr", "en"];

    private const int MinHeight = 144;
    private const int MaxHeight = 4320;

    /// <summary>Valeurs par défaut, avec <paramref name="defaultFolder"/> comme dossier de téléchargement.</summary>
    public static AppSettings CreateDefaults(string defaultFolder) => new(
        CurrentSchemaVersion,
        defaultFolder,
        ThemeChoice.System,
        1080,
        AudioFormat.M4a,
        ClipboardDetection: true,
        "auto");

    /// <summary>
    /// Copie dont toute valeur invalide est remplacée par celle de <paramref name="defaults"/> et dont la version
    /// est la version courante.
    /// </summary>
    public AppSettings Normalize(AppSettings defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var language = Language?.Trim().ToLowerInvariant() ?? string.Empty;
        return new AppSettings(
            CurrentSchemaVersion,
            string.IsNullOrWhiteSpace(DefaultFolder) ? defaults.DefaultFolder : DefaultFolder,
            Enum.IsDefined(Theme) ? Theme : defaults.Theme,
            PreferredHeight is >= MinHeight and <= MaxHeight ? PreferredHeight : defaults.PreferredHeight,
            Enum.IsDefined(PreferredAudio) ? PreferredAudio : defaults.PreferredAudio,
            ClipboardDetection,
            SupportedLanguages.Contains(language) ? language : defaults.Language);
    }
}
