using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Nexus.App.Services;
using Nexus.Core.Abstractions;
using Nexus.Core.Diagnostics;
using Nexus.Core.Models;
using Nexus.Infrastructure.Sensors;
using System.IO;

namespace Nexus.App.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    private static readonly IReadOnlyList<(string Label, bool Value)> OnOff = [("Activé", true), ("Désactivé", false)];

    private readonly ISettingsService _settings;
    private readonly SettingsApplier _applier;
    private readonly IHistoryStore _store;
    private readonly IHistoryRecorder _recorder;
    private readonly IDialogService _dialogs;

    public SettingsViewModel(
        ISettingsService settings,
        SettingsApplier applier,
        IHistoryStore store,
        IHistoryRecorder recorder,
        IDialogService dialogs)
        : base("Paramètres", "\uE713", "Thème, intervalles et conservation des données")
    {
        _settings = settings;
        _applier = applier;
        _store = store;
        _recorder = recorder;
        _dialogs = dialogs;

        var current = settings.Current;

        ThemeChoices = BuildChoices(SettingsChoices.Themes, current.Theme,
            value => Change(s => s with { Theme = value }));
        RefreshChoices = BuildChoices(SettingsChoices.RefreshIntervalsMs, current.RefreshIntervalMs,
            value => Change(s => s with { RefreshIntervalMs = value }));
        RecordingEnabledChoices = BuildChoices(OnOff, current.RecordingEnabled,
            value => Change(s => s with { RecordingEnabled = value }));
        RecordingIntervalChoices = BuildChoices(SettingsChoices.RecordingIntervalsSeconds, current.RecordingIntervalSeconds,
            value => Change(s => s with { RecordingIntervalSeconds = value }));
        RetentionChoices = BuildChoices(SettingsChoices.RetentionDays, current.RetentionDays, ChangeRetentionAsync);
        StartupChoices = BuildChoices(OnOff, current.StartWithWindows,
            value => Change(s => s with { StartWithWindows = value }));

        IsAdministrator = PrivilegeHelper.IsAdministrator();
        CanRestartAsAdministrator = !IsAdministrator;
        AdminStatusText = IsAdministrator
            ? "Nexus s'exécute avec les droits administrateur : toutes les mesures disponibles sur ce matériel sont lues."
            : "Nexus s'exécute sans droits administrateur : les températures, la tension, la puissance et les fréquences par cœur du processeur ne sont pas lisibles. Ces mesures nécessitent aussi le pilote PawnIO.";

        DataFolder = AppPaths.DataFolder;
        VersionText = $"NEXUS Hardware Monitor • version {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3)}";
    }

    public IReadOnlyList<ChoiceViewModel> ThemeChoices { get; }
    public IReadOnlyList<ChoiceViewModel> RefreshChoices { get; }
    public IReadOnlyList<ChoiceViewModel> RecordingEnabledChoices { get; }
    public IReadOnlyList<ChoiceViewModel> RecordingIntervalChoices { get; }
    public IReadOnlyList<ChoiceViewModel> RetentionChoices { get; }
    public IReadOnlyList<ChoiceViewModel> StartupChoices { get; }

    public bool IsAdministrator { get; }
    public bool CanRestartAsAdministrator { get; }
    public string AdminStatusText { get; }
    public string DataFolder { get; }
    public string VersionText { get; }

    [RelayCommand]
    private void RestartAsAdministrator()
    {
        if (ElevationService.StartElevatedInstance())
        {
            Application.Current.Shutdown();
        }
        else
        {
            _dialogs.ShowInfo("Relance annulée", "La relance en administrateur a été annulée ou a échoué. Nexus reste ouvert.");
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DataFolder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Ouverture du dossier des données", ex);
            _dialogs.ShowError("Dossier introuvable", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        if (!_store.IsAvailable)
        {
            _dialogs.ShowError("Export impossible", _store.Error ?? "L'historique est indisponible.");
            return;
        }

        var path = _dialogs.PickCsvSavePath($"nexus-export-complet-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            await _recorder.FlushAsync();
            var count = await _store.ExportCsvAsync(path, DateTimeOffset.UnixEpoch, DateTimeOffset.Now);
            _dialogs.ShowInfo("Export terminé", $"{count:N0} lignes exportées vers :\n{path}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export complet", ex);
            _dialogs.ShowError("Export impossible", ex.Message);
        }
    }

    private Task Change(Func<AppSettings, AppSettings> mutate)
    {
        _settings.Update(mutate);
        _applier.Apply(_settings.Current);
        return Task.CompletedTask;
    }

    private async Task ChangeRetentionAsync(int days)
    {
        // Réduire la durée supprime des mesures existantes : on demande confirmation
        var stats = await _store.GetStatsAsync();
        var cutoff = DateTimeOffset.Now.AddDays(-days);

        if (stats.First is { } first && first < cutoff)
        {
            var confirmed = _dialogs.Confirm(
                "Réduire la durée de conservation",
                $"Les mesures de plus de {days} jours seront supprimées définitivement. Voulez-vous continuer ?");

            if (!confirmed)
            {
                SelectByValue(RetentionChoices, _settings.Current.RetentionDays);
                return;
            }
        }

        await Change(s => s with { RetentionDays = days });
        await _recorder.PurgeNowAsync();
    }

    private static List<ChoiceViewModel> BuildChoices<T>(
        IReadOnlyList<(string Label, T Value)> options,
        T current,
        Func<T, Task> onSelected)
    {
        var choices = new List<ChoiceViewModel>();

        foreach (var (label, value) in options)
        {
            var choice = new ChoiceViewModel(label, value)
            {
                IsSelected = EqualityComparer<T>.Default.Equals(value, current)
            };

            choice.Command = new AsyncRelayCommand(async () =>
            {
                foreach (var other in choices)
                {
                    other.IsSelected = ReferenceEquals(other, choice);
                }

                await onSelected(value);
            });

            choices.Add(choice);
        }

        return choices;
    }

    private static void SelectByValue<T>(IReadOnlyList<ChoiceViewModel> choices, T value)
    {
        foreach (var choice in choices)
        {
            choice.IsSelected = choice.Value is T candidate && EqualityComparer<T>.Default.Equals(candidate, value);
        }
    }
}