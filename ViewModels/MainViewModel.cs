using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nexus.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(IEnumerable<PageViewModel> pages)
    {
        Pages = pages.ToList();
        Navigate(Pages[0]);
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    [ObservableProperty]
    private PageViewModel? _currentPage;

    [RelayCommand]
    private void Navigate(PageViewModel page)
    {
        if (CurrentPage is not null)
        {
            CurrentPage.IsSelected = false;
        }

        page.IsSelected = true;
        CurrentPage = page;
    }
}