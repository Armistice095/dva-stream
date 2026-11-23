using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.App.Localization;
using DvaStream.App.Platform;
using DvaStream.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.App.ViewModels;

/// <summary>
/// Ligne « Enregistrer dans » de l'écran principal (M4-10, FR-34). Part du dossier par défaut des paramètres ;
/// « Modifier » ouvre le sélecteur de dossier et le choix devient le nouveau dossier par défaut (D-043).
/// </summary>
public sealed class DestinationViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IFolderPicker? _picker;
    private readonly ILogger _logger;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private string _folder;
    private Task _change = Task.CompletedTask;

    public DestinationViewModel(ISettingsStore store, IFolderPicker? picker = null, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _picker = picker;
        _logger = logger ?? NullLogger.Instance;
        _folder = store.Current.DefaultFolder;
        ChangeCommand = new AsyncRelayCommand(ChangeAsync, () => _picker is not null);
        _store.Changed += (_, settings) => RunOnUi(() => Folder = settings.DefaultFolder);
    }

    /// <summary>Dossier de destination du prochain téléchargement.</summary>
    public string Folder
    {
        get => _folder;
        private set => SetProperty(ref _folder, value);
    }

    /// <summary>Ouvre le sélecteur de dossier.</summary>
    public IAsyncRelayCommand ChangeCommand { get; }

    /// <summary>Tâche du dernier changement de dossier (terminée si aucun) ; utile aux tests.</summary>
    public Task ChangeCompletion => _change;

    private Task ChangeAsync() => _change = PickAndSaveAsync();

    private async Task PickAndSaveAsync()
    {
        if (_picker is null)
        {
            return;
        }

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

        Folder = picked;
        try
        {
            await _store.SaveAsync(_store.Current with { DefaultFolder = picked }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dossier par défaut non enregistré.");
        }
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
