using DvaStream.Core.Errors;
using DvaStream.Core.Files;
using DvaStream.Infrastructure.Files;
using NSubstitute;

namespace DvaStream.Infrastructure.Tests.Files;

/// <summary>M4-07 : le fichier final n'apparaît qu'une fois complet, sans jamais écraser un fichier existant.</summary>
public sealed class FileFinalizerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dvastream-fin-{Guid.NewGuid():N}");
    private readonly string _temp;
    private readonly string _dest;

    public FileFinalizerTests()
    {
        _temp = Path.Combine(_root, "temp", "job1");
        _dest = Path.Combine(_root, "dest");
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string MakeSource(string content = "video")
    {
        var path = Path.Combine(_temp, "media.mp4");
        File.WriteAllText(path, content);
        return path;
    }

    private FileFinalizer Real() => new(new FileNamer());

    [Fact]
    public void Deplace_le_fichier_cree_le_dossier_et_ne_laisse_rien_derriere()
    {
        var source = MakeSource();

        var path = Real().MoveToFolder(source, _dest, "Mon: titre", "mp4");

        Assert.Equal(Path.Combine(_dest, "Mon titre.mp4"), path);
        Assert.Equal("video", File.ReadAllText(path));
        Assert.False(File.Exists(source));
        Assert.Equal(new[] { path }, Directory.GetFileSystemEntries(_dest));
    }

    [Fact]
    public void Un_fichier_existant_est_conserve_et_le_nouveau_prend_un_suffixe()
    {
        Directory.CreateDirectory(_dest);
        var existing = Path.Combine(_dest, "Titre.mp4");
        File.WriteAllText(existing, "ancien");

        var path = Real().MoveToFolder(MakeSource("nouveau"), _dest, "Titre", "mp4");

        Assert.Equal(Path.Combine(_dest, "Titre (1).mp4"), path);
        Assert.Equal("ancien", File.ReadAllText(existing));
        Assert.Equal("nouveau", File.ReadAllText(path));
    }

    [Fact]
    public void Deux_telechargements_du_meme_titre_donnent_deux_fichiers()
    {
        var finalizer = Real();

        var first = finalizer.MoveToFolder(MakeSource("un"), _dest, "Titre", "mp4");
        var second = finalizer.MoveToFolder(MakeSource("deux"), _dest, "Titre", "mp4");

        Assert.NotEqual(first, second);
        Assert.Equal("un", File.ReadAllText(first));
        Assert.Equal("deux", File.ReadAllText(second));
    }

    [Fact]
    public void Si_le_nom_est_pris_entre_temps_un_autre_est_choisi_sans_ecraser()
    {
        Directory.CreateDirectory(_dest);
        var taken = Path.Combine(_dest, "pris.mp4");
        File.WriteAllText(taken, "ancien");
        var namer = Substitute.For<IFileNamer>();
        namer.BuildSafeName("t", "mp4", _dest).Returns("pris.mp4", "libre.mp4"); // le premier nom a été pris entre-temps

        var path = new FileFinalizer(namer).MoveToFolder(MakeSource(), _dest, "t", "mp4");

        Assert.Equal(Path.Combine(_dest, "libre.mp4"), path);
        Assert.Equal("ancien", File.ReadAllText(taken));
        Assert.Equal("video", File.ReadAllText(path));
    }

    [Fact]
    public void Source_absente_donne_une_erreur_classee()
    {
        var ex = Assert.Throws<FileFinalizeException>(
            () => Real().MoveToFolder(Path.Combine(_temp, "absent.mp4"), _dest, "t", "mp4"));

        Assert.Equal(ErrorKind.Unknown, ex.Error.Kind);
        Assert.False(Directory.Exists(_dest));
    }

    [Fact]
    public void Un_dossier_de_destination_impossible_donne_FolderNotWritable_et_garde_la_source()
    {
        var blocker = Path.Combine(_root, "fichier");
        File.WriteAllText(blocker, "je suis un fichier");
        var source = MakeSource();

        var ex = Assert.Throws<FileFinalizeException>(() => Real().MoveToFolder(source, blocker, "t", "mp4"));

        Assert.Equal(ErrorKind.FolderNotWritable, ex.Error.Kind);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Un_echec_apres_le_deplacement_remet_le_fichier_a_sa_place_et_ne_laisse_aucun_provisoire()
    {
        var namer = Substitute.For<IFileNamer>();
        namer.BuildSafeName(default!, default!, default!).ReturnsForAnyArgs(_ => throw new IOException("No space left on device"));
        var source = MakeSource("precieux");

        var ex = Assert.Throws<FileFinalizeException>(() => new FileFinalizer(namer).MoveToFolder(source, _dest, "t", "mp4"));

        Assert.Equal(ErrorKind.DiskFull, ex.Error.Kind);
        Assert.Equal("precieux", File.ReadAllText(source));
        Assert.Empty(Directory.GetFileSystemEntries(_dest));
    }

    [Fact]
    public void Le_nom_provisoire_n_existe_plus_apres_un_succes()
    {
        Real().MoveToFolder(MakeSource(), _dest, "t", "mp4");

        Assert.DoesNotContain(Directory.GetFileSystemEntries(_dest), p => p.EndsWith(".part", StringComparison.Ordinal));
    }
}
