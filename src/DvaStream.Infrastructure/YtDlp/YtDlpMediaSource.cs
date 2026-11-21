using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Processes;
using Microsoft.Extensions.Logging;

namespace DvaStream.Infrastructure.YtDlp;

/// <summary>Source de médias réelle : interroge yt-dlp en processus externe (M2-06, TDD §6.2).</summary>
public sealed class YtDlpMediaSource : IMediaSource
{
    /// <summary>Délai maximal d'une analyse.</summary>
    public static readonly TimeSpan InfoTimeout = TimeSpan.FromSeconds(45);

    // yt-dlp est un programme Python : sans ceci, Windows encode ses sorties dans la page de code locale.
    private static readonly IReadOnlyDictionary<string, string?> Utf8Environment =
        new Dictionary<string, string?> { ["PYTHONUTF8"] = "1", ["PYTHONIOENCODING"] = "utf-8" };

    private readonly TimeSpan _timeout;
    private readonly IProcessRunner _runner;
    private readonly IPlatformServices _platform;
    private readonly ILogger<YtDlpMediaSource> _logger;

    /// <summary>Crée la source.</summary>
    public YtDlpMediaSource(
        IProcessRunner runner,
        IPlatformServices platform,
        ILogger<YtDlpMediaSource> logger,
        TimeSpan? timeout = null)
    {
        _timeout = timeout ?? InfoTimeout;
        _runner = runner;
        _platform = platform;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "yt-dlp";

    /// <summary>Arguments de l'analyse d'une vidéo (jamais de shell : liste d'arguments).</summary>
    public static IReadOnlyList<string> BuildArguments(VideoId id) =>
    [
        "--dump-single-json",
        "--no-playlist",
        "--no-warnings",
        $"https://www.youtube.com/watch?v={id.Value}",
    ];

    /// <inheritdoc />
    public async Task<MediaInfo> GetInfoAsync(VideoId id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);
        var executable = _platform.EngineExecutablePath;

        if (!File.Exists(executable))
        {
            throw Fail(new AppError(ErrorKind.EngineMissing, $"yt-dlp est introuvable : {executable}"));
        }

        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(new ProcessSpec(executable, BuildArguments(id), _timeout, Environment: Utf8Environment), ct)
                .ConfigureAwait(false);
        }
        catch (ProcessLaunchException ex)
        {
            throw Fail(new AppError(ErrorKind.EngineBroken, $"yt-dlp ne démarre pas : {ex.Message}", ex));
        }
        catch (TimeoutException ex)
        {
            throw Fail(new AppError(ErrorKind.Timeout, $"yt-dlp n'a pas répondu en {_timeout.TotalSeconds:0.#} s.", ex));
        }

        if (result.ExitCode != 0)
        {
            var error = YtDlpErrorMapper.Map(result.ExitCode, result.StandardError);
            _logger.LogWarning("Analyse de {VideoId} refusée ({Kind}) : {Details}", id.Value, error.Kind, error.TechnicalDetails);
            throw Fail(error);
        }

        var info = YtDlpInfoMapper.Parse(result.StandardOutput);
        _logger.LogInformation("Analyse de {VideoId} : {StreamCount} flux", id.Value, info.Streams.Count);
        return info;
    }

    private static MediaSourceException Fail(AppError error) => new(error);
}
