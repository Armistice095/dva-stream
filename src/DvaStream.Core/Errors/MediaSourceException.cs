namespace DvaStream.Core.Errors;

/// <summary>
/// Échec d'un <see cref="Media.IMediaSource"/> : porte une <see cref="AppError"/> typée que la couche
/// présentation traduit en message (M2-12). L'annulation n'en est pas une : elle reste une
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class MediaSourceException : Exception
{
    public MediaSourceException(AppError error)
        : base(BuildMessage(error), error?.Inner)
    {
        Error = error!;
    }

    /// <summary>Erreur typée.</summary>
    public AppError Error { get; }

    private static string BuildMessage(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return $"{error.Kind} : {error.TechnicalDetails}";
    }
}
