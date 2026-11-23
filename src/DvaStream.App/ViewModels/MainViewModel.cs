using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.App.Localization;
using DvaStream.App.Platform;
using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using Microsoft.Extensions.Logging;

namespace DvaStream.App.ViewModels;

/// <summary>
/// Modèle de vue de la fenêtre principale. Jalon M2 : <c>Idle → Analyzing → Ready | Error | Idle (annulé)</c>
/// avec le moteur réel ; la carte de prévisualisation (<see cref="Preview"/>) suit l'état.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IMediaSource _source;
    private readonly IThumbnailLoader _thumbnails;
    private readonly ISettingsStore _settings;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IDownloadService? _downloads;
    private readonly IDesktopShell? _shell;
    private MediaInfo? _info;
    private DownloadViewModel? _download;
    private string? _downloadMessage;
    private Task _downloadTask = Task.CompletedTask;
    private MainState _state = MainState.Idle;
    private AppPage _page = AppPage.Home;
    private CancellationTokenSource? _analysisCts;
    private CancellationTokenSource? _thumbnailCts;
    private Task _analysis = Task.CompletedTask;
    private Task _thumbnail = Task.CompletedTask;
    private Task _preferenceSave = Task.CompletedTask;
    private VideoId? _lastId;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    public MainViewModel(
        UrlInputViewModel urlInput,
        IMediaSource source,
        IThumbnailLoader thumbnails,
        ISettingsStore settings,
        SettingsViewModel settingsPage,
        ILogger<MainViewModel> logger,
        StatusBarViewModel? statusBar = null,
        IDownloadService? downloads = null,
        IFolderPicker? folderPicker = null,
        IDesktopShell? shell = null)
    {
        ArgumentNullException.ThrowIfNull(urlInput);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(thumbnails);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsPage);
        ArgumentNullException.ThrowIfNull(logger);

        UrlInput = urlInput;
        _source = source;
        _thumbnails = thumbnails;
        _settings = settings;
        Settings = settingsPage;
        _downloads = downloads;
        _shell = shell;
        Destination = new DestinationViewModel(settings, folderPicker, logger);
        DownloadCommand = new RelayCommand(StartDownload, () => CanDownload);
        StatusBar = statusBar ?? new StatusBarViewModel(string.Empty, () => true);
        Preview = new PreviewViewModel(RetryLast);
        Preview.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.IsShown))
            {
                OnPropertyChanged(nameof(ShowReassurance));
            }
        };
        ToggleSettingsCommand = new RelayCommand(() => Page = Page == AppPage.Settings ? AppPage.Home : AppPage.Settings);
        CloseSettingsCommand = new RelayCommand(() => Page = AppPage.Home);
        _logger = logger;

        UrlInput.AnalyzeRequested += (_, id) => _analysis = AnalyzeAsync(id);
        UrlInput.CancelRequested += (_, _) => _analysisCts?.Cancel();

        // M3-06 : l'interrupteur du presse-papiers suit les paramètres, et le choix de qualité est mémorisé.
        UrlInput.IsClipboardDetectionEnabled = _settings.Current.ClipboardDetection;
        _settings.Changed += (_, s) => RunOnUi(() => UrlInput.IsClipboardDetectionEnabled = s.ClipboardDetection);
        FormatSelector.ResolutionChosen += (_, height) => _preferenceSave = SaveHeightAsync(height);
    }

    public string Title => "DVA Stream";

    /// <summary>Dossier de destination (M4-10).</summary>
    public DestinationViewModel Destination { get; }

    /// <summary>Lance le téléchargement avec les choix faits (M4-11).</summary>
    public IRelayCommand DownloadCommand { get; }

    /// <summary>Vrai quand une vidéo est analysée et qu'un téléchargement peut démarrer.</summary>
    public bool CanDownload => State == MainState.Ready && _downloads is not null && _info is not null;

    /// <summary>Carte de progression du téléchargement en cours ou terminé, sinon <c>null</c>.</summary>
    public DownloadViewModel? Download
    {
        get => _download;
        private set
        {
            if (SetProperty(ref _download, value))
            {
                OnPropertyChanged(nameof(ShowDownload));
            }
        }
    }

    /// <summary>Message quand la demande est refusée avant de démarrer (dossier invalide...).</summary>
    public string? DownloadMessage
    {
        get => _downloadMessage;
        private set
        {
            if (SetProperty(ref _downloadMessage, value))
            {
                OnPropertyChanged(nameof(HasDownloadMessage));
            }
        }
    }

    public bool HasDownloadMessage => !string.IsNullOrEmpty(_downloadMessage);

    /// <summary>Choix du format, dossier et bouton Télécharger : visibles une fois la vidéo analysée.</summary>
    public bool ShowForm => State == MainState.Ready;

    /// <summary>Champ de saisie : masqué pendant et après un téléchargement.</summary>
    public bool ShowInput => State is not (MainState.Downloading or MainState.Done or MainState.DownloadFailed);

    /// <summary>Carte de progression.</summary>
    public bool ShowDownload => Download is not null;

    /// <summary>Tâche du dernier téléchargement lancé par l'interface (terminée si aucun) ; utile aux tests.</summary>
    public Task DownloadCompletion => _downloadTask;

    public UrlInputViewModel UrlInput { get; }

    /// <summary>Écran Paramètres.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Ouvre les paramètres, ou revient à l'accueil s'ils sont déjà ouverts.</summary>
    public IRelayCommand ToggleSettingsCommand { get; }

    /// <summary>Retour à l'accueil.</summary>
    public IRelayCommand CloseSettingsCommand { get; }

    /// <summary>Page affichée.</summary>
    public AppPage Page
    {
        get => _page;
        private set
        {
            if (SetProperty(ref _page, value))
            {
                OnPropertyChanged(nameof(IsHomePage));
                OnPropertyChanged(nameof(IsSettingsPage));
            }
        }
    }

    public bool IsHomePage => Page == AppPage.Home;

    public bool IsSettingsPage => Page == AppPage.Settings;

    /// <summary>Carte de prévisualisation (masquée tant qu'il n'y a rien à montrer).</summary>
    public PreviewViewModel Preview { get; }

    /// <summary>Barre d'état : moteur et version.</summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>Vrai tant que rien n'est affiché sous le champ : les trois garanties de l'application sont alors montrées.</summary>
    public bool ShowReassurance => !Preview.IsShown;

    /// <summary>Choix du mode, de la qualité et du format audio (affiché une fois la vidéo analysée).</summary>
    public FormatSelectorViewModel FormatSelector { get; } = new();

    public MainState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsAnalyzing));
                OnPropertyChanged(nameof(ShowForm));
                OnPropertyChanged(nameof(ShowInput));
                OnPropertyChanged(nameof(CanDownload));
                DownloadCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsAnalyzing => State == MainState.Analyzing;

    /// <summary>Tâche de la dernière analyse lancée par l'interface (terminée si aucune) ; utile aux tests.</summary>
    public Task AnalysisCompletion => _analysis;

    /// <summary>Tâche du dernier enregistrement de préférence (terminée si aucun) ; utile aux tests.</summary>
    public Task PreferenceSaveCompletion => _preferenceSave;

    /// <summary>Tâche du dernier chargement de miniature (terminée si aucun) ; utile aux tests.</summary>
    public Task ThumbnailCompletion => _thumbnail;

    /// <summary>
    /// Analyse la vidéo : passe en <see cref="MainState.Analyzing"/>, interroge le moteur, puis
    /// <see cref="MainState.Ready"/> (succès), <see cref="MainState.Error"/> (échec) ou <see cref="MainState.Idle"/>
    /// (annulation). Sans effet si une analyse est déjà en cours. Ne lève jamais d'exception.
    /// </summary>
    public async Task AnalyzeAsync(VideoId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (State is MainState.Analyzing or MainState.Downloading)
        {
            return;
        }

        _lastId = id;
        _info = null;
        DownloadMessage = null;
        CancelThumbnail();
        using var cts = new CancellationTokenSource();
        _analysisCts = cts;
        State = MainState.Analyzing;
        Preview.ShowLoading();
        FormatSelector.Clear();
        UrlInput.IsBusy = true;

        try
        {
            _logger.LogInformation("Analyse de la vidéo {VideoId} avec le moteur {Engine}.", id, _source.Name);
            var info = await _source.GetInfoAsync(id, cts.Token);
            _logger.LogInformation("Analyse terminée : « {Title} ».", info.Title);
            _info = info;
            Preview.ShowInfo(info);
            FormatSelector.Configure(info, _settings.Current.PreferredHeight, _settings.Current.PreferredAudio);
            State = MainState.Ready;
            StartThumbnail(info);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analyse annulée.");
            Preview.Clear();
            State = MainState.Idle;
        }
        catch (MediaSourceException ex)
        {
            _logger.LogWarning(ex, "Échec de l'analyse de la vidéo {VideoId} ({Kind}).", id, ex.Error.Kind);
            Preview.ShowError(ex.Error.Kind);
            State = MainState.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec inattendu de l'analyse de la vidéo {VideoId}.", id);
            Preview.ShowError(ErrorKind.Unknown);
            State = MainState.Error;
        }
        finally
        {
            _analysisCts = null;
            UrlInput.IsBusy = false;
            StatusBar.Refresh();
        }
    }

    private void RetryLast()
    {
        if (_lastId is { } id && State is not (MainState.Analyzing or MainState.Downloading))
        {
            _analysis = AnalyzeAsync(id);
        }
    }

    private void StartDownload()
    {
        if (!CanDownload || _info is not { } info)
        {
            return;
        }

        var result = DownloadRequest.Create(
            info, FormatSelector.Mode, FormatSelector.SelectedHeight, FormatSelector.AudioFormat, Destination.Folder);
        if (result.Request is not { } request)
        {
            DownloadMessage = DownloadRequestMessages.MessageFor(result.Error ?? DownloadRequestError.FolderInvalid);
            return;
        }

        DownloadMessage = null;
        _downloadTask = RunDownloadAsync(request, info);
    }

    private async Task RunDownloadAsync(DownloadRequest request, MediaInfo info)
    {
        if (_downloads is not { } service)
        {
            return;
        }

        var job = service.Enqueue(request, info);
        Download = new DownloadViewModel(
            job,
            () => service.Cancel(job),
            RetryDownload,
            BackToStart,
            _shell is null ? null : () => OpenFinalFile(job),
            _shell is null ? null : () => RevealFinalFile(job));
        State = MainState.Downloading;
        _logger.LogInformation("Téléchargement {JobId} démarré ({Mode}).", job.Id, request.Mode);

        try
        {
            await service.RunAsync(job, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec inattendu du téléchargement {JobId}.", job.Id);
            job.Fail(new AppError(ErrorKind.Unknown, ex.Message, ex));
        }

        switch (job.Phase)
        {
            case DownloadPhase.Completed:
                _logger.LogInformation("Téléchargement {JobId} terminé.", job.Id);
                State = MainState.Done;
                NotifyDone(job);
                break;
            case DownloadPhase.Cancelled:
                // Annulé : retour au choix du format, la vidéo reste analysée.
                _logger.LogInformation("Téléchargement {JobId} annulé.", job.Id);
                Download = null;
                State = MainState.Ready;
                break;
            default:
                _logger.LogWarning("Téléchargement {JobId} en échec ({Kind}).", job.Id, job.Error?.Kind);
                State = MainState.DownloadFailed;
                break;
        }
    }

    private void OpenFinalFile(DownloadJob job)
    {
        if (job.FinalPath is { } path)
        {
            _shell?.OpenFile(path);
        }
    }

    private void RevealFinalFile(DownloadJob job)
    {
        if (job.FinalPath is { } path)
        {
            _shell?.RevealInFolder(path);
        }
    }

    // Notification système (FR-37) ; elle n'apparaît que si la fenêtre n'est pas au premier plan.
    private void NotifyDone(DownloadJob job)
    {
        if (_shell is null || job.FinalPath is not { } path)
        {
            return;
        }

        try
        {
            _shell.Notify(Strings.Get("Notify_DoneTitle"), Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification impossible.");
        }
    }

    private void RetryDownload()
    {
        if (State == MainState.DownloadFailed && _info is { } info && Download is { } current)
        {
            _downloadTask = RunDownloadAsync(current.Job.Request, info);
        }
    }

    // « Télécharger une autre vidéo » : retour à l'accueil, champ vide.
    private void BackToStart()
    {
        if (State is not (MainState.Done or MainState.DownloadFailed))
        {
            return;
        }

        CancelThumbnail();
        Download = null;
        _info = null;
        _lastId = null;
        Preview.Clear();
        FormatSelector.Clear();
        UrlInput.ClearCommand.Execute(null);
        State = MainState.Idle;
    }

    private void StartThumbnail(MediaInfo info)
    {
        if (info.ThumbnailUrl is not { } url)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _thumbnailCts = cts;
        _thumbnail = LoadThumbnailAsync(url, cts.Token);
    }

    // Un échec de chargement ne bloque jamais la carte : elle garde son emplacement vide.
    private async Task LoadThumbnailAsync(Uri url, CancellationToken ct)
    {
        try
        {
            var bytes = await _thumbnails.LoadAsync(url, ct);
            if (!ct.IsCancellationRequested)
            {
                Preview.SetThumbnail(bytes);
            }
        }
        catch (OperationCanceledException)
        {
            // Nouvelle analyse démarrée : résultat devenu inutile.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Miniature non chargée.");
        }
    }

    private async Task SaveHeightAsync(int height)
    {
        try
        {
            if (_settings.Current.PreferredHeight != height)
            {
                await _settings.SaveAsync(_settings.Current with { PreferredHeight = height }, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Préférence de qualité non enregistrée.");
        }
    }

    // Le magasin peut signaler un changement depuis un autre thread : on revient sur le fil de l'interface.
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

    private void CancelThumbnail()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
    }
}
