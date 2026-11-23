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

    /// <summary>Arguments du téléchargement d'un plan (jamais de shell : liste d'arguments).</summary>
    public static IReadOnlyList<string> BuildDownloadArguments(DownloadPlan plan, string ffmpegDirectory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegDirectory);

        return
        [
            "--no-playlist",
            "--no-warnings",
            "--newline",
            "--no-mtime",
            "--retries", "10",
            "--fragment-retries", "10",
            "--continue",
            "--ffmpeg-location", ffmpegDirectory,
            "--progress-template", "download:" + YtDlpProgressParser.DownloadTemplate,
            "--progress-template", "postprocess:" + YtDlpProgressParser.PostprocessTemplate,
            .. plan.FormatArguments,
            "-o", plan.OutputTemplate,
            $"https://www.youtube.com/watch?v={plan.Request.Id.Value}",
        ];
    }

    /// <inheritdoc />
    public async Task<string> DownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);

        var executable = _platform.EngineExecutablePath;
        if (!File.Exists(executable))
        {
            throw Fail(new AppError(ErrorKind.EngineMissing, $"yt-dlp est introuvable : {executable}"));
        }

        var ffmpegDirectory = FfmpegLocator.GetDirectory(_platform.Directories);
        var ffmpegPath = Path.Combine(
            ffmpegDirectory,
            FfmpegLocator.GetExecutableName(OperatingSystem.IsWindows() ? HostOs.Windows : HostOs.Linux));
        var needsFfmpeg = plan.Request.Mode == DownloadMode.Video || plan.FormatArguments.Contains("-x");
        if (needsFfmpeg && !File.Exists(ffmpegPath))
        {
            throw Fail(new AppError(ErrorKind.FfmpegMissing, $"FFmpeg est introuvable : {ffmpegPath}"));
        }

        try
        {
            Directory.CreateDirectory(plan.TempDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Fail(new AppError(ErrorKind.FolderNotWritable, $"Dossier temporaire inaccessible : {plan.TempDirectory}", ex));
        }

        var tracker = new DownloadProgressTracker(plan);
        progress.Report(tracker.Current);

        var spec = new ProcessSpec(
            executable,
            BuildDownloadArguments(plan, ffmpegDirectory),
            Timeout: null,
            Environment: Utf8Environment,
            OnOutputLine: line =>
            {
                if (tracker.Apply(YtDlpProgressParser.Parse(line)) is { } update)
                {
                    progress.Report(update);
                }
            });

        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(spec, ct).ConfigureAwait(false);
        }
        catch (ProcessLaunchException ex)
        {
            throw Fail(new AppError(ErrorKind.EngineBroken, $"yt-dlp ne démarre pas : {ex.Message}", ex));
        }

        if (result.ExitCode != 0)
        {
            var error = YtDlpErrorMapper.Map(result.ExitCode, result.StandardError);
            _logger.LogWarning("Téléchargement de {VideoId} échoué ({Kind}) : {Details}", plan.Request.Id.Value, error.Kind, error.TechnicalDetails);
            throw Fail(error);
        }

        var produced = FindProducedFile(plan)
            ?? throw Fail(new AppError(ErrorKind.Unknown, $"yt-dlp a terminé sans produire de fichier dans {plan.TempDirectory}."));

        progress.Report(tracker.Complete());
        _logger.LogInformation("Téléchargement de {VideoId} terminé : {File}", plan.Request.Id.Value, Path.GetFileName(produced));
        return produced;
    }

    private static string? FindProducedFile(DownloadPlan plan)
    {
        if (File.Exists(plan.TempFilePath))
        {
            return plan.TempFilePath;
        }

        // Extension imprévue (conteneur différent de celui annoncé) : « media.<extension> » seul, sans reste de flux.
        return Directory.Exists(plan.TempDirectory)
            ? Directory.EnumerateFiles(plan.TempDirectory, "media.*")
                .FirstOrDefault(f => System.Text.RegularExpressions.Regex.IsMatch(
                    Path.GetFileName(f), @"^media\.(?!part$|ytdl$)[A-Za-z0-9]+$"))
            : null;
    }

    private static MediaSourceException Fail(AppError error) => new(error);
}
