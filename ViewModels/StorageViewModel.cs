using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Core.Abstractions;
using Nexus.Core.Models;
using static Nexus.App.ViewModels.CollectionSync;
using Nexus.App.Services;

namespace Nexus.App.ViewModels;

public sealed partial class StorageViewModel : PageViewModel
{
    public StorageViewModel(IMonitoringService monitoring, LiveChartFactory charts, ChartSettings chartSettings)
        : base("Stockage", "\uEDA2", "Espace, débits et état de santé du SSD")
    {
        ChartSettings = chartSettings;
        ActivityChart = charts.Create("Activité du disque système", "%",
            [new("disk_activity", "Activité", ChartColors.Violet)], 0, 100);
        ThroughputChart = charts.Create("Lecture et écriture du disque système", "Mo/s",
        [
            new("disk_read", "Lecture", ChartColors.Cyan),
            new("disk_write", "Écriture", ChartColors.Amber)
        ], 0, null, "0.0");

        monitoring.SampleCollected += OnSampleCollected;
    }

    public ChartSettings ChartSettings { get; }
    public LiveChartViewModel ActivityChart { get; }
    public LiveChartViewModel ThroughputChart { get; }

    public ObservableCollection<DiskViewModel> Disks { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string? _warning;

    public bool HasWarning => !string.IsNullOrEmpty(Warning);

    private void OnSampleCollected(object? sender, HardwareSample sample)
    {
        UiActivity.Post(() => Apply(sample));
    }

    private void Apply(HardwareSample sample)
    {
        if (sample.Storage is not { } storage)
        {
            return;
        }

        Sync(Disks, storage.Disks,
            source => new DiskViewModel(source.Number),
            (row, source) => row.Update(source));

        Warning = storage.Warning;
    }
}