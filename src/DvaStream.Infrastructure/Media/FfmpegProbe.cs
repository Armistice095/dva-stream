using DvaStream.Core.Media;
using DvaStream.Infrastructure.Processes;
using Microsoft.Extensions.Logging;

namespace DvaStream.Infrastructure.Media;

/// <summary>État de FFmpeg au démarrage.</summary>
public enum FfmpegState
{
    /// <summary>Le fichier n'existe pas.</summary>
    Missing,

    /// <summary>Le fichier existe mais ne répond pas comme FFmpeg.</summary>
    Broken,

    /// <summary>FFmpeg répond.</summary>
    Ready,
}

/// <summary>Résultat de la vérification.</summary>
public sealed record FfmpegStatus(FfmpegState State, FfmpegInfo? Info = null);

/// <summary>Vérifie la présence et la version de FFmpeg (M4-02, FR-52). Ne lève jamais d'exception.</summary>
public sealed class FfmpegProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner _runner;
    private readonly ILogger<FfmpegProbe> _logger;

    public FfmpegProbe(IProcessRunner runner, ILogger<FfmpegProbe> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task<FfmpegStatus> ProbeAsync(string ffmpegPath, CancellationToken ct = default)
    {
        if (!File.Exists(ffmpegPath))
        {
            _logger.LogWarning("FFmpeg introuvable : {Path}", ffmpegPath);
            return new FfmpegStatus(FfmpegState.Missing);
        }

        try
        {
            var result = await _runner.RunAsync(new ProcessSpec(ffmpegPath, ["-version"], Timeout), ct).ConfigureAwait(false);
            var info = result.ExitCode == 0 ? FfmpegVersionParser.Parse(result.StandardOutput) : null;
            if (info is null)
            {
                _logger.LogWarning("FFmpeg ne répond pas comme prévu (code {ExitCode}).", result.ExitCode);
                return new FfmpegStatus(FfmpegState.Broken);
            }

            if (info.IsGpl)
            {
                _logger.LogWarning("FFmpeg {Version} est un build GPL ; le build LGPL est visé (D-004).", info.Version);
            }
            else
            {
                _logger.LogInformation("FFmpeg {Version} prêt.", info.Version);
            }

            return new FfmpegStatus(FfmpegState.Ready, info);
        }
        catch (Exception ex) when (ex is ProcessLaunchException or TimeoutException)
        {
            _logger.LogWarning(ex, "FFmpeg n'a pas pu être lancé.");
            return new FfmpegStatus(FfmpegState.Broken);
        }
    }
}
