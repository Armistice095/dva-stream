using CommunityToolkit.Mvvm.ComponentModel;
using DvaStream.App.Localization;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;

namespace DvaStream.App.ViewModels;

/// <summary>États de la carte de prévisualisation (M2-11).</summary>
public enum PreviewState
{
    /// <summary>Rien à montrer : la carte est masquée.</summary>
    Empty,

    /// <summary>Analyse en cours.</summary>
    Loading,

    /// <summary>Miniature, titre, chaîne et durée affichés.</summary>
    Ready,

    /// <summary>Message d'erreur avec action suggérée.</summary>
    Error,
}

/// <summary>Carte de prévisualisation (FR-10 à FR-13). Pilotée par <see cref="MainViewModel"/>.</summary>
public sealed class PreviewViewModel : ObservableObject
{
    private PreviewState _state = PreviewState.Empty;
    private string _title = string.Empty;
    private string _channel = string.Empty;
    private string? _durationText;
    private byte[]? _thumbnailBytes;
    private string _errorMessage = string.Empty;
    private string _errorAction = string.Empty;

    public PreviewState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsShown));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsError));
            }
        }
    }

    public bool IsShown => State != PreviewState.Empty;

    public bool IsLoading => State == PreviewState.Loading;

    public bool IsReady => State == PreviewState.Ready;

    public bool IsError => State == PreviewState.Error;

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Channel
    {
        get => _channel;
        private set => SetProperty(ref _channel, value);
    }

    /// <summary>Durée formatée ; <c>null</c> si inconnue.</summary>
    public string? DurationText
    {
        get => _durationText;
        private set
        {
            if (SetProperty(ref _durationText, value))
            {
                OnPropertyChanged(nameof(HasDuration));
            }
        }
    }

    public bool HasDuration => !string.IsNullOrEmpty(DurationText);

    /// <summary>Octets de la miniature ; <c>null</c> tant qu'elle n'est pas chargée (ou en cas d'échec).</summary>
    public byte[]? ThumbnailBytes
    {
        get => _thumbnailBytes;
        private set => SetProperty(ref _thumbnailBytes, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string ErrorAction
    {
        get => _errorAction;
        private set => SetProperty(ref _errorAction, value);
    }

    /// <summary>Masque la carte et efface son contenu.</summary>
    public void Clear()
    {
        Reset();
        State = PreviewState.Empty;
    }

    /// <summary>Affiche l'état de chargement.</summary>
    public void ShowLoading()
    {
        Reset();
        State = PreviewState.Loading;
    }

    /// <summary>Affiche les informations de la vidéo (miniature à venir via <see cref="SetThumbnail"/>).</summary>
    public void ShowInfo(MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Reset();
        Title = info.Title;
        Channel = info.Channel;
        DurationText = DurationFormatter.Format(info.Duration);
        State = PreviewState.Ready;
    }

    /// <summary>Affiche le message d'erreur correspondant à <paramref name="kind"/>.</summary>
    public void ShowError(ErrorKind kind)
    {
        Reset();
        ErrorMessage = PreviewErrorMessages.MessageFor(kind);
        ErrorAction = PreviewErrorMessages.ActionFor(kind);
        State = PreviewState.Error;
    }

    /// <summary>Renseigne la miniature ; sans effet si la carte n'affiche plus une vidéo.</summary>
    public void SetThumbnail(byte[]? bytes)
    {
        if (State == PreviewState.Ready)
        {
            ThumbnailBytes = bytes;
        }
    }

    private void Reset()
    {
        Title = string.Empty;
        Channel = string.Empty;
        DurationText = null;
        ThumbnailBytes = null;
        ErrorMessage = string.Empty;
        ErrorAction = string.Empty;
    }
}
