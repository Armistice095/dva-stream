using System.Text;
using DvaStream.App.Platform;
using DvaStream.Core.Platform;

namespace DvaStream.App.Tests.Platform;

/// <summary>M4-12 : commandes d'ouverture, d'affichage dans le dossier et de notification pour chaque OS.</summary>
public sealed class ShellCommandsTests
{
    [Fact]
    public void Windows_ouvre_le_fichier_avec_son_application_associee()
    {
        var cmd = ShellCommands.Open(HostOs.Windows, @"D:\Vidéos\a b.mp4");

        Assert.Equal(@"D:\Vidéos\a b.mp4", cmd.FileName);
        Assert.True(cmd.UseShellExecute);
        Assert.Empty(cmd.Arguments);
    }

    [Fact]
    public void Windows_affiche_le_fichier_dans_l_Explorateur_avec_le_chemin_entre_guillemets()
    {
        var cmd = ShellCommands.Reveal(HostOs.Windows, @"D:\Vidéos\a b.mp4");

        Assert.Equal("explorer.exe", cmd.FileName);
        Assert.Equal("/select,\"D:\\Vidéos\\a b.mp4\"", cmd.RawArguments);
        Assert.False(cmd.UseShellExecute);
    }

    [Fact]
    public void MacOs_utilise_open_et_open_R()
    {
        Assert.Equal(["/Users/a/v.mp4"], ShellCommands.Open(HostOs.MacOs, "/Users/a/v.mp4").Arguments);
        Assert.Equal("open", ShellCommands.Open(HostOs.MacOs, "/Users/a/v.mp4").FileName);
        Assert.Equal(["-R", "/Users/a/v.mp4"], ShellCommands.Reveal(HostOs.MacOs, "/Users/a/v.mp4").Arguments);
    }

    [Fact]
    public void Linux_utilise_xdg_open_et_ouvre_le_dossier_pour_afficher()
    {
        var open = ShellCommands.Open(HostOs.Linux, "/home/a/v.mp4");
        var reveal = ShellCommands.Reveal(HostOs.Linux, "/home/a/v.mp4");

        Assert.Equal("xdg-open", open.FileName);
        Assert.Equal(["/home/a/v.mp4"], open.Arguments);
        Assert.Equal("xdg-open", reveal.FileName);
        Assert.Equal(["/home/a"], reveal.Arguments);
    }

    [Fact]
    public void Linux_notifie_avec_notify_send_sans_confondre_un_titre_avec_une_option()
    {
        var cmd = ShellCommands.Notify(HostOs.Linux, "Terminé", "-fichier.mp4");

        Assert.Equal("notify-send", cmd.FileName);
        Assert.Equal(["--app-name=DVA Stream", "--", "Terminé", "-fichier.mp4"], cmd.Arguments);
    }

    [Fact]
    public void MacOs_notifie_avec_osascript_en_echappant_guillemets_et_antislash()
    {
        var cmd = ShellCommands.Notify(HostOs.MacOs, "T", "dit \"a\\b\"");

        Assert.Equal("osascript", cmd.FileName);
        Assert.Equal("display notification \"dit \\\"a\\\\b\\\"\" with title \"T\"", cmd.Arguments[1]);
    }

    [Fact]
    public void Windows_notifie_avec_un_script_PowerShell_encode_qui_double_les_apostrophes()
    {
        var cmd = ShellCommands.Notify(HostOs.Windows, "Terminé", "L'argent’s");

        Assert.Equal("powershell.exe", cmd.FileName);
        var index = cmd.Arguments.ToList().IndexOf("-EncodedCommand");
        Assert.True(index >= 0);
        var script = Encoding.Unicode.GetString(Convert.FromBase64String(cmd.Arguments[index + 1]));
        Assert.Contains("CreateTextNode('L''argent’’s')", script, StringComparison.Ordinal);
        Assert.Contains("CreateTextNode('Terminé')", script, StringComparison.Ordinal);
        Assert.Contains(ShellCommands.WindowsToastAppId, script, StringComparison.Ordinal);
    }
}
