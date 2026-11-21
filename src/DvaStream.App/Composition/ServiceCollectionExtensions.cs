using DvaStream.App.ViewModels;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DvaStream.App.Composition;

/// <summary>Composition root unique de l'application (TDD ADR-06).</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDvaStream(this IServiceCollection services, IPlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(platform);

        services.AddSingleton(platform);
        services.AddDvaStreamInfrastructure(platform.Directories.LogsDirectory);
        services.AddSingleton<IUrlParser, UrlParser>();
        services.AddSingleton<UrlInputViewModel>();
        services.AddSingleton<MainViewModel>();
        return services;
    }
}
