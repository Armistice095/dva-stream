using DvaStream.Core.Media;
using DvaStream.Core.Urls;

namespace DvaStream.Infrastructure.Media;

/// <summary>
/// Faux moteur du jalon M1 (M1-10) : attend un peu puis renvoie une vidéo fictive, pour faire vivre
/// l'état « Analyse en cours ». Remplacé par <c>YtDlpMediaSource</c> en M2-06.
/// </summary>
public sealed class SimulatedMediaSource : IMediaSource
{
    private readonly TimeSpan _delay;

    public SimulatedMediaSource(TimeSpan? delay = null) => _delay = delay ?? TimeSpan.FromSeconds(1.5);

    public string Name => "Simulé";

    public Task<string> DownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("Le moteur simulé ne télécharge pas.");

    public async Task<MediaInfo> GetInfoAsync(VideoId id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);

        await Task.Delay(_delay, ct).ConfigureAwait(false);

        return new MediaInfo(
            id,
            Title: "Vidéo simulée",
            Channel: "DVA Labs",
            Duration: TimeSpan.FromMinutes(3),
            ThumbnailUrl: null,
            Streams: []);
    }
}
