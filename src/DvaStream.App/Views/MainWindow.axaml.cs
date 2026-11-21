using Avalonia.Controls;
using Avalonia.VisualTree;
using DvaStream.App.ViewModels;

namespace DvaStream.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
        PointerPressed += OnPointerPressed;
    }

    // Avalonia ne retire pas le focus d'un champ quand on clique sur une zone vide : sans ceci, l'erreur
    // d'un lien refusé n'apparaîtrait qu'en changeant de fenêtre. IFocusManager.ClearFocus() n'existe plus
    // en Avalonia 12 : on donne le focus au conteneur racine quand le clic ne vise ni un champ de saisie
    // ni un bouton (qui prennent le focus eux-mêmes).
    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.Source is Avalonia.Visual visual
            && visual.GetSelfAndVisualAncestors().Any(v => v is TextBox or Button))
        {
            return;
        }

        RootPanel.Focus();
    }

    // À chaque retour au premier plan, on regarde si le presse-papiers contient un lien à proposer (FR-02).
    private async void OnActivated(object? sender, EventArgs e)
    {
        try
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.UrlInput.CheckClipboardAsync();
            }
        }
        catch (Exception)
        {
            // La détection du presse-papiers est un confort : elle ne doit jamais faire planter l'application.
        }
    }
}
