using DvaStream.App.Platform;
using DvaStream.App.ViewModels;
using DvaStream.Core.Settings;
using NSubstitute;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M4-10 : ligne « Enregistrer dans ».</summary>
[Collection(CultureCollection.Name)]
public sealed class DestinationViewModelTests
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();

    public DestinationViewModelTests()
    {
        _store.Current.Returns(AppSettings.CreateDefaults("dossier-defaut"));
    }

    [Fact]
    public void Le_dossier_de_depart_est_le_dossier_par_defaut()
    {
        var vm = new DestinationViewModel(_store, _picker);

        Assert.Equal("dossier-defaut", vm.Folder);
    }

    [Fact]
    public async Task Modifier_change_le_dossier_et_l_enregistre_comme_nouveau_defaut()
    {
        _picker.PickFolderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("nouveau");
        var vm = new DestinationViewModel(_store, _picker);

        vm.ChangeCommand.Execute(null);
        await vm.ChangeCompletion;

        Assert.Equal("nouveau", vm.Folder);
        await _store.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s.DefaultFolder == "nouveau"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Annuler_le_selecteur_ne_change_rien()
    {
        _picker.PickFolderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        var vm = new DestinationViewModel(_store, _picker);

        vm.ChangeCommand.Execute(null);
        await vm.ChangeCompletion;

        Assert.Equal("dossier-defaut", vm.Folder);
        await _store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_selecteur_en_panne_ne_leve_pas_d_exception()
    {
        _picker.PickFolderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string?>(_ => throw new InvalidOperationException("boom"));
        var vm = new DestinationViewModel(_store, _picker);

        vm.ChangeCommand.Execute(null);
        await vm.ChangeCompletion;

        Assert.Equal("dossier-defaut", vm.Folder);
    }

    [Fact]
    public async Task Le_dossier_suit_les_changements_des_parametres()
    {
        var vm = new DestinationViewModel(_store, _picker);

        _store.Changed += Raise.Event<EventHandler<AppSettings>>(_store, AppSettings.CreateDefaults("ailleurs"));

        await EventuallyAsync(() => vm.Folder == "ailleurs");
    }
    // Les notifications sont postées sur le contexte de synchronisation capturé : on attend leur arrivée.
    internal static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }


    [Fact]
    public void Sans_selecteur_le_bouton_est_inactif()
    {
        var vm = new DestinationViewModel(_store);

        Assert.False(vm.ChangeCommand.CanExecute(null));
    }
}
