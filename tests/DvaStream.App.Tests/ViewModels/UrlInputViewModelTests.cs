using System.Globalization;
using DvaStream.App.Localization;
using DvaStream.App.ViewModels;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using NSubstitute;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M1-07 : état, messages et commandes du champ de saisie d'URL.</summary>
[Collection(CultureCollection.Name)]
public sealed class UrlInputViewModelTests : IDisposable
{
    private const string ValidUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
    private const string PlaylistUrl = "https://www.youtube.com/playlist?list=PLabc123";

    private readonly CultureInfo? _previousCulture = Strings.Culture;
    private readonly UrlInputViewModel _vm;

    public UrlInputViewModelTests()
    {
        Strings.Culture = new CultureInfo("fr");
        _vm = new UrlInputViewModel(new UrlParser(), Substitute.For<IPlatformServices>());
    }

    public void Dispose() => Strings.Culture = _previousCulture;

    [Fact]
    public void Au_depart_le_champ_est_vide_sans_erreur_et_l_analyse_est_desactivee()
    {
        Assert.Equal(string.Empty, _vm.Text);
        Assert.Equal(UrlInputState.Empty, _vm.State);
        Assert.Null(_vm.ErrorMessage);
        Assert.Null(_vm.ValidVideoId);
        Assert.False(_vm.IsErrorVisible);
        Assert.False(_vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public void Une_url_valide_donne_l_etat_valide_et_active_l_analyse()
    {
        _vm.Text = ValidUrl;

        Assert.Equal(UrlInputState.Valid, _vm.State);
        Assert.Equal("dQw4w9WgXcQ", _vm.ValidVideoId?.Value);
        Assert.Null(_vm.ErrorMessage);
        Assert.False(_vm.IsErrorVisible);
        Assert.True(_vm.AnalyzeCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Un_texte_vide_ou_blanc_donne_l_etat_vide_sans_erreur(string text)
    {
        _vm.Text = "bonjour";   // d'abord invalide, pour vérifier le retour à « vide »
        _vm.Text = text;

        Assert.Equal(UrlInputState.Empty, _vm.State);
        Assert.Null(_vm.ErrorMessage);
        Assert.False(_vm.IsErrorVisible);
    }

    [Fact]
    public void Un_texte_null_est_traite_comme_vide()
    {
        _vm.Text = ValidUrl;
        _vm.Text = null!;

        Assert.Equal(string.Empty, _vm.Text);
        Assert.Equal(UrlInputState.Empty, _vm.State);
    }

    [Theory]
    [InlineData("bonjour", UrlInvalidReason.NotAUrl)]
    [InlineData("ftp://youtu.be/dQw4w9WgXcQ", UrlInvalidReason.UnsupportedScheme)]
    [InlineData("https://evil.com/watch?v=dQw4w9WgXcQ", UrlInvalidReason.UnsupportedHost)]
    [InlineData("https://www.youtube.com/@DVALabs", UrlInvalidReason.UnrecognizedFormat)]
    [InlineData("https://www.youtube.com/watch?v=court", UrlInvalidReason.InvalidVideoId)]
    public void Une_saisie_invalide_donne_le_message_de_sa_raison(string text, UrlInvalidReason reason)
    {
        _vm.Text = text;

        Assert.Equal(UrlInputState.Invalid, _vm.State);
        Assert.Equal(UrlErrorMessages.For(reason), _vm.ErrorMessage);
        Assert.Null(_vm.ValidVideoId);
        Assert.False(_vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public void Une_playlist_est_refusee_avec_le_message_de_playlist()
    {
        _vm.Text = PlaylistUrl;

        Assert.Equal(UrlInputState.Invalid, _vm.State);
        Assert.Equal(UrlErrorMessages.ForPlaylist(), _vm.ErrorMessage);
        Assert.False(_vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public void L_erreur_reste_cachee_pendant_la_frappe_et_apparait_apres_RevealError()
    {
        _vm.Text = "htt";

        Assert.Equal(UrlInputState.Invalid, _vm.State);
        Assert.False(_vm.IsErrorVisible);

        _vm.RevealError();

        Assert.True(_vm.IsErrorVisible);
    }

    [Fact]
    public void L_erreur_revelee_disparait_quand_la_saisie_devient_valide_puis_reste_cachee_ensuite()
    {
        _vm.Text = "bonjour";
        _vm.RevealError();
        Assert.True(_vm.IsErrorVisible);

        _vm.Text = ValidUrl;
        Assert.False(_vm.IsErrorVisible);

        _vm.Text = "bonjour";   // nouvelle erreur : à nouveau cachée jusqu'à la prochaine révélation
        Assert.False(_vm.IsErrorVisible);
    }

    [Fact]
    public void RevealError_ne_fait_rien_si_la_saisie_est_vide_ou_valide()
    {
        _vm.RevealError();
        Assert.False(_vm.IsErrorVisible);

        _vm.Text = ValidUrl;
        _vm.RevealError();
        Assert.False(_vm.IsErrorVisible);
    }

    [Fact]
    public void ApplyText_revele_immediatement_l_erreur_d_un_texte_invalide()
    {
        _vm.ApplyText("bonjour");

        Assert.Equal("bonjour", _vm.Text);
        Assert.True(_vm.IsErrorVisible);
    }

    [Fact]
    public void ApplyText_avec_une_url_valide_ne_montre_pas_d_erreur()
    {
        _vm.ApplyText(ValidUrl);

        Assert.Equal(UrlInputState.Valid, _vm.State);
        Assert.False(_vm.IsErrorVisible);
    }

    [Fact]
    public void La_commande_Analyze_declenche_la_demande_avec_l_identifiant()
    {
        VideoId? requested = null;
        _vm.AnalyzeRequested += (_, id) => requested = id;
        _vm.Text = ValidUrl;

        _vm.AnalyzeCommand.Execute(null);

        Assert.Equal("dQw4w9WgXcQ", requested?.Value);
    }

    [Fact]
    public void La_commande_Analyze_ne_declenche_rien_si_la_saisie_est_invalide()
    {
        var raised = 0;
        _vm.AnalyzeRequested += (_, _) => raised++;
        _vm.Text = "bonjour";

        _vm.AnalyzeCommand.Execute(null);   // RelayCommand.Execute ignore CanExecute

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Submit_analyse_si_valide()
    {
        VideoId? requested = null;
        _vm.AnalyzeRequested += (_, id) => requested = id;
        _vm.Text = ValidUrl;

        _vm.Submit();

        Assert.Equal("dQw4w9WgXcQ", requested?.Value);
    }

    [Fact]
    public void Submit_montre_l_erreur_sans_analyser_si_invalide()
    {
        var raised = 0;
        _vm.AnalyzeRequested += (_, _) => raised++;
        _vm.Text = "bonjour";

        _vm.Submit();

        Assert.Equal(0, raised);
        Assert.True(_vm.IsErrorVisible);
    }

    [Fact]
    public void La_commande_Clear_vide_le_champ_et_cache_l_erreur()
    {
        _vm.Text = "bonjour";
        _vm.RevealError();

        _vm.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, _vm.Text);
        Assert.Equal(UrlInputState.Empty, _vm.State);
        Assert.False(_vm.IsErrorVisible);
    }

    [Fact]
    public void Les_changements_sont_notifies_a_la_vue()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _vm.Text = ValidUrl;

        Assert.Contains(nameof(UrlInputViewModel.Text), changed);
        Assert.Contains(nameof(UrlInputViewModel.State), changed);
        Assert.Contains(nameof(UrlInputViewModel.ValidVideoId), changed);
        Assert.Contains(nameof(UrlInputViewModel.IsErrorVisible), changed);
    }

    [Fact]
    public void La_disponibilite_de_la_commande_Analyze_est_notifiee()
    {
        var notifications = 0;
        _vm.AnalyzeCommand.CanExecuteChanged += (_, _) => notifications++;

        _vm.Text = ValidUrl;
        var afterValid = notifications;
        _vm.Text = "bonjour";

        Assert.True(afterValid > 0);
        Assert.True(notifications > afterValid);
    }

    // ---------------------------------------------------------------- Bouton « Analyser » (SubmitCommand)

    [Fact]
    public void Le_bouton_Analyser_est_inactif_tant_que_le_champ_est_vide()
    {
        Assert.False(_vm.SubmitCommand.CanExecute(null));
        Assert.False(_vm.IsAnalyzePending);
    }

    [Fact]
    public void Le_bouton_Analyser_reste_cliquable_sur_un_lien_refuse_et_prend_l_aspect_en_attente()
    {
        _vm.Text = "bonjour";

        Assert.True(_vm.SubmitCommand.CanExecute(null));
        Assert.True(_vm.IsAnalyzePending);
    }

    [Fact]
    public void Cliquer_sur_Analyser_avec_un_lien_refuse_montre_l_erreur_sans_analyser()
    {
        var raised = 0;
        _vm.AnalyzeRequested += (_, _) => raised++;
        _vm.Text = "bonjour";
        Assert.False(_vm.IsErrorVisible);

        _vm.SubmitCommand.Execute(null);

        Assert.True(_vm.IsErrorVisible);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Cliquer_sur_Analyser_avec_un_lien_valide_lance_l_analyse_et_n_est_pas_en_attente()
    {
        VideoId? requested = null;
        _vm.AnalyzeRequested += (_, id) => requested = id;
        _vm.Text = ValidUrl;

        Assert.False(_vm.IsAnalyzePending);
        _vm.SubmitCommand.Execute(null);

        Assert.Equal("dQw4w9WgXcQ", requested?.Value);
    }

    [Fact]
    public void Le_bouton_Analyser_est_inactif_pendant_l_analyse()
    {
        _vm.Text = ValidUrl;
        _vm.IsBusy = true;

        Assert.False(_vm.SubmitCommand.CanExecute(null));
    }

    [Fact]
    public void La_disponibilite_du_bouton_Analyser_est_notifiee()
    {
        var notifications = 0;
        _vm.SubmitCommand.CanExecuteChanged += (_, _) => notifications++;

        _vm.Text = "bonjour";

        Assert.True(notifications > 0);
    }

    [Fact]
    public void Des_dependances_nulles_sont_refusees()
    {
        var platform = Substitute.For<IPlatformServices>();

        Assert.Throws<ArgumentNullException>(() => new UrlInputViewModel(null!, platform));
        Assert.Throws<ArgumentNullException>(() => new UrlInputViewModel(new UrlParser(), null!));
    }

    // ---------------------------------------------------------------- Occupation et annulation (M1-10)

    [Fact]
    public void Pendant_l_analyse_Analyze_est_desactivee_et_Cancel_active()
    {
        _vm.Text = ValidUrl;
        Assert.False(_vm.CancelCommand.CanExecute(null));

        _vm.IsBusy = true;

        Assert.False(_vm.AnalyzeCommand.CanExecute(null));
        Assert.True(_vm.CancelCommand.CanExecute(null));
        Assert.False(_vm.IsAnalyzeVisible);
        Assert.False(_vm.IsClearVisible);
    }

    [Fact]
    public void Submit_ne_fait_rien_pendant_l_analyse()
    {
        var raised = 0;
        _vm.AnalyzeRequested += (_, _) => raised++;
        _vm.Text = ValidUrl;
        _vm.IsBusy = true;

        _vm.Submit();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void La_commande_Cancel_declenche_la_demande_d_annulation()
    {
        var raised = 0;
        _vm.CancelRequested += (_, _) => raised++;
        _vm.IsBusy = true;

        _vm.CancelCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Les_boutons_Analyser_et_Effacer_sont_exclusifs_selon_l_erreur()
    {
        Assert.True(_vm.IsAnalyzeVisible);
        Assert.False(_vm.IsClearVisible);

        _vm.Text = "bonjour";
        _vm.RevealError();

        Assert.False(_vm.IsAnalyzeVisible);
        Assert.True(_vm.IsClearVisible);
    }
}
