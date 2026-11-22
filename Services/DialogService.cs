using System.Windows;
using Microsoft.Win32;

namespace Nexus.App.Services;

public interface IDialogService
{
    string? PickCsvSavePath(string defaultFileName);
    bool Confirm(string title, string message);
    void ShowInfo(string title, string message);
    void ShowError(string title, string message);
}

public sealed class DialogService : IDialogService
{
    public string? PickCsvSavePath(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter l'historique",
            FileName = defaultFileName,
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            Filter = "Fichier CSV (*.csv)|*.csv"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
        == MessageBoxResult.Yes;

    public void ShowInfo(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}