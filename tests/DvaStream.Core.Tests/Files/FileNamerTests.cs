using System.Text;
using System.Text.RegularExpressions;
using DvaStream.Core.Files;

namespace DvaStream.Core.Tests.Files;

/// <summary>M4-06 : noms de fichiers sûrs (caractères interdits, noms réservés, Unicode, longueur, conflits).</summary>
public class FileNamerTests
{
    private static string Name(string title, string ext = "mp4", params string[] existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        var namer = new FileNamer(path => taken.Contains(Path.GetFileName(path)));
        return namer.BuildSafeName(title, ext, "dir");
    }

    [Theory]
    [InlineData("Hello", "Hello.mp4")]
    [InlineData("Le résumé Real Madrid vs Celta de Vigo  - La Liga 2026-27 (J13)", "Le résumé Real Madrid vs Celta de Vigo - La Liga 2026-27 (J13).mp4")]
    [InlineData("A: B", "A B.mp4")]
    [InlineData("What? Why*", "What Why.mp4")]
    [InlineData("AC/DC", "AC DC.mp4")]
    [InlineData("a\\b|c", "a b c.mp4")]
    [InlineData("\"Quoted\"", "Quoted.mp4")]
    [InlineData("<tag>", "tag.mp4")]
    [InlineData("tab\there\nnewline", "tab here newline.mp4")]
    [InlineData("  spaces  ", "spaces.mp4")]
    [InlineData("trailing dots...", "trailing dots.mp4")]
    [InlineData("...hidden", "hidden.mp4")]
    [InlineData("a . b", "a . b.mp4")]
    [InlineData("", "video.mp4")]
    [InlineData("   ", "video.mp4")]
    [InlineData("...", "video.mp4")]
    [InlineData("???", "video.mp4")]
    [InlineData("a​b", "ab.mp4")]
    [InlineData("﻿BOM", "BOM.mp4")]
    [InlineData("é", "é.mp4")]
    [InlineData("Super 🎵 mix", "Super 🎵 mix.mp4")]
    [InlineData("日本語のタイトル", "日本語のタイトル.mp4")]
    [InlineData("a\uD800b", "ab.mp4")]
    [InlineData("a b", "a b.mp4")]
    public void Titres_courants_et_pieges(string title, string expected) => Assert.Equal(expected, Name(title));

    [Theory]
    [InlineData("CON", "_CON.mp4")]
    [InlineData("nul", "_nul.mp4")]
    [InlineData("Com1", "_Com1.mp4")]
    [InlineData("LPT9", "_LPT9.mp4")]
    [InlineData("aux.", "_aux.mp4")]
    [InlineData("con.txt", "_con.txt.mp4")]
    [InlineData("CONSOLE", "CONSOLE.mp4")]
    [InlineData("COM10", "COM10.mp4")]
    [InlineData("PRN2", "PRN2.mp4")]
    public void Noms_reserves_windows(string title, string expected) => Assert.Equal(expected, Name(title));

    [Theory]
    [InlineData(".MP4", "x.mp4")]
    [InlineData("m4a", "x.m4a")]
    [InlineData(" Mp3 ", "x.mp3")]
    public void Extension_normalisee(string extension, string expected) => Assert.Equal(expected, Name("x", extension));

    [Theory]
    [InlineData("")]
    [InlineData("mp4!")]
    [InlineData("a/b")]
    [InlineData("trop-longue-extension")]
    public void Extension_invalide_refusee(string extension) =>
        Assert.Throws<ArgumentException>(() => Name("x", extension));

    [Fact]
    public void Un_fichier_existant_n_est_jamais_ecrase()
    {
        Assert.Equal("A (1).mp4", Name("A", "mp4", "A.mp4"));
        Assert.Equal("A (2).mp4", Name("A", "mp4", "A.mp4", "A (1).mp4"));
        Assert.Equal("A (3).mp4", Name("A", "mp4", "A.mp4", "A (1).mp4", "A (2).mp4"));
    }

    [Fact]
    public void Le_conflit_ne_compte_que_pour_la_meme_extension()
    {
        Assert.Equal("A.m4a", Name("A", "m4a", "A.mp4"));
    }

