using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.App.ViewModels;

public sealed partial class MetricRowViewModel : ObservableObject
{
    public MetricRowViewModel(string label, string toolTip)
    {
        Label = label;
        ToolTip = toolTip;
    }

    public string Label { get; }
    public string ToolTip { get; }

    [ObservableProperty]
    private string _value = "Non disponible";
}