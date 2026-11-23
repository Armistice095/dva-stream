using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DvaStream.App.Localization;
using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;

namespace DvaStream.App.ViewModels;

/// <summary>
/// Carte de téléchargement (M4-11, FR-30 et FR-31) : suit un <see cref="DownloadJob"/> et en présente
/// l'avancement (pourcentage, taille, vitesse, temps restant, étape) dans la langue de l'interface. Les changements
/// du job, qui arrivent d'un autre thread, sont ramenés sur le fil de l'interface.
/// </summary>
public sealed class DownloadViewModel : ObservableObject
{
    private readonly DownloadJob _job;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly bool _canOpenFile;
    private readonly bool _canReveal;

    public DownloadViewModel(
        DownloadJob job, Action cancel, Action retry, Action another, Action? openFile = null, Action? reveal = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(another);

        _job = job;
        CancelCommand = new RelayCommand(cancel, () => CanCancel);
        RetryCommand = new RelayCommand(retry);
        AnotherCommand = new RelayCommand(another);
        _canOpenFile = openFile is not null;
        _canReveal = reveal is not null;
        OpenFileCommand = new RelayCommand(() => openFile?.Invoke(), () => openFile is not null && IsDone);
        RevealCommand = new RelayCommand(() => reveal?.Invoke(), () => reveal is not null && IsDone);
        _job.Changed += (_, _) => RunOnUi(Refresh);
    }

    /// <summary>Annule le téléchargement ; indisponible une fois le fichier en cours d'enregistrement.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>Relance le même téléchargement après un échec.</summary>
    public IRelayCommand RetryCommand { get; }

    /// <summary>Revient à l'accueil pour une autre vidéo.</summary>
    public IRelayCommand AnotherCommand { get; }

    /// <summary>Ouvre le fichier terminé (M4-12).</summary>
    public IRelayCommand OpenFileCommand { get; }

    /// <summary>Affiche le fichier terminé dans son dossier (M4-12).</summary>
    public IRelayCommand RevealCommand { get; }

    /// <summary>Vrai quand le fichier est prêt et que l'ouverture est possible.</summary>
    public bool CanOpenFile => _canOpenFile && IsDone;

    /// <summary>Vrai quand le fichier est prêt et que son affichage dans le dossier est possible.</summary>
    public bool CanReveal => _canReveal && IsDone;

    public DownloadJob Job => _job;

    public string Title => _job.Request.Title;

    public DownloadPhase Phase => _job.Phase;

    public string PhaseText => Strings.Get($"Dl_Phase_{_job.Phase}");

    /// <summary>Avancement global, de 0 à 100.</summary>
    public double Percent => Math.Clamp(_job.Progress?.OverallPercent ?? 0, 0, 100);

    public string PercentText =>
        string.Format(Culture, Strings.Get("Dl_Percent"), ((int)Math.Floor(Percent)).ToString(Culture));

    /// <summary>Barre sans valeur tant que rien n'est mesurable (attente, préparation).</summary>
    public bool IsIndeterminate => _job.Phase is DownloadPhase.Queued or DownloadPhase.Preparing;

    /// <summary>Taille reçue, vitesse et temps restant, séparés par « · » ; vide hors téléchargement.</summary>
    public string StatsText => BuildStats();

    public bool HasStats => StatsText.Length > 0;

    public bool IsRunning => !_job.IsTerminal;

    public bool IsDone => _job.Phase == DownloadPhase.Completed;

    public bool IsFailed => _job.Phase == DownloadPhase.Failed;

    /// <summary>Vrai tant que l'annulation est possible (la finalisation du fichier ne s'annule pas).</summary>
    public bool CanCancel => !_job.IsTerminal && _job.Phase != DownloadPhase.Finalizing;

    public string ErrorMessage => PreviewErrorMessages.MessageFor(_job.Error?.Kind ?? ErrorKind.Unknown);

    public string ErrorAction => PreviewErrorMessages.ActionFor(_job.Error?.Kind ?? ErrorKind.Unknown);

    /// <summary>Nom du fichier enregistré (vide tant que le téléchargement n'est pas terminé).</summary>
    public string FileName => _job.FinalPath is { } path ? Path.GetFileName(path) : string.Empty;

    /// <summary>Dossier du fichier enregistré (vide tant que le téléchargement n'est pas terminé).</summary>
    public string FolderPath => _job.FinalPath is { } path ? Path.GetDirectoryName(path) ?? string.Empty : string.Empty;

    /// <summary>« Enregistré dans … » une fois le fichier prêt, sinon vide.</summary>
    public string SavedToText => FolderPath.Length == 0 ? string.Empty : string.Format(Culture, Strings.Get("Dl_SavedTo"), FolderPath);

    private static CultureInfo Culture => Strings.Culture ?? CultureInfo.CurrentUICulture;

    private string BuildStats()
    {
        if (_job.Progress is not { } p || _job.IsTerminal || p.BytesDone <= 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(3);
        parts.Add(p.BytesTotal is > 0 and var total
            ? string.Format(Culture, Strings.Get("Dl_Stats_Size"), SizeFormatter.Format(p.BytesDone), SizeFormatter.Format(total))
            : SizeFormatter.Format(p.BytesDone));

        if (p.BytesPerSecond is > 0 and var speed)
        {
            parts.Add(SizeFormatter.FormatSpeed(speed));
        }

        if (p.Remaining is { } remaining && DurationFormatter.Format(remaining) is { } text)
        {
            parts.Add(string.Format(Culture, Strings.Get("Dl_Stats_Remaining"), text));
        }

        return string.Join(" · ", parts);
    }

    private void Refresh()
    {
        OnPropertyChanged(string.Empty);
        CancelCommand.NotifyCanExecuteChanged();
        OpenFileCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
    }

    private void RunOnUi(Action action)
    {
        if (_ui is null)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }
}
