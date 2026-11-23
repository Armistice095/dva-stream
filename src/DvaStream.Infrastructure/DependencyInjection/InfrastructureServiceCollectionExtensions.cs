using DvaStream.Core.Downloads;
using DvaStream.Core.Files;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Infrastructure.Downloads;
using DvaStream.Infrastructure.Files;
using DvaStream.Infrastructure.Logging;
using DvaStream.Infrastructure.Media;
using DvaStream.Infrastructure.Processes;
using DvaStream.Infrastructure.YtDlp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DvaStream.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les services d'infrastructure : la journalisation Serilog (M0), l'exécuteur de processus
    /// et la source de médias <see cref="YtDlpMediaSource"/> (M2-06). <see cref="IPlatformServices"/> est
    /// fourni par l'application. <see cref="SimulatedMediaSource"/> reste disponible pour les tests.
    /// </summary>
    public static IServiceCollection AddDvaStreamInfrastructure(this IServiceCollection services, string logsDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = LoggingSetup.CreateLogger(logsDirectory);
        services.AddLogging(builder => builder.ClearProviders().AddSerilog(logger, dispose: true));
        services.AddSingleton<IProcessRunner, CliWrapProcessRunner>();
        services.AddSingleton<IMediaSource, YtDlpMediaSource>();
        services.AddSingleton<IFileNamer, FileNamer>();
        services.AddSingleton<IFileFinalizer, FileFinalizer>();
        services.AddSingleton<IDownloadService>(sp => new DownloadService(
            sp.GetRequiredService<IMediaSource>(),
            sp.GetRequiredService<IFileFinalizer>(),
            sp.GetRequiredService<IPlatformServices>().Directories.TempDirectory,
            sp.GetService<ILogger<DownloadService>>()));
        services.AddSingleton<IThumbnailLoader>(sp => new HttpThumbnailLoader(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            sp.GetService<ILogger<HttpThumbnailLoader>>()));
        return services;
    }
}
