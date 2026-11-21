namespace DvaStream.App.Tests;

/// <summary>
/// <see cref="DvaStream.App.Localization.Strings.Culture"/> est statique : les classes de tests qui la
/// modifient ou dépendent de messages traduits partagent cette collection pour ne pas s'exécuter en parallèle.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CultureCollection
{
    public const string Name = "Culture";
}
