using System.Windows;

namespace Nexus.App.Services;

public enum AppTheme
{
    Dark,
    Light
}

public interface IThemeService
{
    AppTheme Current { get; }
    void Apply(AppTheme theme);
    void Toggle();
}

/// <summary>Remplace le dictionnaire de couleurs actif : l'interface se met à jour en direct.</summary>
public sealed class ThemeService : IThemeService
{
    private ResourceDictionary? _active;

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        var file = theme == AppTheme.Dark ? "Colors.Dark.xaml" : "Colors.Light.xaml";
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{file}", UriKind.Absolute)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_active is not null)
        {
            merged.Remove(_active);
        }

        merged.Add(dictionary);
        _active = dictionary;
        Current = theme;

        foreach (Window window in Application.Current.Windows)
        {
            TitleBarTheme.Apply(window, theme == AppTheme.Dark);
        }
    }

    public void Toggle() => Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
}