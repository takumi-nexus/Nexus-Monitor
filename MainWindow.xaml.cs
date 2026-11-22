using System.Windows;
using Nexus.App.Services;
using Nexus.App.ViewModels;

namespace Nexus.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Barre de titre assortie au thème, dès que la fenêtre existe
        SourceInitialized += (_, _) =>
            TitleBarTheme.Apply(this, themeService.Current == AppTheme.Dark);

        // Fenêtre réduite : on suspend l'affichage
        // (la collecte et l'historique continuent)
        StateChanged += (_, _) =>
            UiActivity.SetActive(WindowState != WindowState.Minimized);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
