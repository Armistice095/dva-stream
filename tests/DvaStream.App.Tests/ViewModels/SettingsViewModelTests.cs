using DvaStream.App.Platform;
using DvaStream.App.ViewModels;
using DvaStream.Core.Settings;
using NSubstitute;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M3-07 : chaque réglage est enregistré dès qu'il change et suit les changements extérieurs.</summary>
[Collection(CultureCollection.Name)]
public class SettingsViewModelTests
{
    private readonly FakeSettingsStore _store = new(AppSettings.CreateDefaults("C:/Videos"));
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests() => _vm = new SettingsViewModel(_store, _picker);

    [Fact]
    public void L_ecran_reflete_les_parametres_enregistres()
    {
        var store = new FakeSettingsStore(new AppSettings(1, "D:/X", ThemeChoice.Dark, 720, AudioFormat.M4a, false, "auto"));

        var vm = new SettingsViewModel(store, _picker);

        Assert.Equal("D:/X", vm.DefaultFolder);
        Assert.Equal(720, vm.SelectedQuality!.Height);
        Assert.True(vm.IsThemeDark);
        Assert.False(vm.IsThemeLight);
        Assert.False(vm.IsThemeSystem);
        Assert.False(vm.ClipboardDetection);
    }

    [Fact]
    public void Les_qualites_proposees_vont_de_2160p_a_144p()
    {
        Assert.Equal(new[] { 2160, 1440, 1080, 720, 480, 360, 240, 144 }, _vm.QualityOptions.Select(o => o.Height).ToArray());
    }

    [Fact]
    public async Task Changer_la_qualite_enregistre_la_preference()
    {
        _vm.SelectedQuality = _vm.QualityOptions.Single(o => o.Height == 480);
        await _vm.SaveCompletion;

        Assert.Equal(480, _store.Current.PreferredHeight);
    }

    [Fact]
    public async Task Choisir_un_theme_l_enregistre_et_met_a_jour_les_indicateurs()
    {
        _vm.SelectThemeCommand.Execute(ThemeChoice.Dark);
        await _vm.SaveCompletion;

        Assert.Equal(ThemeChoice.Dark, _store.Current.Theme);
        Assert.True(_vm.IsThemeDark);
        Assert.False(_vm.IsThemeSystem);
    }

    [Fact]
    public async Task Rechoisir_le_meme_theme_n_enregistre_rien()
    {
        _vm.SelectThemeCommand.Execute(ThemeChoice.System);
        await _vm.SaveCompletion;

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Basculer_la_detection_du_presse_papiers_l_enregistre()
    {
        _vm.ClipboardDetection = false;
        await _vm.SaveCompletion;

        Assert.False(_store.Current.ClipboardDetection);
    }

    [Fact]
    public async Task Choisir_un_dossier_l_enregistre()
    {
        _picker.PickFolderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("E:/Mes videos"));

        await _vm.BrowseFolderCommand.ExecuteAsync(null);

        Assert.Equal("E:/Mes videos", _vm.DefaultFolder);
        Assert.Equal("E:/Mes videos", _store.Current.DefaultFolder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Annuler_le_choix_de_dossier_ne_change_rien(string? picked)
    {
        _picker.PickFolderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(picked));

        await _vm.BrowseFolderCommand.ExecuteAsync(null);

        Assert.Equal("C:/Videos", _vm.DefaultFolder);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Un_selecteur_de_dossier_en_panne_ne_plante_pas()
    {
        _picker.PickFolderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string?>(new InvalidOperationException("boum")));

        await _vm.BrowseFolderCommand.ExecuteAsync(null);

        Assert.Equal("C:/Videos", _vm.DefaultFolder);
    }

    [Fact]
    public void Un_changement_venu_d_ailleurs_met_l_ecran_a_jour_sans_reenregistrer()
    {
        _store.RaiseChanged(_store.Current with { PreferredHeight = 360, Theme = ThemeChoice.Light, ClipboardDetection = false });

        Assert.Equal(360, _vm.SelectedQuality!.Height);
        Assert.True(_vm.IsThemeLight);
        Assert.False(_vm.ClipboardDetection);
        Assert.Empty(_store.Saved);
    }

    [Theory]
    [InlineData(1000, 720)]   // entre deux valeurs : la plus proche inférieure
    [InlineData(4320, 2160)]  // au-dessus de tout : la plus haute proposée
    [InlineData(100, 144)]    // en dessous de tout : la plus basse proposée
    [InlineData(2160, 2160)]
    public void Une_hauteur_non_standard_est_rapprochee_d_une_valeur_proposee(int stored, int expected)
    {
        _store.RaiseChanged(_store.Current with { PreferredHeight = stored });

        Assert.Equal(expected, _vm.SelectedQuality!.Height);
    }

    [Fact]
    public async Task Des_changements_rapides_sont_tous_conserves()
    {
        _store.Delay = 20;

        _vm.SelectedQuality = _vm.QualityOptions.Single(o => o.Height == 360);
        _vm.SelectThemeCommand.Execute(ThemeChoice.Dark);
        _vm.ClipboardDetection = false;
        await _vm.SaveCompletion;
        await Task.Delay(200);

        Assert.Equal(360, _store.Current.PreferredHeight);
        Assert.Equal(ThemeChoice.Dark, _store.Current.Theme);
        Assert.False(_store.Current.ClipboardDetection);
    }

    [Fact]
    public async Task Un_echec_d_enregistrement_ne_plante_pas()
    {
        _store.FailWith = new IOException("disque plein");

        _vm.ClipboardDetection = false;
        await _vm.SaveCompletion;

        Assert.True(_store.Current.ClipboardDetection);
    }

    [Fact]
    public void Le_MP3_n_est_pas_encore_disponible()
    {
        Assert.False(_vm.IsMp3Available);
    }

    [Fact]
    public void Des_dependances_nulles_sont_refusees()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, _picker));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(_store, null!));
    }
}
