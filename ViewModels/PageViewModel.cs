using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.App.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    protected PageViewModel(string title, string iconGlyph, string description)
    {
        Title = title;
        IconGlyph = iconGlyph;
        Description = description;
    }

    public string Title { get; }
    public string IconGlyph { get; }
    public string Description { get; }

    [ObservableProperty]
    private bool _isSelected;
}