    [Fact]
    public void Titre_tres_long_est_coupe_a_200_octets()
    {
        var name = Name(new string('a', 1000));

        Assert.Equal(new string('a', 200) + ".mp4", name);
    }

    [Fact]
    public void Troncature_en_octets_pour_les_caracteres_larges_sans_couper_un_caractere()
    {
        var japanese = Name(new string('日', 300));
        Assert.Equal(new string('日', 66) + ".mp4", japanese);

        var emoji = Name(string.Concat(Enumerable.Repeat("🎵", 150)));
        Assert.Equal(string.Concat(Enumerable.Repeat("🎵", 50)) + ".mp4", emoji);
        _ = new UTF8Encoding(false, true).GetBytes(emoji); // lève si une paire de jumelles est coupée
    }

    [Fact]
    public void La_troncature_ne_laisse_ni_espace_ni_point_final()
    {
        Assert.Equal(new string('a', 199) + ".mp4", Name(new string('a', 199) + " b"));
        Assert.Equal(new string('a', 199) + ".mp4", Name(new string('a', 199) + ".b"));
    }

    [Fact]
    public void Le_suffixe_de_conflit_garde_le_nom_sous_la_limite_du_systeme()
    {
        var name = Name(new string('a', 1000), "mp4", new string('a', 200) + ".mp4");

        Assert.Equal(new string('a', 200) + " (1).mp4", name);
        Assert.True(Encoding.UTF8.GetByteCount(name) <= 255);
    }

    [Fact]
    public void Un_dossier_tres_long_reduit_le_nom_mais_pas_sous_16_caracteres()
    {
        var namer = new FileNamer(_ => false);
        var folder = new string('d', 240);

        var name = namer.BuildSafeName(new string('a', 100), "mp4", folder);

        Assert.Equal(new string('a', 16) + ".mp4", name);
    }

    [Fact]
    public void Le_chemin_complet_respecte_la_limite_quand_c_est_possible()
    {
        var namer = new FileNamer(_ => false, maxPathLength: 259);
        var folder = new string('d', 100);

        var name = namer.BuildSafeName(new string('a', 500), "mp4", folder);

        Assert.True(folder.Length + 1 + name.Length <= 259, $"chemin de {folder.Length + 1 + name.Length} caractères");
    }

    [Fact]
    public void Le_resultat_est_deterministe()
    {
        Assert.Equal(Name("Titre: test?"), Name("Titre: test?"));
    }

    [Fact]
    public void Plus_de_9999_homonymes_donnent_quand_meme_un_nom_libre()
    {
        var namer = new FileNamer(path => Path.GetFileName(path) is "A.mp4" || Regex.IsMatch(Path.GetFileName(path), @"^A \(\d+\)\.mp4$"));

        var name = namer.BuildSafeName("A", "mp4", "dir");

        Assert.Matches(@"^A [0-9a-f]{32}\.mp4$", name);
    }

    public static TheoryData<string> Nasty() => new()
    {
        "..", "...x...", "CON", "con.", " nul ", "a<b>c:d\"e/f\\g|h?i*j", "\0\u0001\u001F\u007F", "‮evil‬",
        "trailing space ", "tout va bien", "~$temp", "a\r\nb", "𐀀 ok \uDFFF", "COM1.txt", "lpt3 .log",
    };

    [Theory]
    [MemberData(nameof(Nasty))]
    public void Le_resultat_est_toujours_un_nom_de_fichier_valide(string title)
    {
        var stem = FileNamer.Sanitize(title);

        Assert.NotEmpty(stem);
        Assert.DoesNotContain(stem, c => "<>:\"/\\|?*".Contains(c, StringComparison.Ordinal) || c < ' ' || c == '\u007F');
        Assert.False(stem.EndsWith(' ') || stem.EndsWith('.') || stem.StartsWith('.') || stem.StartsWith(' '));
        Assert.DoesNotMatch(@"^(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|\s|$)", stem);
        Assert.Equal(stem.Normalize(NormalizationForm.FormC), stem);
    }
}
