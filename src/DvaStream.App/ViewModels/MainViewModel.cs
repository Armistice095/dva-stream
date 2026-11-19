using CommunityToolkit.Mvvm.ComponentModel;

namespace DvaStream.App.ViewModels;

/// <summary>Modèle de vue de la fenêtre principale. Coquille du jalon M0 : l'état s'enrichit dès M1.</summary>
public sealed class MainViewModel : ObservableObject
{
    public string Title => "DVA Stream";

    public string Status => "Fondations en place (jalon M0)";
}
