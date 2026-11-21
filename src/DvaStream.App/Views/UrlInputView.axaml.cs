using Avalonia.Controls;
using DvaStream.App.ViewModels;

namespace DvaStream.App.Views;

public partial class UrlInputView : UserControl
{
    public UrlInputView()
    {
        InitializeComponent();

        // Quitter le champ : on peut maintenant dire pourquoi le lien est refusé (pas pendant la frappe).
        UrlBox.LostFocus += (_, _) => (DataContext as UrlInputViewModel)?.RevealError();
    }
}
