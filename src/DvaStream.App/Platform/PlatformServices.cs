using DvaStream.Core.Platform;

namespace DvaStream.App.Platform;

/// <summary>Implémentation réelle de <see cref="IPlatformServices"/> pour l'OS courant.</summary>
public sealed class PlatformServices : IPlatformServices
{
    public PlatformServices()
    {
        var os = OperatingSystem.IsWindows() ? HostOs.Windows
               : OperatingSystem.IsMacOS() ? HostOs.MacOs
               : HostOs.Linux;

        Directories = AppDirectoryResolver.Resolve(
            os,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable);
    }

    public AppDirectories Directories { get; }
}
