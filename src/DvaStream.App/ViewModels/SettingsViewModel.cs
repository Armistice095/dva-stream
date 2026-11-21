using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.App.Localization;
using DvaStream.App.Platform;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.App.ViewModels;

/// <summary>
/// Écran Paramètres (M3-07, FR-60 et FR-61) : chaque réglage est enregistré dès qu'il change, sans bouton
/// « Enregistrer », et suit les changements venus d'ailleurs (ex. la qualité choisie sur l'écran principal).
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private static readonly int[] StandardHeights = [2160, 1440, 1080, 720, 480, 360, 240, 144];

    private readonly ISettingsStore _store;
    private readonly IFolderPicker _picker;
    private readonly ILogger _logger;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loading;
    private string _defaultFolder = string.Empty;
    private ResolutionOption? _selectedQuality;
    private ThemeChoice _theme;
    private bool _clipboardDetection;
    private Task _save = Task.CompletedTask;

    public SettingsViewModel(ISettingsStore store, IFolderPicker picker, ILogger<SettingsViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(picker);

        _store = store;
        _picker = picker;
        _logger = logger ?? NullLogger<SettingsViewModel>.Instance;

        QualityOptions = StandardHeights.Select(h => new ResolutionOption(h, $"{h}p")).ToList();
        BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync);
        SelectThemeCommand = new RelayCommand<ThemeChoice>(SelectTheme);

        Load(_store.Current);
        _store.Changed += (_, settings) => RunOnUi(() => Load(settings));
    }

    public IReadOnlyList<ResolutionOption> QualityOptions { get; }

    public IAsyncRelayCommand BrowseFolderCommand { get; }

    public IRelayCommand<ThemeChoice> SelectThemeCommand { get; }

    /// <summary>Dossier de téléchargement par défaut.</summary>
    public string DefaultFolder
    {
        get => _defaultFolder;
        private set => SetProperty(ref _defaultFolder, value);
    }

    /// <summary>Qualité vidéo préférée (hauteur la plus proche parmi les valeurs proposées).</summary>
    public ResolutionOption? SelectedQuality
    {
        get => _selectedQuality;
        set
        {
            if (value is null || _loading || !SetProperty(ref _selectedQuality, value))
            {
                return;
            }

            var height = value.Height;
            Update(s => s with { PreferredHeight = height });
        }
    }

    public bool IsThemeSystem => _theme == ThemeChoice.System;

    public bool IsThemeLight => _theme == ThemeChoice.Light;

    public bool IsThemeDark => _theme == ThemeChoice.Dark;

    /// <summary>Le format audio est M4A ; le MP3 est visible mais pas encore disponible (M7-01).</summary>
    public bool IsMp3Available => FormatSelectorViewModel.Mp3Available;

    /// <summary>Détection d'un lien dans le presse-papiers au retour dans l'application (FR-02).</summary>
    public bool ClipboardDetection
    {
        get => _clipboardDetection;
        set
        {
            if (_loading || !SetProperty(ref _clipboardDetection, value))
            {
                return;
            }

            Update(s => s with { ClipboardDetection = value });
        }
    }

    /// <summary>Tâche du dernier enregistrement (terminée si aucun) ; utile aux tests.</summary>
    public Task SaveCompletion => _save;

    private void SelectTheme(ThemeChoice choice)
    {
        if (_theme == choice)
        {
            return;
        }

        _theme = choice;
        RaiseTheme();
        Update(s => s with { Theme = choice });
    }

    private async Task BrowseFolderAsync()
    {
        string? picked;
        try
        {
            picked = await _picker.PickFolderAsync(Strings.Get("Settings_FolderPickerTitle"), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sélecteur de dossier indisponible.");
            return;
        }

        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        DefaultFolder = picked;
        Update(s => s with { DefaultFolder = picked });
        await _save;
    }

    // Les modifications sont enregistrées l'une après l'autre ; chacune repart des paramètres les plus récents.
    private void Update(Func<AppSettings, AppSettings> change) => _save = SaveAsync(change);

    private async Task SaveAsync(Func<AppSettings, AppSettings> change)
    {
        await _gate.WaitAsync();
        try
        {
            await _store.SaveAsync(change(_store.Current), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Paramètres non enregistrés.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            DefaultFolder = settings.DefaultFolder;
            _theme = settings.Theme;
            RaiseTheme();
            var closest = ResolutionResolver.Resolve(settings.PreferredHeight, StandardHeights);
            SelectedQualityInternal(QualityOptions.FirstOrDefault(o => o.Height == closest));
            _clipboardDetection = settings.ClipboardDetection;
            OnPropertyChanged(nameof(ClipboardDetection));
        }
        finally
        {
            _loading = false;
        }
    }

    private void SelectedQualityInternal(ResolutionOption? option)
    {
        _selectedQuality = option;
        OnPropertyChanged(nameof(SelectedQuality));
    }

    private void RaiseTheme()
    {
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
    }

    private void RunOnUi(Action action)
    {
        if (_ui is null)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }
}
