using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nexus.App.ViewModels;

public sealed partial class ChartSettings : ObservableObject
{
    public ChartSettings()
    {
        Options =
        [
            new("1 min", TimeSpan.FromMinutes(1)),
            new("5 min", TimeSpan.FromMinutes(5)),
            new("15 min", TimeSpan.FromMinutes(15)),
            new("1 h", TimeSpan.FromHours(1))
        ];

        Options[0].IsSelected = true;
    }

    public IReadOnlyList<ChoiceViewModel> Options { get; }

    [ObservableProperty]
    private TimeSpan _period = TimeSpan.FromMinutes(1);

    [RelayCommand]
    private void Select(ChoiceViewModel option)
    {
        foreach (var choice in Options)
        {
            choice.IsSelected = ReferenceEquals(choice, option);
        }

        Period = (TimeSpan)option.Value!;
    }
}