using System.Diagnostics;
using DvaStream.Infrastructure.Processes;

namespace DvaStream.Infrastructure.Tests.Processes;

/// <summary>M2-04 : exécuteur de processus (arguments en liste, sorties, code de sortie, annulation, délai).</summary>
public class CliWrapProcessRunnerTests
{
    private readonly CliWrapProcessRunner _runner = new();

    [Fact]
    public async Task Renvoie_la_sortie_standard_et_le_code_zero()
    {
        var result = await _runner.RunAsync(TestProcessHost.Spec("echo", "bonjour"), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("bonjour", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task Les_arguments_sont_transmis_tels_quels_sans_interpretation()
    {
        // Espaces, guillemets, esperluette, point-virgule, dollar : un interprète de commandes les altérerait.
        string[] arguments = ["un mot", "il a dit \"ok\"", "a&b", "c;d", "$HOME", "--flag=valeur", "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=1"];

        var result = await _runner.RunAsync(TestProcessHost.Spec("echo", arguments), CancellationToken.None);

        Assert.Equal(string.Join("|", arguments), result.StandardOutput);
    }

    [Fact]
    public async Task La_sortie_d_erreur_est_capturee_separement()
    {
        var result = await _runner.RunAsync(TestProcessHost.Spec("stderr", "problème", "ici"), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal("problème ici", result.StandardError);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(42)]
    public async Task Un_code_de_sortie_non_nul_est_renvoye_sans_exception(int code)
    {
        var result = await _runner.RunAsync(TestProcessHost.Spec("exit", code.ToString(System.Globalization.CultureInfo.InvariantCulture)), CancellationToken.None);

        Assert.Equal(code, result.ExitCode);
    }

    [Fact]
    public async Task Les_accents_et_symboles_sont_decodes_en_utf8()
    {
        var result = await _runner.RunAsync(TestProcessHost.Spec("unicode"), CancellationToken.None);

        Assert.Equal("é✓日本", result.StandardOutput);
    }

    [Fact]
    public async Task Les_variables_d_environnement_demandees_sont_transmises()
    {
        var spec = TestProcessHost.Spec("env", "DVASTREAM_TEST_VAR") with
        {
            Environment = new Dictionary<string, string?> { ["DVASTREAM_TEST_VAR"] = "valeur é" },
        };

        var result = await _runner.RunAsync(spec, CancellationToken.None);

        Assert.Equal("valeur é", result.StandardOutput);
    }

    [Fact]
    public async Task Un_processus_tres_bavard_ne_se_bloque_pas()
    {
        const int lines = 3000;   // ~300 Ko sur chaque sortie, bien au-delà des tampons de tube

        var result = await _runner.RunAsync(
            TestProcessHost.SpecWith(TimeSpan.FromSeconds(60), null, "flood", lines.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(lines, result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(lines, result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Le_dossier_de_travail_est_respecte()
    {
        var folder = Path.Combine(Path.GetTempPath(), "dva-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var result = await _runner.RunAsync(TestProcessHost.SpecWith(null, folder, "cwd"), CancellationToken.None);

            Assert.EndsWith(Path.GetFileName(folder), result.StandardOutput.Trim(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Un_delai_depasse_arrete_le_processus_et_leve_TimeoutException()
    {
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            _runner.RunAsync(TestProcessHost.SpecWith(TimeSpan.FromMilliseconds(700), null, "sleep", "60000"), CancellationToken.None));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Le processus n'a pas été arrêté ({stopwatch.Elapsed}).");
    }

    [Fact]
    public async Task L_annulation_arrete_le_processus_et_leve_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        var running = _runner.RunAsync(TestProcessHost.Spec("sleep", "60000"), cts.Token);
        await Task.Delay(700);
        await cts.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.IsNotType<TimeoutException>(exception);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Le processus n'a pas été arrêté ({stopwatch.Elapsed}).");
    }

    [Fact]
    public async Task Un_jeton_deja_annule_leve_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _runner.RunAsync(TestProcessHost.Spec("echo", "x"), cts.Token));
    }

    [Fact]
    public async Task Un_executable_introuvable_leve_ProcessLaunchException()
    {
        var spec = new ProcessSpec(Path.Combine(Path.GetTempPath(), "dva-inexistant-" + Guid.NewGuid().ToString("N")), []);

        await Assert.ThrowsAsync<ProcessLaunchException>(() => _runner.RunAsync(spec, CancellationToken.None));
    }

    [Fact]
    public async Task Des_parametres_invalides_sont_refuses()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.RunAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => _runner.RunAsync(new ProcessSpec(" ", []), CancellationToken.None));
    }

    [Fact]
    public async Task Les_lignes_de_la_sortie_standard_sont_transmises_une_a_une_et_la_sortie_complete_est_gardee()
    {
        var file = Path.Combine(Path.GetTempPath(), $"dva-lines-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(file, "un\ndeux é\ntrois\n");
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        try
        {
            var spec = TestProcessHost.Spec("replay", file, "-", "0") with { OnOutputLine = lines.Enqueue };

            var result = await _runner.RunAsync(spec, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(new[] { "un", "deux é", "trois" }, lines.ToArray());
            Assert.Equal("un\ndeux é\ntrois\n", result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Un_rappel_qui_leve_une_exception_n_interrompt_pas_le_processus()
    {
        var spec = TestProcessHost.Spec("echo", "bonjour") with { OnOutputLine = _ => throw new InvalidOperationException("boom") };

        var result = await _runner.RunAsync(spec, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("bonjour", result.StandardOutput);
    }

    [Fact]
    public async Task Avec_lecture_des_lignes_le_code_de_sortie_et_stderr_restent_disponibles()
    {
        var out1 = Path.Combine(Path.GetTempPath(), $"dva-o-{Guid.NewGuid():N}.txt");
        var err1 = Path.Combine(Path.GetTempPath(), $"dva-e-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(out1, "a\n");
        await File.WriteAllTextAsync(err1, "ERROR: raté");
        try
        {
            var spec = TestProcessHost.Spec("replay", out1, err1, "3") with { OnOutputLine = _ => { } };

            var result = await _runner.RunAsync(spec, CancellationToken.None);

            Assert.Equal(3, result.ExitCode);
            Assert.Equal("ERROR: raté", result.StandardError);
        }
        finally
        {
            File.Delete(out1);
            File.Delete(err1);
        }
    }
}
