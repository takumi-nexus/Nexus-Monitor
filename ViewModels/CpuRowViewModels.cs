using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.App.ViewModels;

public sealed partial class LogicalRowViewModel : ObservableObject
{
    public LogicalRowViewModel(int index) => Label = $"Thread {index}";

    public string Label { get; }

    [ObservableProperty]
    private string _usage = "Non disponible";
}

public sealed partial class CoreRowViewModel : ObservableObject
{
    public CoreRowViewModel(int index) => Label = $"Cœur {index}";

    public string Label { get; }

    [ObservableProperty]
    private string _temperature = "Non disponible";

    [ObservableProperty]
    private string _frequency = "Non disponible";
}