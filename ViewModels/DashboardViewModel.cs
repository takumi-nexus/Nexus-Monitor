using System.Windows;
using Nexus.Core.Abstractions;
using Nexus.Core.Metrics;
using Nexus.Core.Models;
using Nexus.App.Services;

namespace Nexus.App.ViewModels;

public sealed class DashboardCardViewModel
{
    public DashboardCardViewModel(string title, string iconGlyph, IReadOnlyList<MetricRowViewModel> rows)
    {
        Title = title;
        IconGlyph = iconGlyph;
        Rows = rows;
    }

    public string Title { get; }
    public string IconGlyph { get; }
    public IReadOnlyList<MetricRowViewModel> Rows { get; }
}

public sealed class DashboardViewModel : PageViewModel
{
    private readonly MetricRowViewModel _cpuUsage = new("Utilisation", "Utilisation globale du processeur.");
    private readonly MetricRowViewModel _cpuTemp = new("Température", "Température du processeur (droits administrateur requis).");
    private readonly MetricRowViewModel _cpuFreq = new("Fréquence", "Fréquence actuelle du processeur.");

    private readonly MetricRowViewModel _gpuUsage = new("Utilisation", "Utilisation du cœur graphique.");
    private readonly MetricRowViewModel _gpuTemp = new("Température", "Température du processeur graphique.");
    private readonly MetricRowViewModel _gpuClock = new("Fréquence", "Fréquence actuelle du cœur graphique.");
    private readonly MetricRowViewModel _vram = new("VRAM utilisée", "Mémoire vidéo utilisée sur la mémoire vidéo totale.");

    private readonly MetricRowViewModel _ramPercent = new("Utilisation", "Part de la mémoire utilisable qui est occupée.");
    private readonly MetricRowViewModel _ramUsed = new("Mémoire utilisée", "Mémoire utilisée sur la mémoire utilisable par Windows.");

    private readonly MetricRowViewModel _diskActivity = new("Activité", "Part du temps pendant laquelle le disque est occupé.");
    private readonly MetricRowViewModel _diskTemp = new("Température", "Température du disque (peut nécessiter les droits administrateur).");
    private readonly MetricRowViewModel _diskSpace = new("Espace occupé", "Espace utilisé sur les volumes du disque système.");

    public DashboardViewModel(IMonitoringService monitoring, LiveChartFactory charts, ChartSettings chartSettings)
        : base("Tableau de bord", "\uE80F", "Vue d'ensemble de votre ordinateur")
    {
        ChartSettings = chartSettings;

        Cards =
        [
            new DashboardCardViewModel("Processeur", "\uE9F5", [_cpuUsage, _cpuTemp, _cpuFreq]),
            new DashboardCardViewModel("Carte graphique", "\uE7F4", [_gpuUsage, _gpuTemp, _gpuClock, _vram]),
            new DashboardCardViewModel("Mémoire vive", "\uE964", [_ramPercent, _ramUsed]),
            new DashboardCardViewModel("Stockage", "\uEDA2", [_diskActivity, _diskTemp, _diskSpace])
        ];

        UsageChart = charts.Create("Utilisation globale", "%",
        [
            new("cpu_usage", "CPU", ChartColors.Violet),
            new("gpu_usage", "GPU", ChartColors.Cyan),
            new("ram_percent", "RAM", ChartColors.Amber)
        ], 0, 100);

        TemperatureChart = charts.Create("Températures", "°C",
        [
            new("cpu_temp", "CPU", ChartColors.Violet),
            new("gpu_temp", "GPU", ChartColors.Cyan),
            new("disk_temp", "SSD", ChartColors.Amber)
        ]);

        monitoring.SampleCollected += OnSampleCollected;
    }

    public ChartSettings ChartSettings { get; }
    public IReadOnlyList<DashboardCardViewModel> Cards { get; }
    public LiveChartViewModel UsageChart { get; }
    public LiveChartViewModel TemperatureChart { get; }

    private void OnSampleCollected(object? sender, HardwareSample sample)
    {
        UiActivity.Post(() => Apply(sample));
    }

    private void Apply(HardwareSample sample)
    {
        if (sample.Cpu is { } cpu)
        {
            _cpuUsage.Value = cpu.TotalUsage.ToDisplay("%");
            _cpuTemp.Value = cpu.PackageTemperature.ToDisplay("°C");
            _cpuFreq.Value = cpu.FrequencyMhz.ToDisplay("MHz", "N0");
        }

        if (sample.Gpu is { } gpu)
        {
            _gpuUsage.Value = gpu.Usage.ToDisplay("%");
            _gpuTemp.Value = gpu.Temperature.ToDisplay("°C");
            _gpuClock.Value = gpu.CoreClockMhz.ToDisplay("MHz", "N0");
            _vram.Value = Pair(gpu.VramUsedMb, gpu.VramTotalMb, "Mo", "N0");
        }

        if (sample.Ram is { } ram)
        {
            _ramPercent.Value = ram.UsagePercent.ToDisplay("%");
            _ramUsed.Value = Pair(ram.UsedGb, ram.UsableGb, "Go", "0.0");
        }

        if (MetricCatalog.PrimaryDisk(sample.Storage) is { } disk)
        {
            _diskActivity.Value = disk.ActivityPercent.ToDisplay("%");
            _diskTemp.Value = disk.Temperature.ToDisplay("°C");
            _diskSpace.Value = disk.UsedGb.IsAvailable && disk.UsagePercent.IsAvailable
                ? $"{disk.UsedGb.ToDisplay("Go", "0")} ({disk.UsagePercent.ToDisplay("%")})"
                : disk.UsedGb.ToDisplay("Go", "0");
        }
    }

    private static string Pair(MetricValue used, MetricValue total, string unit, string format) =>
        used.IsAvailable && total.IsAvailable
            ? $"{used.Value!.Value.ToString(format)} / {total.Value!.Value.ToString(format)} {unit}"
            : "Non disponible";
}