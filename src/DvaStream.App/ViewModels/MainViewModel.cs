using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
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
    private readonly ILogger<MainViewModel> _logger;
    private MainState _state = MainState.Idle;
    private CancellationTokenSource? _analysisCts;
    private CancellationTokenSource? _thumbnailCts;
    private Task _analysis = Task.CompletedTask;
    private Task _thumbnail = Task.CompletedTask;

    public MainViewModel(
        UrlInputViewModel urlInput,
        IMediaSource source,
        IThumbnailLoader thumbnails,
        ILogger<MainViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(urlInput);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(thumbnails);
        ArgumentNullException.ThrowIfNull(logger);

        UrlInput = urlInput;
        _source = source;
        _thumbnails = thumbnails;
        _logger = logger;

        UrlInput.AnalyzeRequested += (_, id) => _analysis = AnalyzeAsync(id);
        UrlInput.CancelRequested += (_, _) => _analysisCts?.Cancel();
    }

    public string Title => "DVA Stream";

    public UrlInputViewModel UrlInput { get; }

    /// <summary>Carte de prévisualisation (masquée tant qu'il n'y a rien à montrer).</summary>
    public PreviewViewModel Preview { get; } = new();

    public MainState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsAnalyzing));
            }
        }
    }

    public bool IsAnalyzing => State == MainState.Analyzing;

    /// <summary>Tâche de la dernière analyse lancée par l'interface (terminée si aucune) ; utile aux tests.</summary>
    public Task AnalysisCompletion => _analysis;

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

        if (State == MainState.Analyzing)
        {
            return;
        }

        CancelThumbnail();
        using var cts = new CancellationTokenSource();
        _analysisCts = cts;
        State = MainState.Analyzing;
        Preview.ShowLoading();
        UrlInput.IsBusy = true;

        try
        {
            _logger.LogInformation("Analyse de la vidéo {VideoId} avec le moteur {Engine}.", id, _source.Name);
            var info = await _source.GetInfoAsync(id, cts.Token);
            _logger.LogInformation("Analyse terminée : « {Title} ».", info.Title);
            Preview.ShowInfo(info);
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
        }
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

    private void CancelThumbnail()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
    }
}
