using CommunityToolkit.Mvvm.ComponentModel;
using DvaStream.Core.Media;
using DvaStream.Core.Urls;
using Microsoft.Extensions.Logging;

namespace DvaStream.App.ViewModels;

/// <summary>
/// Modèle de vue de la fenêtre principale. Au jalon M1 (M1-10) : orchestre <c>Idle → Analyzing → Idle</c>
/// avec un moteur simulé ; la prévisualisation arrive en M2.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IMediaSource _source;
    private readonly ILogger<MainViewModel> _logger;
    private MainState _state = MainState.Idle;
    private CancellationTokenSource? _analysisCts;
    private Task _analysis = Task.CompletedTask;

    public MainViewModel(UrlInputViewModel urlInput, IMediaSource source, ILogger<MainViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(urlInput);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);

        UrlInput = urlInput;
        _source = source;
        _logger = logger;

        UrlInput.AnalyzeRequested += (_, id) => _analysis = AnalyzeAsync(id);
        UrlInput.CancelRequested += (_, _) => _analysisCts?.Cancel();
    }

    public string Title => "DVA Stream";

    public UrlInputViewModel UrlInput { get; }

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

    /// <summary>
    /// Analyse la vidéo : passe en <see cref="MainState.Analyzing"/>, interroge le moteur, puis revient à
    /// <see cref="MainState.Idle"/>. Sans effet si une analyse est déjà en cours. Ne lève jamais d'exception :
    /// l'affichage des erreurs de moteur arrive en M2-12.
    /// </summary>
    public async Task AnalyzeAsync(VideoId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (State != MainState.Idle)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        _analysisCts = cts;
        State = MainState.Analyzing;
        UrlInput.IsBusy = true;

        try
        {
            _logger.LogInformation("Analyse de la vidéo {VideoId} avec le moteur {Engine}.", id, _source.Name);
            var info = await _source.GetInfoAsync(id, cts.Token);
            _logger.LogInformation("Analyse terminée : « {Title} ».", info.Title);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analyse annulée.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'analyse de la vidéo {VideoId}.", id);
        }
        finally
        {
            _analysisCts = null;
            UrlInput.IsBusy = false;
            State = MainState.Idle;
        }
    }
}
