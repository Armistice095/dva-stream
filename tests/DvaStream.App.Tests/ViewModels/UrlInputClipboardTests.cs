using System.Globalization;
using DvaStream.App.Localization;
using DvaStream.App.ViewModels;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using NSubstitute;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M1-09 (FR-02) : le lien du presse-papiers est proposé, jamais appliqué automatiquement.</summary>
[Collection(CultureCollection.Name)]
public sealed class UrlInputClipboardTests : IDisposable
{
    private const string ClipboardUrl = "https://www.youtube.com/watch?v=aB3dE5fGh7k";

    private readonly CultureInfo? _previousCulture = Strings.Culture;
    private readonly IPlatformServices _platform = Substitute.For<IPlatformServices>();
    private readonly UrlInputViewModel _vm;

    public UrlInputClipboardTests()
    {
        Strings.Culture = new CultureInfo("fr");
        _vm = new UrlInputViewModel(new UrlParser(), _platform);
    }

    public void Dispose() => Strings.Culture = _previousCulture;

    private void SetClipboard(string? content) =>
        _platform.ReadClipboardAsync().Returns(Task.FromResult(content));

    [Fact]
    public async Task Une_url_valide_dans_le_presse_papiers_est_proposee_sans_toucher_au_champ()
    {
        SetClipboard(ClipboardUrl);

        await _vm.CheckClipboardAsync();

        Assert.True(_vm.HasClipboardSuggestion);
        Assert.Equal(ClipboardUrl, _vm.ClipboardSuggestion);
        Assert.Equal(string.Empty, _vm.Text);          // jamais appliquée automatiquement
        Assert.Equal(UrlInputState.Empty, _vm.State);
    }

    [Fact]
    public async Task Le_lien_propose_est_affiche_sans_schema_ni_www()
    {
        SetClipboard(ClipboardUrl);

        await _vm.CheckClipboardAsync();

        Assert.Equal("youtube.com/watch?v=aB3dE5fGh7k", _vm.ClipboardSuggestionDisplay);
    }

    [Fact]
    public async Task Les_espaces_autour_du_lien_sont_ignores()
    {
        SetClipboard("  " + ClipboardUrl + "\r\n");

        await _vm.CheckClipboardAsync();

        Assert.Equal(ClipboardUrl, _vm.ClipboardSuggestion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bonjour tout le monde")]
    [InlineData("https://evil.com/watch?v=aB3dE5fGh7k")]
    [InlineData("https://www.youtube.com/playlist?list=PLabc123")]
    [InlineData("https://www.youtube.com/watch?v=court")]
    public async Task Un_contenu_qui_n_est_pas_une_url_de_video_valide_n_est_pas_propose(string? content)
    {
        SetClipboard(content);

        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
        Assert.Null(_vm.ClipboardSuggestion);
        Assert.Null(_vm.ClipboardSuggestionDisplay);
    }

    [Fact]
    public async Task Un_presse_papiers_illisible_ne_propose_rien_et_ne_leve_pas_d_exception()
    {
        _platform.ReadClipboardAsync().Returns(Task.FromResult<string?>(null));

        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task La_proposition_disparait_si_le_presse_papiers_ne_contient_plus_de_lien()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();
        Assert.True(_vm.HasClipboardSuggestion);

        SetClipboard("autre chose");
        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Utiliser_le_lien_remplit_le_champ_et_masque_la_proposition()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();

        _vm.UseSuggestionCommand.Execute(null);

        Assert.Equal(ClipboardUrl, _vm.Text);
        Assert.Equal(UrlInputState.Valid, _vm.State);
        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Le_lien_utilise_n_est_pas_repropose_au_prochain_controle()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();
        _vm.UseSuggestionCommand.Execute(null);
        _vm.ClearCommand.Execute(null);

        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Ignorer_masque_la_proposition_et_elle_n_est_pas_repropose_pour_le_meme_lien()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();

        _vm.DismissSuggestionCommand.Execute(null);
        Assert.False(_vm.HasClipboardSuggestion);
        Assert.Equal(string.Empty, _vm.Text);

        await _vm.CheckClipboardAsync();
        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Un_nouveau_lien_est_propose_apres_avoir_ignore_le_precedent()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();
        _vm.DismissSuggestionCommand.Execute(null);

        SetClipboard("https://youtu.be/dQw4w9WgXcQ");
        await _vm.CheckClipboardAsync();

        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", _vm.ClipboardSuggestion);
    }

    [Fact]
    public async Task Un_lien_deja_present_dans_le_champ_n_est_pas_propose()
    {
        _vm.Text = ClipboardUrl;
        SetClipboard(ClipboardUrl);

        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task La_proposition_disparait_quand_l_utilisateur_saisit_lui_meme_ce_lien()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();

        _vm.Text = ClipboardUrl;

        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Rien_n_est_lu_quand_la_detection_est_desactivee()
    {
        _vm.IsClipboardDetectionEnabled = false;
        SetClipboard(ClipboardUrl);

        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
        await _platform.DidNotReceive().ReadClipboardAsync();
    }

    [Fact]
    public async Task Desactiver_la_detection_retire_la_proposition_en_cours()
    {
        SetClipboard(ClipboardUrl);
        await _vm.CheckClipboardAsync();

        _vm.IsClipboardDetectionEnabled = false;

        Assert.False(_vm.HasClipboardSuggestion);
    }

    [Fact]
    public async Task Rien_n_est_lu_pendant_une_analyse()
    {
        _vm.IsBusy = true;
        SetClipboard(ClipboardUrl);

        await _vm.CheckClipboardAsync();

        Assert.False(_vm.HasClipboardSuggestion);
        await _platform.DidNotReceive().ReadClipboardAsync();
    }

    [Fact]
    public async Task Une_lecture_depassee_par_une_plus_recente_est_ignoree()
    {
        var slow = new TaskCompletionSource<string?>();
        _platform.ReadClipboardAsync().Returns(slow.Task, Task.FromResult<string?>("https://youtu.be/dQw4w9WgXcQ"));

        var first = _vm.CheckClipboardAsync();     // bloquée sur la lecture lente
        await _vm.CheckClipboardAsync();           // plus récente : propose dQw4w9WgXcQ
        slow.SetResult(ClipboardUrl);              // la lecture ancienne se termine après
        await first;

        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", _vm.ClipboardSuggestion);
    }
}
