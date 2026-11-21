using DvaStream.Core.Settings;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>Magasin de paramètres en mémoire pour les tests (enregistre, puis signale le changement).</summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public FakeSettingsStore(AppSettings? initial = null) => Current = initial ?? AppSettings.CreateDefaults("dossier");

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Current { get; private set; }

    public List<AppSettings> Saved { get; } = [];

    public Exception? FailWith { get; set; }

    public int Delay { get; set; }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct)
    {
        if (Delay > 0)
        {
            await Task.Delay(Delay, ct);
        }

        if (FailWith is not null)
        {
            throw FailWith;
        }

        Current = settings;
        Saved.Add(settings);
        Changed?.Invoke(this, settings);
    }

    public void RaiseChanged(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, settings);
    }
}
