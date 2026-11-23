using DvaStream.Infrastructure.YtDlp;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>M4-04 : lecture des lignes de progression, sur des cas écrits et sur de vraies sorties (spike S2).</summary>
public class YtDlpProgressParserTests
{
    public static TheoryData<string> RealOutputs() => new() { "direct-short.txt", "direct-long.txt" };

    private static string[] Lines(string file) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "progress", file));

    [Fact]
    public void Les_gabarits_ont_le_nombre_de_champs_attendu_par_l_analyseur()
    {
        Assert.Equal(9, YtDlpProgressParser.DownloadTemplate.Split('|').Length);
        Assert.Equal(3, YtDlpProgressParser.PostprocessTemplate.Split('|').Length);
        Assert.StartsWith(YtDlpProgressParser.DownloadPrefix + "|", YtDlpProgressParser.DownloadTemplate, StringComparison.Ordinal);
        Assert.StartsWith(YtDlpProgressParser.PostprocessPrefix + "|", YtDlpProgressParser.PostprocessTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void Ligne_de_progression_complete()
    {
        var line = Assert.IsType<DownloadLine>(
            YtDlpProgressParser.Parse("DVAPROG|downloading|1024|20086214|NA|569171.3882851842|35|NA|NA"));

        Assert.Equal(YtDlpDownloadStatus.Downloading, line.Status);
        Assert.Equal(1024, line.DownloadedBytes);
        Assert.Equal(20086214, line.TotalBytes);
        Assert.Null(line.TotalBytesEstimate);
        Assert.Equal(569171.3882851842, line.BytesPerSecond);
        Assert.Equal(35, line.EtaSeconds);
        Assert.Null(line.FragmentIndex);
        Assert.Equal(20086214, line.KnownTotal);
    }

    [Fact]
    public void Vitesse_et_duree_restante_absentes_donnent_null()
    {
        var line = Assert.IsType<DownloadLine>(
            YtDlpProgressParser.Parse("DVAPROG|downloading|10100034|20086214|NA|NA|NA|NA|NA"));

        Assert.Null(line.BytesPerSecond);
        Assert.Null(line.EtaSeconds);
        Assert.Equal(10100034, line.DownloadedBytes);
    }

    [Fact]
    public void Flux_fragmente_avec_taille_estimee()
    {
        var line = Assert.IsType<DownloadLine>(
            YtDlpProgressParser.Parse("DVAPROG|downloading|5000|NA|90000|1.5e+06|12.5|3|40"));

        Assert.Null(line.TotalBytes);
        Assert.Equal(90000, line.KnownTotal);
        Assert.Equal(1_500_000, line.BytesPerSecond);
        Assert.Equal(12.5, line.EtaSeconds);
        Assert.Equal(3, line.FragmentIndex);
        Assert.Equal(40, line.FragmentCount);
    }

    [Theory]
    [InlineData("finished", YtDlpDownloadStatus.Finished)]
    [InlineData("error", YtDlpDownloadStatus.Error)]
    [InlineData("quelquechose", YtDlpDownloadStatus.Unknown)]
    public void Statuts(string status, YtDlpDownloadStatus expected)
    {
        var line = Assert.IsType<DownloadLine>(YtDlpProgressParser.Parse($"DVAPROG|{status}|10|10|NA|NA|NA|NA|NA"));
        Assert.Equal(expected, line.Status);
    }

    [Fact]
    public void Post_traitements()
    {
        Assert.Equal(new PostprocessLine("Merger", false), YtDlpProgressParser.Parse("DVAPOST|started|Merger"));
        Assert.Equal(new PostprocessLine("MoveFiles", true), YtDlpProgressParser.Parse("DVAPOST|finished|MoveFiles"));
        Assert.Null(YtDlpProgressParser.Parse("DVAPOST|started|"));
        Assert.Null(YtDlpProgressParser.Parse("DVAPOST|bizarre|Merger"));
    }

    [Fact]
    public void Annonce_des_flux_et_nouveau_fichier()
    {
        var formats = Assert.IsType<FormatsAnnouncedLine>(
            YtDlpProgressParser.Parse("[info] p1FpH_adikQ: Downloading 1 format(s): 396+251-1"));
        Assert.Equal(new[] { "396", "251-1" }, formats.FormatIds);

        var single = Assert.IsType<FormatsAnnouncedLine>(
            YtDlpProgressParser.Parse("[info] abc: Downloading 1 format(s): 18"));
        Assert.Single(single.FormatIds);

        var dest = Assert.IsType<DestinationLine>(
            YtDlpProgressParser.Parse(@"[download] Destination: C:\Temp\media.f396.mp4"));
        Assert.Equal(@"C:\Temp\media.f396.mp4", dest.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[youtube] Extracting URL: https://www.youtube.com/watch?v=abc")]
    [InlineData("[Merger] Merging formats into \"x.mp4\"")]
    [InlineData("Deleting original file x.mp4 (pass -k to keep)")]
    [InlineData("[info] abc: Downloading 1 format(s):")]
    [InlineData("[download] Destination:")]
    [InlineData("DVAPROG|downloading|abc|10|NA|NA|NA|NA|NA")]
    [InlineData("DVAPROG|downloading|-5|10|NA|NA|NA|NA|NA")]
    [InlineData("DVAPROG|downloading|10")]
    [InlineData("DVAPROG|downloading|99999999999999999999999|10|NA|NA|NA|NA|NA")]
    public void Ce_qui_n_est_pas_de_la_progression_donne_null_sans_exception(string? line) =>
        Assert.Null(YtDlpProgressParser.Parse(line));

    [Theory]
    [MemberData(nameof(RealOutputs))]
    public void Sur_une_vraie_sortie_chaque_ligne_du_gabarit_est_comprise(string file)
    {
        var lines = Lines(file);
        var template = lines.Where(l => l.StartsWith("DVAPROG|", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(template);
        Assert.All(template, l => Assert.IsType<DownloadLine>(YtDlpProgressParser.Parse(l)));
    }

    [Theory]
    [MemberData(nameof(RealOutputs))]
    public void Sur_une_vraie_sortie_deux_flux_se_suivent_chacun_croissant_et_termine(string file)
    {
        var streams = new List<List<DownloadLine>>();
        foreach (var parsed in Lines(file).Select(YtDlpProgressParser.Parse))
        {
            switch (parsed)
            {
                case DestinationLine:
                    streams.Add([]);
                    break;
                case DownloadLine d:
                    streams[^1].Add(d);
                    break;
            }
        }

        Assert.Equal(2, streams.Count);
        foreach (var stream in streams)
        {
            var downloaded = stream.Select(l => l.DownloadedBytes).ToList();
            Assert.Equal(downloaded.OrderBy(x => x), downloaded);

            var last = stream[^1];
            Assert.Equal(YtDlpDownloadStatus.Finished, last.Status);
            Assert.Equal(last.TotalBytes, last.DownloadedBytes);
            Assert.All(stream, l => Assert.NotNull(l.TotalBytes));
        }
    }

    [Theory]
    [MemberData(nameof(RealOutputs))]
    public void Sur_une_vraie_sortie_les_flux_sont_annonces_et_les_post_traitements_ordonnes(string file)
    {
        var parsed = Lines(file).Select(YtDlpProgressParser.Parse).Where(p => p is not null).ToList();

        var announced = Assert.Single(parsed.OfType<FormatsAnnouncedLine>());
        Assert.Equal(2, announced.FormatIds.Count);

        Assert.Equal(
            new[]
            {
                new PostprocessLine("Merger", false), new PostprocessLine("Merger", true),
                new PostprocessLine("MoveFiles", false), new PostprocessLine("MoveFiles", true),
            },
            parsed.OfType<PostprocessLine>().ToArray());
    }
}
