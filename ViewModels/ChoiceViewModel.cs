using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.App.ViewModels;

public sealed partial class ChoiceViewModel : ObservableObject
{
    public ChoiceViewModel(string label, object? value = null)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public object? Value { get; }

    /// <summary>Commande exécutée au clic (utilisée par la page Paramètres).</summary>
    public ICommand? Command { get; set; }

    [ObservableProperty]
    private bool _isSelected;
}