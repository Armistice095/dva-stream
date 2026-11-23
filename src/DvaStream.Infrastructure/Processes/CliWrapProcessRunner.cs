using System.ComponentModel;
using System.Text;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.Infrastructure.Processes;

/// <summary>Implémentation de <see cref="IProcessRunner"/> avec CliWrap (ADR-08, D-006).</summary>
public sealed class CliWrapProcessRunner : IProcessRunner
{
    private readonly ILogger<CliWrapProcessRunner> _logger;

    public CliWrapProcessRunner(ILogger<CliWrapProcessRunner>? logger = null) =>
        _logger = logger ?? NullLogger<CliWrapProcessRunner>.Instance;

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Executable);
        ArgumentNullException.ThrowIfNull(spec.Arguments);

        using var timeoutSource = spec.Timeout is { } timeout
            ? new CancellationTokenSource(timeout)
            : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutSource.Token);

        var command = Cli.Wrap(spec.Executable)
            .WithArguments(spec.Arguments)
            .WithValidation(CommandResultValidation.None);
        if (spec.WorkingDirectory is not null)
        {
            command = command.WithWorkingDirectory(spec.WorkingDirectory);
        }

        if (spec.Environment is { Count: > 0 } environment)
        {
            command = command.WithEnvironmentVariables(builder =>
            {
                foreach (var (key, value) in environment)
                {
                    builder.Set(key, value);
                }
            });
        }

        var name = Path.GetFileName(spec.Executable);
        try
        {
            ProcessResult processResult;
            if (spec.OnOutputLine is { } onLine)
            {
                // Lecture ligne à ligne de la sortie standard (progression) ; la sortie complète est aussi gardée.
                var standardOutput = new StringBuilder();
                var standardError = new StringBuilder();
                var streaming = command
                    .WithStandardOutputPipe(PipeTarget.Merge(
                        PipeTarget.ToStringBuilder(standardOutput, Encoding.UTF8),
                        PipeTarget.ToDelegate(line => SafeInvoke(onLine, line), Encoding.UTF8)))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(standardError, Encoding.UTF8));
                var streamed = await streaming.ExecuteAsync(linked.Token).ConfigureAwait(false);
                processResult = new ProcessResult(streamed.ExitCode, standardOutput.ToString(), standardError.ToString());
            }
            else
            {
                var result = await command.ExecuteBufferedAsync(Encoding.UTF8, linked.Token).ConfigureAwait(false);
                processResult = new ProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
            }

            _logger.LogDebug("Processus {Executable} terminé avec le code {ExitCode}.", name, processResult.ExitCode);
            return processResult;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Processus {Executable} arrêté : délai de {Timeout} dépassé.", name, spec.Timeout);
            throw new TimeoutException($"Le processus « {name} » a dépassé le délai de {spec.Timeout}.");
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogError(ex, "Processus {Executable} impossible à démarrer.", name);
            throw new ProcessLaunchException($"Impossible de démarrer « {name} » : {ex.Message}", ex);
        }
    }

    private void SafeInvoke(Action<string> handler, string line)
    {
        try
        {
            handler(line);
        }
        catch (Exception ex)
        {
            // Un rappel défaillant ne doit pas interrompre le processus ni la lecture de sa sortie.
            _logger.LogWarning(ex, "Le rappel de lecture de la sortie a échoué.");
        }
    }
}
