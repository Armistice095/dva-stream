using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.App.Localization;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;

namespace DvaStream.App.ViewModels;

/// <summary>Une résolution proposée dans la liste de qualité.</summary>
/// <param name="Height">Hauteur en pixels.</param>
/// <param name="Label">Texte affiché (ex. <c>1080p</c>).</param>
public sealed record ResolutionOption(int Height, string Label);

/// <summary>
/// Choix du mode (Vidéo / Audio), de la qualité et du format audio (M3-03, FR-20 à FR-22). Ne propose que les
/// résolutions réellement disponibles (FR-12) et se replie sur la plus proche inférieure (M3-04).
/// </summary>
public sealed class FormatSelectorViewModel : ObservableObject
{
    /// <summary>Le MP3 est visible mais pas encore disponible (ré-encodage prévu en M7-01).</summary>
    public const bool Mp3Available = false;

    private IReadOnlyList<ResolutionOption> _resolutions = [];
    private ResolutionOption? _selectedResolution;
    private DownloadMode _mode = DownloadMode.Video;
    private AudioFormat _audioFormat = AudioFormat.M4a;
    private bool _isConfigured;
    private bool _canChooseVideo;
    private bool _canChooseAudio;
    private string? _fallbackNote;
    private MediaInfo? _info;
    private string? _sizeNote;

    public FormatSelectorViewModel()
    {
        SelectVideoCommand = new RelayCommand(() => SetMode(DownloadMode.Video), () => _canChooseVideo);
        SelectAudioCommand = new RelayCommand(() => SetMode(DownloadMode.Audio), () => _canChooseAudio);
    }

    /// <summary>Déclenché quand l'utilisateur choisit une résolution (pas pour le repli automatique) ; sert à mémoriser la préférence.</summary>
    public event EventHandler<int>? ResolutionChosen;

    public IRelayCommand SelectVideoCommand { get; }

    public IRelayCommand SelectAudioCommand { get; }

    /// <summary>Vrai quand une vidéo analysée est présentée (le sélecteur est alors affiché).</summary>
    public bool IsConfigured
    {
        get => _isConfigured;
        private set
        {
            if (SetProperty(ref _isConfigured, value))
            {
                RaiseVisibility();
            }
        }
    }

    public DownloadMode Mode => _mode;

    public bool IsVideoMode => _mode == DownloadMode.Video;

    public bool IsAudioMode => _mode == DownloadMode.Audio;

    public bool CanChooseVideo => _canChooseVideo;

    public bool CanChooseAudio => _canChooseAudio;

    public bool ShowQuality => IsConfigured && IsVideoMode;

    public bool ShowAudioOptions => IsConfigured && IsAudioMode;

    /// <summary>Résolutions disponibles, de la plus grande à la plus petite.</summary>
    public IReadOnlyList<ResolutionOption> Resolutions
    {
        get => _resolutions;
        private set => SetProperty(ref _resolutions, value);
    }

    public ResolutionOption? SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            if (value is not null && !_resolutions.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _selectedResolution, value))
            {
                // Un choix manuel remplace la mention de repli.
                FallbackNote = null;
                UpdateSizeNote();
                if (value is not null)
                {
                    ResolutionChosen?.Invoke(this, value.Height);
                }
            }
        }
    }

    /// <summary>Hauteur choisie, ou <c>null</c> en mode audio ou sans résolution.</summary>
    public int? SelectedHeight => IsVideoMode ? _selectedResolution?.Height : null;

    public AudioFormat AudioFormat => _audioFormat;

    public bool IsM4aSelected => _audioFormat == AudioFormat.M4a;

    public bool IsMp3Available => Mp3Available;

    /// <summary>Mention « 1080p n'est pas disponible : 720p sélectionnée » ; <c>null</c> sinon.</summary>
    public string? FallbackNote
    {
        get => _fallbackNote;
        private set
        {
            if (SetProperty(ref _fallbackNote, value))
            {
                OnPropertyChanged(nameof(HasFallbackNote));
            }
        }
    }

    public bool HasFallbackNote => !string.IsNullOrEmpty(_fallbackNote);

    /// <summary>« Fichier d'environ 232 Mo. Plus la qualité est haute, plus il est lourd. » ; <c>null</c> si la taille est inconnue.</summary>
    public string? SizeNote
    {
        get => _sizeNote;
        private set
        {
            if (SetProperty(ref _sizeNote, value))
            {
                OnPropertyChanged(nameof(HasSizeNote));
            }
        }
    }

    public bool HasSizeNote => !string.IsNullOrEmpty(_sizeNote);

    /// <summary>Prépare le sélecteur pour <paramref name="info"/> avec les préférences enregistrées.</summary>
    public void Configure(MediaInfo info, int preferredHeight, AudioFormat preferredAudio)
    {
        ArgumentNullException.ThrowIfNull(info);

        var heights = info.GetAvailableResolutions();
        Resolutions = heights.Select(h => new ResolutionOption(h, QualityLabels.For(h))).ToList();

        var chosen = ResolutionResolver.Resolve(preferredHeight, heights);
        _selectedResolution = chosen is { } h2 ? _resolutions.First(r => r.Height == h2) : null;
        FallbackNote = chosen is { } picked && picked != preferredHeight
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("Format_Fallback"), preferredHeight, picked)
            : null;

        _canChooseVideo = heights.Count > 0;
        _canChooseAudio = info.HasAudio();
        _audioFormat = preferredAudio == AudioFormat.Mp3 && !Mp3Available ? AudioFormat.M4a : preferredAudio;
        _mode = _canChooseVideo ? DownloadMode.Video : DownloadMode.Audio;

        _info = info;
        UpdateSizeNote();
        IsConfigured = true;
        OnPropertyChanged(string.Empty);
        SelectVideoCommand.NotifyCanExecuteChanged();
        SelectAudioCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Masque le sélecteur et oublie la vidéo précédente.</summary>
    public void Clear()
    {
        Resolutions = [];
        _selectedResolution = null;
        _canChooseVideo = false;
        _canChooseAudio = false;
        _fallbackNote = null;
        _info = null;
        _sizeNote = null;
        _mode = DownloadMode.Video;
        IsConfigured = false;
        OnPropertyChanged(string.Empty);
        SelectVideoCommand.NotifyCanExecuteChanged();
        SelectAudioCommand.NotifyCanExecuteChanged();
    }

    private void SetMode(DownloadMode mode)
    {
        // Execute() d'une commande n'évalue pas CanExecute : on protège ici contre un mode impossible.
        var allowed = mode == DownloadMode.Video ? _canChooseVideo : _canChooseAudio;
        if (_mode == mode || !allowed)
        {
            return;
        }

        _mode = mode;
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsVideoMode));
        OnPropertyChanged(nameof(IsAudioMode));
        OnPropertyChanged(nameof(SelectedHeight));
        UpdateSizeNote();
        RaiseVisibility();
    }

    private void UpdateSizeNote()
    {
        var bytes = _info?.EstimateSizeBytes(_mode, _selectedResolution?.Height);
        SizeNote = bytes is > 0
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Get("Format_SizeNote"), SizeFormatter.Format(bytes.Value))
            : null;
    }

    private void RaiseVisibility()
    {
        OnPropertyChanged(nameof(ShowQuality));
        OnPropertyChanged(nameof(ShowAudioOptions));
    }
}
