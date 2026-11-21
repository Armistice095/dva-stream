using DvaStream.Core.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.Infrastructure.Media;

/// <summary>Chargeur de miniatures HTTP(S) avec cache mémoire borné (M2-09).</summary>
public sealed class HttpThumbnailLoader : IThumbnailLoader
{
    /// <summary>Taille maximale acceptée pour une image.</summary>
    public const int MaxImageBytes = 2 * 1024 * 1024;

    private readonly HttpClient _client;
    private readonly ILogger<HttpThumbnailLoader> _logger;
    private readonly int _maxEntries;
    private readonly object _gate = new();
    private readonly Dictionary<Uri, LinkedListNode<(Uri Url, byte[] Bytes)>> _index = new();
    private readonly LinkedList<(Uri Url, byte[] Bytes)> _order = new();

    /// <summary>Crée le chargeur ; <paramref name="maxEntries"/> images au plus restent en mémoire.</summary>
    public HttpThumbnailLoader(HttpClient client, ILogger<HttpThumbnailLoader>? logger = null, int maxEntries = 32)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        _client = client;
        _logger = logger ?? NullLogger<HttpThumbnailLoader>.Instance;
        _maxEntries = maxEntries;
    }

    /// <summary>Nombre d'images en cache (pour les tests).</summary>
    public int CachedCount
    {
        get
        {
            lock (_gate)
            {
                return _index.Count;
            }
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> LoadAsync(Uri url, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme is not ("http" or "https"))
        {
            return null;
        }

        if (TryGetCached(url) is { } cached)
        {
            return cached;
        }

        try
        {
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Miniature refusée : HTTP {Status}.", (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentLength is > MaxImageBytes)
            {
                return null;
            }

            var bytes = await ReadBoundedAsync(response.Content, ct).ConfigureAwait(false);
            if (bytes is null)
            {
                return null;
            }

            Store(url, bytes);
            return bytes;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            // OperationCanceledException sans demande d'annulation = délai du client HTTP.
            _logger.LogDebug(ex, "Miniature non chargée.");
            return null;
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxImageBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    private byte[]? TryGetCached(Uri url)
    {
        lock (_gate)
        {
            if (!_index.TryGetValue(url, out var node))
            {
                return null;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            return node.Value.Bytes;
        }
    }

    private void Store(Uri url, byte[] bytes)
    {
        lock (_gate)
        {
            if (_index.TryGetValue(url, out var existing))
            {
                _order.Remove(existing);
            }

            _index[url] = _order.AddFirst((url, bytes));
            while (_index.Count > _maxEntries)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _index.Remove(last.Value.Url);
            }
        }
    }
}
