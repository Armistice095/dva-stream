using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using DvaStream.App.Localization;

namespace DvaStream.App.ViewModels;

/// <summary>Barre d'état du bas de la fenêtre : état du moteur et version de l'application (maquette).</summary>
public sealed class StatusBarViewModel : ObservableObject
{
    private readonly Func<bool> _isEngineAvailable;
    private readonly Func<bool> _isFfmpegAvailable;
    private bool _engineReady;
    private bool _ffmpegReady = true;

    public StatusBarViewModel(string version, Func<bool> isEngineAvailable, Func<bool>? isFfmpegAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(isEngineAvailable);
        _isEngineAvailable = isEngineAvailable;
        _isFfmpegAvailable = isFfmpegAvailable ?? (() => true);
        VersionText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("Status_Version"), version);
        Refresh();
    }

    public string VersionText { get; }

    /// <summary>Vrai si yt-dlp et FFmpeg sont tous deux présents.</summary>
    public bool EngineReady => _engineReady && _ffmpegReady;

    public bool EngineMissing => !EngineReady;

    /// <summary>« Moteur prêt », « Moteur introuvable » ou « FFmpeg introuvable ».</summary>
    public string EngineText => Strings.Get(!_engineReady ? "Status_EngineMissing" : !_ffmpegReady ? "Status_FfmpegMissing" : "Status_EngineReady");

    /// <summary>Revérifie la présence du moteur (appelé après chaque analyse).</summary>
    public void Refresh()
    {
        bool available;
        try
        {
            available = _isEngineAvailable();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            available = false;
        }

        bool ffmpeg;
        try
        {
            ffmpeg = _isFfmpegAvailable();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ffmpeg = false;
        }

        if (available != _engineReady || ffmpeg != _ffmpegReady)
        {
            _engineReady = available;
            _ffmpegReady = ffmpeg;
            OnPropertyChanged(nameof(EngineReady));
            OnPropertyChanged(nameof(EngineMissing));
            OnPropertyChanged(nameof(EngineText));
        }
    }

    /// <summary>Version de l'application sans l'empreinte de commit (« 0.1.0+abc123 » donne « 0.1.0 »).</summary>
    public static string CurrentVersion()
    {
        var info = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = info ?? typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
    }
}
