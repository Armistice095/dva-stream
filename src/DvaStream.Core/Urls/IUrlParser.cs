namespace DvaStream.Core.Urls;

/// <summary>
/// Valide strictement une saisie avant tout appel au moteur (TDD §12) et en extrait l'identifiant de vidéo.
/// Les implémentations sont pures : pas d'accès réseau ni disque, pas d'exception sur une saisie quelconque.
/// </summary>
public interface IUrlParser
{
    /// <summary>Analyse <paramref name="input"/> (les espaces de début et de fin sont ignorés).</summary>
    UrlParseResult Parse(string? input);
}
