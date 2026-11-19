using DvaStream.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DvaStream.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Enregistre les services d'infrastructure du jalon M0 : la journalisation Serilog.</summary>
    public static IServiceCollection AddDvaStreamInfrastructure(this IServiceCollection services, string logsDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = LoggingSetup.CreateLogger(logsDirectory);
        services.AddLogging(builder => builder.ClearProviders().AddSerilog(logger, dispose: true));
        return services;
    }
}
