using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Core.Abstractions;
using Nexus.Core.Models;
using Nexus.App.Services;

namespace Nexus.App.ViewModels;

public sealed partial class GpuViewModel : PageViewModel
{
    private readonly MetricRowViewModel _name = new("Modèle", "Nom exact de la carte graphique, tel que fourni par le pilote.");
    private readonly MetricRowViewModel _source = new("Source des données",
        "Origine des mesures. NVML est la bibliothèque officielle de NVIDIA, installée avec le pilote.");
    private readonly MetricRowViewModel _usage = new("Utilisation GPU",
        "Part du temps pendant lequel le cœur graphique est occupé.");
    private readonly MetricRowViewModel _temperature = new("Température",
        "Température du processeur graphique (GPU).");
    private readonly MetricRowViewModel _coreClock = new("Fréquence du cœur",
        "Vitesse de fonctionnement du cœur graphique, en MHz. Elle varie selon la charge et la température.");
    private readonly MetricRowViewModel _memoryClock = new("Fréquence mémoire",
        "Vitesse de la mémoire vidéo telle que rapportée par le pilote NVIDIA. Elle peut différer de la fréquence « effective » annoncée par le constructeur, car la mémoire GDDR transfère plusieurs fois par cycle.");
    private readonly MetricRowViewModel _fan = new("Ventilateurs",
        "Sur beaucoup de portables, les ventilateurs sont pilotés par la carte mère et non par le GPU : la valeur est alors indisponible.");
    private readonly MetricRowViewModel _power = new("Puissance consommée",
        "Puissance électrique absorbée par la carte, en watts. Non fournie par tous les portables.");

    private readonly MetricRowViewModel _vramUsed = new("VRAM utilisée",
        "Mémoire vidéo actuellement occupée (applications, jeux, bureau Windows).");
    private readonly MetricRowViewModel _vramTotal = new("VRAM totale",
        "Capacité totale de la mémoire vidéo de la carte.");
    private readonly MetricRowViewModel _vramFree = new("VRAM disponible",
        "Mémoire vidéo encore libre.");
    private readonly MetricRowViewModel _vramPercent = new("Utilisation VRAM",
        "Part de la mémoire vidéo occupée (VRAM utilisée divisée par VRAM totale).");

    public GpuViewModel(IMonitoringService monitoring, LiveChartFactory charts, ChartSettings chartSettings)
        : base("Carte graphique", "\uE7F4", "Utilisation, température et mémoire vidéo du GPU")
    {
        General = [_name, _source, _usage, _temperature, _coreClock, _memoryClock, _fan, _power];
        Vram = [_vramUsed, _vramTotal, _vramFree, _vramPercent];

        ChartSettings = chartSettings;
        UsageChart = charts.Create("Utilisation du GPU", "%",
            [new("gpu_usage", "GPU", ChartColors.Violet)], 0, 100);
        TemperatureChart = charts.Create("Température du GPU", "°C",
            [new("gpu_temp", "GPU", ChartColors.Amber)]);
        VramChart = charts.Create("Utilisation de la VRAM", "%",
            [new("vram_percent", "VRAM", ChartColors.Cyan)], 0, 100);

        monitoring.SampleCollected += OnSampleCollected;
    }

    public ChartSettings ChartSettings { get; }
    public LiveChartViewModel UsageChart { get; }
    public LiveChartViewModel TemperatureChart { get; }
    public LiveChartViewModel VramChart { get; }

    public IReadOnlyList<MetricRowViewModel> General { get; }
    public IReadOnlyList<MetricRowViewModel> Vram { get; }

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
        if (sample.Gpu is not { } gpu)
        {
            return;
        }

        _name.Value = gpu.Info.Name;
        _source.Value = gpu.Info.Source;
        _usage.Value = gpu.Usage.ToDisplay("%");
        _temperature.Value = gpu.Temperature.ToDisplay("°C");
        _coreClock.Value = gpu.CoreClockMhz.ToDisplay("MHz", "N0");
        _memoryClock.Value = gpu.MemoryClockMhz.ToDisplay("MHz", "N0");
        _fan.Value = gpu.FanSpeedPercent.ToDisplay("%");
        _power.Value = gpu.Power.ToDisplay("W", "0.0");

        _vramUsed.Value = gpu.VramUsedMb.ToDisplay("Mo", "N0");
        _vramTotal.Value = gpu.VramTotalMb.ToDisplay("Mo", "N0");
        _vramFree.Value = gpu.VramFreeMb.ToDisplay("Mo", "N0");
        _vramPercent.Value = gpu.VramUsagePercent.ToDisplay("%");

        Warning = gpu.Warning;
    }
}