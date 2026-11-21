using DvaStream.Core.Settings;
using DvaStream.Infrastructure.Settings;

namespace DvaStream.Infrastructure.Tests.Settings;

/// <summary>M3-01 et M3-02 : lecture, écriture atomique, fichiers corrompus ou anciens.</summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dvastream-settings-{Guid.NewGuid():N}");
    private readonly AppSettings _defaults = AppSettings.CreateDefaults("defaut");

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string FilePath => Path.Combine(_directory, "settings.json");

    private JsonSettingsStore Open() => new(FilePath, _defaults);

    [Fact]
    public void Sans_fichier_les_valeurs_par_defaut_sont_utilisees_et_rien_n_est_ecrit()
    {
        using var store = Open();

        Assert.Equal(_defaults, store.Current);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Un_enregistrement_est_relu_apres_redemarrage()
    {
        var chosen = new AppSettings(1, "D:/Videos", ThemeChoice.Dark, 720, AudioFormat.Mp3, false, "en");
        using (var first = Open())
        {
            await first.SaveAsync(chosen, CancellationToken.None);
        }

        using var second = Open();

        Assert.Equal(chosen, second.Current);
    }

    [Fact]
    public async Task L_enregistrement_cree_le_dossier_et_ne_laisse_pas_de_fichier_temporaire()
    {
        var path = Path.Combine(_directory, "sous", "dossier", "settings.json");
        using var store = new JsonSettingsStore(path, _defaults);

        await store.SaveAsync(_defaults with { PreferredHeight = 480 }, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Les_valeurs_invalides_sont_corrigees_a_l_enregistrement()
    {
        using var store = Open();

        await store.SaveAsync(_defaults with { PreferredHeight = -5, Language = "zz" }, CancellationToken.None);

        Assert.Equal(1080, store.Current.PreferredHeight);
        Assert.Equal("auto", store.Current.Language);
    }

    [Fact]
    public async Task Le_changement_est_signale_avec_les_valeurs_enregistrees()
    {
        using var store = Open();
        AppSettings? received = null;
        store.Changed += (_, s) => received = s;

        await store.SaveAsync(_defaults with { Theme = ThemeChoice.Light }, CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(ThemeChoice.Light, received!.Theme);
        Assert.Equal(received, store.Current);
    }

    [Fact]
    public async Task Des_enregistrements_simultanes_ne_corrompent_pas_le_fichier()
    {
        using var store = Open();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            store.SaveAsync(_defaults with { PreferredHeight = 144 + i }, CancellationToken.None)));

        using var reopened = Open();
        Assert.Equal(store.Current, reopened.Current);
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    public void Un_fichier_corrompu_donne_les_valeurs_par_defaut_et_une_sauvegarde_bak(string content)
    {
        File.WriteAllText(FilePath, content);

        using var store = Open();

        Assert.Equal(_defaults, store.Current);
        Assert.Equal(content, File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void Un_champ_invalide_n_invalide_pas_tout_le_fichier()
    {
        File.WriteAllText(
            FilePath,
            """{"schemaVersion":1,"defaultFolder":"D:/V","theme":"Dark","preferredHeight":"grand","preferredAudio":"Wav","clipboardDetection":false,"language":"fr"}""");

        using var store = Open();

        Assert.Equal("D:/V", store.Current.DefaultFolder);
        Assert.Equal(ThemeChoice.Dark, store.Current.Theme);
        Assert.Equal(1080, store.Current.PreferredHeight);
        Assert.Equal(AudioFormat.M4a, store.Current.PreferredAudio);
        Assert.False(store.Current.ClipboardDetection);
        Assert.Equal("fr", store.Current.Language);
    }

    [Fact]
    public void Un_ancien_fichier_sans_version_est_lu_et_les_champs_inconnus_ignores()
    {
        File.WriteAllText(FilePath, """{"theme":"Light","inconnu":{"a":1}}""");

        using var store = Open();

        Assert.Equal(ThemeChoice.Light, store.Current.Theme);
        Assert.Equal(AppSettings.CurrentSchemaVersion, store.Current.SchemaVersion);
    }

    [Fact]
    public void Un_fichier_plus_recent_est_lu_sans_perte_des_champs_connus()
    {
        File.WriteAllText(FilePath, """{"schemaVersion":7,"theme":"Dark","nouveauChamp":true}""");

        using var store = Open();

        Assert.Equal(ThemeChoice.Dark, store.Current.Theme);
    }

    [Fact]
    public void Les_noms_d_enumeration_sont_lus_sans_tenir_compte_de_la_casse()
    {
        File.WriteAllText(FilePath, """{"theme":"dark","preferredAudio":"MP3"}""");

        using var store = Open();

        Assert.Equal(ThemeChoice.Dark, store.Current.Theme);
        Assert.Equal(AudioFormat.Mp3, store.Current.PreferredAudio);
    }

    [Fact]
    public void Des_arguments_invalides_sont_refuses()
    {
        Assert.Throws<ArgumentException>(() => new JsonSettingsStore(" ", _defaults));
        Assert.Throws<ArgumentNullException>(() => new JsonSettingsStore(FilePath, null!));
    }
}
