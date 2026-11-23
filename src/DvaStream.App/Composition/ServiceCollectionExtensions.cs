using DvaStream.App.Platform;
using DvaStream.App.ViewModels;
using DvaStream.Core.Platform;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.DependencyInjection;
using DvaStream.Infrastructure.Media;
using DvaStream.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<ISettingsStore>(sp => new JsonSettingsStore(
            Path.Combine(platform.Directories.ConfigDirectory, "settings.json"),
            AppSettings.CreateDefaults(DefaultDownloadFolder()),
            sp.GetService<ILogger<JsonSettingsStore>>()));
        services.AddSingleton(new StatusBarViewModel(
            StatusBarViewModel.CurrentVersion(),
            () => File.Exists(platform.EngineExecutablePath),
            () => File.Exists(FfmpegLocator.GetExecutablePath(
                platform.Directories, OperatingSystem.IsWindows() ? HostOs.Windows : HostOs.Linux))));
        services.AddSingleton<FfmpegProbe>();
        services.AddSingleton<IFolderPicker, AvaloniaFolderPicker>();
        services.AddSingleton<IDesktopShell>(sp => new DesktopShell(null, sp.GetService<ILogger<DesktopShell>>()));
        services.AddSingleton<ThemeService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<IUrlParser, UrlParser>();
        services.AddSingleton<UrlInputViewModel>();
        services.AddSingleton<MainViewModel>();
        return services;
    }

    private static string DefaultDownloadFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
}
