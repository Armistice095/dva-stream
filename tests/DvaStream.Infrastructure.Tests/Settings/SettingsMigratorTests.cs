using System.Text.Json.Nodes;
using DvaStream.Infrastructure.Settings;

namespace DvaStream.Infrastructure.Tests.Settings;

/// <summary>M3-02 : migration d'anciens fichiers de paramètres.</summary>
public class SettingsMigratorTests
{
    [Theory]
    [InlineData("""{"x":1}""", 0)]
    [InlineData("""{"schemaVersion":"abc"}""", 0)]
    [InlineData("""{"schemaVersion":-3}""", 0)]
    [InlineData("""{"schemaVersion":2}""", 2)]
    public void La_version_de_depart_est_lue_avec_0_par_defaut(string json, int expected)
    {
        Assert.Equal(expected, SettingsMigrator.VersionOf((JsonObject)JsonNode.Parse(json)!));
    }

    [Fact]
    public void Un_fichier_sans_version_est_estampille_sans_perdre_de_donnees()
    {
        var root = (JsonObject)JsonNode.Parse("""{"theme":"Dark"}""")!;

        new SettingsMigrator(1).Migrate(root);

        Assert.Equal(1, (int)root["schemaVersion"]!);
        Assert.Equal("Dark", (string)root["theme"]!);
    }

    [Fact]
    public void Les_etapes_manquantes_sont_appliquees_dans_l_ordre()
    {
        var calls = new List<int>();
        var steps = new Dictionary<int, Action<JsonObject>>
        {
            [1] = o => { calls.Add(1); o["quality"] = 720; },
            [2] = o => { calls.Add(2); o["preferredHeight"] = o["quality"]!.DeepClone(); o.Remove("quality"); },
        };
        var root = (JsonObject)JsonNode.Parse("""{"schemaVersion":1}""")!;

        new SettingsMigrator(3, steps).Migrate(root);

        Assert.Equal(new[] { 1, 2 }, calls);
        Assert.Equal(3, (int)root["schemaVersion"]!);
        Assert.Equal(720, (int)root["preferredHeight"]!);
        Assert.Null(root["quality"]);
    }

    [Fact]
    public void Un_fichier_deja_a_jour_ou_plus_recent_n_est_pas_modifie()
    {
        var current = (JsonObject)JsonNode.Parse("""{"schemaVersion":1,"a":1}""")!;
        var newer = (JsonObject)JsonNode.Parse("""{"schemaVersion":9,"a":1}""")!;

        new SettingsMigrator(1).Migrate(current);
        new SettingsMigrator(1).Migrate(newer);

        Assert.Equal(1, (int)current["schemaVersion"]!);
        Assert.Equal(9, (int)newer["schemaVersion"]!);
    }
}
