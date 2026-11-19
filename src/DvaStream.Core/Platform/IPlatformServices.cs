namespace DvaStream.Core.Platform;

/// <summary>
/// Services dépendants de l'OS (TDD §5). Au jalon M0 : dossiers seulement ; presse-papiers,
/// ouverture de fichiers et notifications sont ajoutés aux jalons M1 et M4.
/// </summary>
public interface IPlatformServices
{
    AppDirectories Directories { get; }
}
