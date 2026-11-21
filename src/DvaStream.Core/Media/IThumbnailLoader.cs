namespace DvaStream.Core.Media;

/// <summary>Charge l'image d'une miniature (M2-09). Un échec de chargement ne doit jamais bloquer l'aperçu.</summary>
public interface IThumbnailLoader
{
    /// <summary>
    /// Octets de l'image, ou <c>null</c> si elle est introuvable, trop grosse ou illisible. Ne lève jamais d'exception
    /// d'échec réseau ; seule l'annulation lève <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<byte[]?> LoadAsync(Uri url, CancellationToken ct);
}
