namespace DvaStream.Infrastructure.Processes;

/// <summary>Description d'un processus à lancer. Les arguments sont une <b>liste</b> : jamais de ligne de commande à assembler (TDD §12).</summary>
/// <param name="Executable">Chemin ou nom de l'exécutable.</param>
/// <param name="Arguments">Arguments, un par élément ; aucun interprète de commandes n'est utilisé.</param>
/// <param name="Timeout">Durée maximale ; <c>null</c> = pas de limite.</param>
/// <param name="WorkingDirectory">Dossier de travail ; <c>null</c> = celui de l'application.</param>
public sealed record ProcessSpec(
    string Executable,
    IReadOnlyList<string> Arguments,
    TimeSpan? Timeout = null,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? Environment = null);

/// <summary>Résultat d'un processus terminé. Un code de sortie non nul n'est pas une exception : c'est à l'appelant d'en juger.</summary>
/// <param name="ExitCode">Code de sortie.</param>
/// <param name="StandardOutput">Sortie standard complète (UTF-8).</param>
/// <param name="StandardError">Sortie d'erreur complète (UTF-8).</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Le processus n'a pas pu démarrer (exécutable introuvable, droits insuffisants...).</summary>
public sealed class ProcessLaunchException : Exception
{
    public ProcessLaunchException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Lance un processus externe (yt-dlp, FFmpeg) et attend sa fin (M2-04, TDD §12). Les sorties sont lues en
/// parallèle, donc un processus bavard ne se bloque pas ; l'annulation et le délai maximal arrêtent le processus
/// <b>et ses enfants</b>.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Lance le processus et renvoie son résultat.
    /// </summary>
    /// <exception cref="ProcessLaunchException">Le processus n'a pas pu démarrer.</exception>
    /// <exception cref="TimeoutException"><see cref="ProcessSpec.Timeout"/> dépassé (le processus a été arrêté).</exception>
    /// <exception cref="OperationCanceledException">Annulation demandée par l'appelant (le processus a été arrêté).</exception>
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct);
}
