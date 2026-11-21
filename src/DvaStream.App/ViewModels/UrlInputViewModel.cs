using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.App.Localization;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;

namespace DvaStream.App.ViewModels;

/// <summary>
/// Champ de saisie d'URL (M1-07, M1-09 ; FR-01 à FR-03) : valide à chaque modification, expose l'état et
/// le message d'erreur, propose le lien du presse-papiers et déclenche l'analyse quand l'URL est valide.
/// </summary>
/// <remarks>
/// L'état est recalculé à chaque frappe, mais l'erreur n'est <em>affichée</em> (<see cref="IsErrorVisible"/>)
/// qu'après <see cref="RevealError"/> : on n'accuse pas l'utilisateur d'une erreur pendant qu'il tape.
/// La vue l'appelle à la perte du focus ; <see cref="Submit"/> et <see cref="ApplyText"/> la révèlent aussi.
/// Le lien du presse-papiers est seulement <em>proposé</em> (<see cref="ClipboardSuggestion"/>) : il n'est
/// jamais appliqué sans geste de l'utilisateur (FR-02). Propriétés écrites à la main (pas de générateur).
/// </remarks>
public sealed class UrlInputViewModel : ObservableObject
{
    private readonly IUrlParser _parser;
    private readonly IPlatformServices _platform;
    private string _text = string.Empty;
    private UrlInputState _state = UrlInputState.Empty;
    private string? _errorMessage;
    private VideoId? _videoId;
    private bool _errorRevealed;
    private bool _isBusy;
    private string? _clipboardSuggestion;
    private string? _lastHandledClipboard;
    private bool _isClipboardDetectionEnabled = true;
    private int _clipboardCheckVersion;

