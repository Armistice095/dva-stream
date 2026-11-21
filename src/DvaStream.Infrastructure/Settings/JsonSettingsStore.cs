using System.Text.Json;
using System.Text.Json.Nodes;
using DvaStream.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.Infrastructure.Settings;

/// <summary>
/// Paramètres enregistrés dans un fichier JSON (M3-01, M3-02) : écriture atomique (fichier temporaire puis
/// renommage), accès sérialisé par un verrou asynchrone, lecture tolérante champ par champ, fichier corrompu
/// remplacé par les valeurs par défaut après sauvegarde en <c>.bak</c>.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly AppSettings _defaults;
    private readonly SettingsMigrator _migrator;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile AppSettings _current;

    /// <summary>Charge le fichier (valeurs par défaut s'il est absent ou inutilisable).</summary>
    public JsonSettingsStore(
        string path,
        AppSettings defaults,
        ILogger<JsonSettingsStore>? logger = null,
        SettingsMigrator? migrator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(defaults);

        _path = path;
        _defaults = defaults.Normalize(defaults);
        _migrator = migrator ?? new SettingsMigrator(AppSettings.CurrentSchemaVersion);
        _logger = logger ?? NullLogger<JsonSettingsStore>.Instance;
        _current = Load();
    }

    /// <inheritdoc />
    public event EventHandler<AppSettings>? Changed;

    /// <inheritdoc />
    public AppSettings Current => _current;

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize(_defaults);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_path))!;
            Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, ToJson(normalized), ct).ConfigureAwait(false);
            File.Move(temporary, _path, overwrite: true);
            _current = normalized;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, normalized);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private static string ToJson(AppSettings s) => new JsonObject
    {
        ["schemaVersion"] = s.SchemaVersion,
        ["defaultFolder"] = s.DefaultFolder,
        ["theme"] = s.Theme.ToString(),
        ["preferredHeight"] = s.PreferredHeight,
        ["preferredAudio"] = s.PreferredAudio.ToString(),
        ["clipboardDetection"] = s.ClipboardDetection,
        ["language"] = s.Language,
    }.ToJsonString(WriteOptions);

    private AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return _defaults;
        }

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Paramètres illisibles : valeurs par défaut.");
            return _defaults;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(text) as JsonObject
                ?? throw new JsonException("Le fichier de paramètres n'est pas un objet JSON.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Paramètres corrompus : sauvegarde en .bak et valeurs par défaut.");
            Backup();
            return _defaults;
        }

        var from = SettingsMigrator.VersionOf(root);
        _migrator.Migrate(root);
        if (from != AppSettings.CurrentSchemaVersion)
        {
            _logger.LogInformation("Paramètres : version {From} lue vers la version {To}.", from, AppSettings.CurrentSchemaVersion);
        }

        return FromJson(root).Normalize(_defaults);
    }

    private AppSettings FromJson(JsonObject o) => _defaults with
    {
        DefaultFolder = GetString(o, "defaultFolder") ?? _defaults.DefaultFolder,
        Theme = GetEnum<ThemeChoice>(o, "theme") ?? _defaults.Theme,
        PreferredHeight = GetInt(o, "preferredHeight") ?? _defaults.PreferredHeight,
        PreferredAudio = GetEnum<AudioFormat>(o, "preferredAudio") ?? _defaults.PreferredAudio,
        ClipboardDetection = GetBool(o, "clipboardDetection") ?? _defaults.ClipboardDetection,
        Language = GetString(o, "language") ?? _defaults.Language,
    };

    private static string? GetString(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int? GetInt(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    private static bool? GetBool(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static T? GetEnum<T>(JsonObject o, string key)
        where T : struct, Enum =>
        GetString(o, key) is { } s && Enum.TryParse<T>(s, ignoreCase: true, out var e) && Enum.IsDefined(e) ? e : null;

    private void Backup()
    {
        try
        {
            File.Copy(_path, _path + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Sauvegarde .bak impossible.");
        }
    }
}