    public UrlInputViewModel(IUrlParser parser, IPlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(platform);
        _parser = parser;
        _platform = platform;

        AnalyzeCommand = new RelayCommand(Analyze, CanAnalyze);
        SubmitCommand = new RelayCommand(Submit, CanSubmit);
        ClearCommand = new RelayCommand(Clear);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty), () => IsBusy);
        UseSuggestionCommand = new RelayCommand(UseSuggestion);
        DismissSuggestionCommand = new RelayCommand(DismissSuggestion);
    }

    /// <summary>Demande d'analyse d'une URL valide ; <see cref="MainViewModel"/> s'y abonne.</summary>
    public event EventHandler<VideoId>? AnalyzeRequested;

    /// <summary>Demande d'annulation de l'analyse en cours ; <see cref="MainViewModel"/> s'y abonne.</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Texte du champ.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                Revalidate();
                HideSuggestionIfAlreadyTyped();
            }
        }
    }

    public UrlInputState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    /// <summary>Message à afficher sous le champ quand <see cref="State"/> est <see cref="UrlInputState.Invalid"/>.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Identifiant de la vidéo quand <see cref="State"/> est <see cref="UrlInputState.Valid"/>.</summary>
    public VideoId? ValidVideoId
    {
        get => _videoId;
        private set => SetProperty(ref _videoId, value);
    }

    /// <summary>Vrai pendant une analyse (positionné par <see cref="MainViewModel"/>) : champ en lecture seule, « Annuler » proposé.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseVisibilityChanged();
                AnalyzeCommand.NotifyCanExecuteChanged();
                SubmitCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Interrupteur de la détection du presse-papiers (FR-02). Actif par défaut ; piloté par les paramètres
    /// (<c>MainViewModel</c>, M3-06).
    /// </summary>
    public bool IsClipboardDetectionEnabled
    {
        get => _isClipboardDetectionEnabled;
        set
        {
            if (SetProperty(ref _isClipboardDetectionEnabled, value) && !value)
            {
                ClipboardSuggestion = null;
            }
        }
    }

    /// <summary>URL valide trouvée dans le presse-papiers et proposée à l'utilisateur ; <c>null</c> sinon.</summary>
    public string? ClipboardSuggestion
    {
        get => _clipboardSuggestion;
        private set
        {
            if (SetProperty(ref _clipboardSuggestion, value))
            {
                OnPropertyChanged(nameof(HasClipboardSuggestion));
                OnPropertyChanged(nameof(ClipboardSuggestionDisplay));
            }
        }
    }

    public bool HasClipboardSuggestion => _clipboardSuggestion is not null;

    /// <summary>Lien proposé sans schéma ni « www. », pour une lecture plus légère (<c>youtube.com/watch?v=…</c>).</summary>
    public string? ClipboardSuggestionDisplay => _clipboardSuggestion is null ? null : ShortenForDisplay(_clipboardSuggestion);

    /// <summary>Vrai quand l'erreur doit être montrée (saisie invalide et erreur révélée).</summary>
    public bool IsErrorVisible => State == UrlInputState.Invalid && _errorRevealed;

    /// <summary>Le bouton « Analyser » est visible hors analyse et hors erreur affichée.</summary>
    public bool IsAnalyzeVisible => !IsBusy && !IsErrorVisible;

    /// <summary>Le bouton ✕ (effacer) remplace « Analyser » quand une erreur est affichée.</summary>
    public bool IsClearVisible => !IsBusy && IsErrorVisible;

    /// <summary>Lance l'analyse ; active seulement si la saisie est valide et qu'aucune analyse n'est en cours.</summary>
    public IRelayCommand AnalyzeCommand { get; }

    /// <summary>
    /// Bouton « Analyser » et touche Entrée : analyse si c'est valide, sinon montre l'erreur. Active dès qu'il y a
    /// quelque chose à valider (hors analyse en cours), pour qu'un clic sur un lien refusé en explique la raison.
    /// </summary>
    public IRelayCommand SubmitCommand { get; }

    /// <summary>
    /// Vrai quand la saisie est refusée : le bouton « Analyser » prend l'aspect « en attente » (grisé) tout en
    /// restant cliquable, pour afficher la raison du refus.
    /// </summary>
    public bool IsAnalyzePending => State == UrlInputState.Invalid;

    /// <summary>Vide le champ.</summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>Demande l'annulation de l'analyse en cours ; active seulement pendant l'analyse.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>Applique le lien proposé par le presse-papiers (geste explicite de l'utilisateur).</summary>
    public IRelayCommand UseSuggestionCommand { get; }

    /// <summary>Écarte le lien proposé ; il ne sera reproposé que si le presse-papiers change.</summary>
    public IRelayCommand DismissSuggestionCommand { get; }

    /// <summary>Montre l'erreur de la saisie courante (perte du focus, tentative de validation).</summary>
    public void RevealError()
    {
        if (State == UrlInputState.Invalid && !_errorRevealed)
        {
            _errorRevealed = true;
            RaiseVisibilityChanged();
        }
    }

    /// <summary>
    /// Remplace le texte depuis l'extérieur (lien proposé par le presse-papiers) et révèle immédiatement
    /// l'erreur éventuelle : l'utilisateur n'a rien tapé, il doit voir pourquoi c'est refusé.
    /// </summary>
    public void ApplyText(string text)
    {
        Text = text;
        RevealError();
    }

    /// <summary>Touche Entrée : analyse si c'est valide, sinon montre l'erreur. Sans effet pendant une analyse.</summary>
    public void Submit()
    {
        if (IsBusy)
        {
            return;
        }

        if (State == UrlInputState.Valid)
        {
            Analyze();
        }
        else
        {
            RevealError();
        }
    }

    /// <summary>
    /// Lit le presse-papiers et propose son contenu s'il s'agit d'une URL de vidéo valide (FR-02).
    /// À appeler à l'activation de la fenêtre. Ne modifie jamais le champ.
    /// </summary>
    public async Task CheckClipboardAsync()
    {
        if (!IsClipboardDetectionEnabled || IsBusy)
        {
            return;
        }

        var version = ++_clipboardCheckVersion;
        var raw = await _platform.ReadClipboardAsync();
        if (version != _clipboardCheckVersion)
        {
            return;   // une lecture plus récente est en cours ou terminée : on lui laisse la main
        }

        var candidate = raw?.Trim();
        var isProposable =
            !string.IsNullOrEmpty(candidate)
            && _parser.Parse(candidate) is UrlParseResult.Valid
            && !string.Equals(candidate, _lastHandledClipboard, StringComparison.Ordinal)
            && !string.Equals(candidate, _text.Trim(), StringComparison.Ordinal);

        ClipboardSuggestion = isProposable ? candidate : null;
    }

    private bool CanAnalyze() => State == UrlInputState.Valid && !IsBusy;

    private bool CanSubmit() => State != UrlInputState.Empty && !IsBusy;

    private void Analyze()
    {
        // RelayCommand.Execute ne vérifie pas CanExecute : le garde-fou est ici.
        if (CanAnalyze() && _videoId is not null)
        {
            AnalyzeRequested?.Invoke(this, _videoId);
        }
    }

    private void Clear() => Text = string.Empty;

    private void UseSuggestion()
    {
        var suggestion = _clipboardSuggestion;
        if (suggestion is null)
        {
            return;
        }

        _lastHandledClipboard = suggestion;
        ClipboardSuggestion = null;
        ApplyText(suggestion);
    }

    private void DismissSuggestion()
    {
        _lastHandledClipboard = _clipboardSuggestion;
        ClipboardSuggestion = null;
    }

    private void HideSuggestionIfAlreadyTyped()
    {
        if (_clipboardSuggestion is not null
            && string.Equals(_text.Trim(), _clipboardSuggestion, StringComparison.Ordinal))
        {
            _lastHandledClipboard = _clipboardSuggestion;
            ClipboardSuggestion = null;
        }
    }

    private void Revalidate()
    {
        switch (_parser.Parse(_text))
        {
            case UrlParseResult.Valid valid:
                Apply(UrlInputState.Valid, null, valid.VideoId);
                break;
            case UrlParseResult.Playlist:
                Apply(UrlInputState.Invalid, UrlErrorMessages.ForPlaylist(), null);
                break;
            case UrlParseResult.Invalid { Reason: UrlInvalidReason.Empty }:
                Apply(UrlInputState.Empty, null, null);
                break;
            case UrlParseResult.Invalid invalid:
                Apply(UrlInputState.Invalid, UrlErrorMessages.For(invalid.Reason), null);
                break;
            default:
                throw new InvalidOperationException("Résultat d'analyse d'URL inattendu.");
        }
    }

    private void Apply(UrlInputState state, string? errorMessage, VideoId? videoId)
    {
        State = state;
        ErrorMessage = errorMessage;
        ValidVideoId = videoId;

        if (state != UrlInputState.Invalid)
        {
            _errorRevealed = false;
        }

        RaiseVisibilityChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void RaiseVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsAnalyzePending));
        OnPropertyChanged(nameof(IsErrorVisible));
        OnPropertyChanged(nameof(IsAnalyzeVisible));
        OnPropertyChanged(nameof(IsClearVisible));
    }

    private static string ShortenForDisplay(string url)
    {
        var shortened = url;
        foreach (var prefix in new[] { "https://", "http://" })
        {
            if (shortened.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                shortened = shortened[prefix.Length..];
                break;
            }
        }

        return shortened.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? shortened[4..] : shortened;
    }
}
