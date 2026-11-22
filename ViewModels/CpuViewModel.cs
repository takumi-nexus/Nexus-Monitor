using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Core.Abstractions;
using Nexus.Core.Models;
using static Nexus.App.ViewModels.CollectionSync;
using Nexus.App.Services;

namespace Nexus.App.ViewModels;

public sealed partial class CpuViewModel : PageViewModel
{
    private readonly MetricRowViewModel _name = new("Modèle", "Nom commercial du processeur, lu via Windows (WMI).");
    private readonly MetricRowViewModel _manufacturer = new("Fabricant", "Entreprise qui fabrique le processeur.");
    private readonly MetricRowViewModel _cores = new("Cœurs / threads",
        "Un cœur est une unité de calcul. Grâce à l'Hyper-Threading, chaque cœur peut traiter 2 threads (tâches) en parallèle.");
    private readonly MetricRowViewModel _nominal = new("Fréquence nominale",
        "Fréquence de base annoncée par Windows. Le mode Turbo peut temporairement la dépasser quand le processeur est peu chargé et assez froid.");

    private readonly MetricRowViewModel _usage = new("Utilisation globale",
        "Part du temps pendant lequel le processeur travaille, tous cœurs confondus.");
    private readonly MetricRowViewModel _temperature = new("Température globale",
        "Température du processeur lue dans ses capteurs internes. Nécessite un pilote et les droits administrateur.");
    private readonly MetricRowViewModel _frequency = new("Fréquence actuelle",
        "Vitesse de fonctionnement en MHz. Sur un portable, elle varie en permanence (économie d'énergie, Turbo).");
    private readonly MetricRowViewModel _voltage = new("Tension",
        "Tension d'alimentation du processeur. Souvent non exposée sur les portables.");
    private readonly MetricRowViewModel _power = new("Puissance consommée",
        "Puissance électrique absorbée par le processeur, en watts. Nécessite les droits administrateur.");
    private readonly MetricRowViewModel _throttling = new("Limitation thermique",
        "Détection indirecte : signalée quand la fréquence reste nettement sous la fréquence nominale malgré une forte charge, pendant plusieurs secondes. Elle ne permet pas de distinguer une limite de température d'une limite de puissance.");

    public CpuViewModel(IMonitoringService monitoring, LiveChartFactory charts, ChartSettings chartSettings)
        : base("Processeur", "\uE9F5", "Utilisation, températures et fréquences du processeur")
    {
        Identity = [_name, _manufacturer, _cores, _nominal];
        Measures = [_usage, _temperature, _frequency, _voltage, _power, _throttling];

        ChartSettings = chartSettings;
        UsageChart = charts.Create("Utilisation du processeur", "%",
            [new("cpu_usage", "CPU", ChartColors.Violet)], 0, 100);
        TemperatureChart = charts.Create("Température du processeur", "°C",
            [new("cpu_temp", "CPU", ChartColors.Amber)]);
        FrequencyChart = charts.Create("Fréquence du processeur", "MHz",
            [new("cpu_freq", "CPU", ChartColors.Cyan)], 0, null);

        monitoring.SampleCollected += OnSampleCollected;
    }

    public ChartSettings ChartSettings { get; }
    public LiveChartViewModel UsageChart { get; }
    public LiveChartViewModel TemperatureChart { get; }
    public LiveChartViewModel FrequencyChart { get; }

    public IReadOnlyList<MetricRowViewModel> Identity { get; }
    public IReadOnlyList<MetricRowViewModel> Measures { get; }

    public ObservableCollection<LogicalRowViewModel> LogicalProcessors { get; } = [];
    public ObservableCollection<CoreRowViewModel> Cores { get; } = [];

    [ObservableProperty]
    private bool _hasAdminNotice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string? _warning;

    [ObservableProperty]
    private string _frequencyNote = string.Empty;

    public bool HasWarning => !string.IsNullOrEmpty(Warning);

    private void OnSampleCollected(object? sender, HardwareSample sample)
    {
        // Les capteurs sont lus sur un thread de fond : on revient sur le thread de l'interface.
        UiActivity.Post(() => Apply(sample));
    }

    private void Apply(HardwareSample sample)
    {
        if (sample.Cpu is not { } cpu)
        {
            return;
        }

        var info = cpu.Info;
        _name.Value = info.Name;
        _manufacturer.Value = info.Manufacturer;
        _cores.Value = info.PhysicalCores > 0
            ? $"{info.PhysicalCores} cœurs / {info.LogicalProcessors} threads"
            : "Non disponible";
        _nominal.Value = info.NominalFrequencyMhz is { } nominal ? $"{nominal:N0} MHz" : "Non disponible";

        _usage.Value = cpu.TotalUsage.ToDisplay("%");
        _temperature.Value = cpu.PackageTemperature.ToDisplay("°C");
        _frequency.Value = cpu.FrequencyMhz.ToDisplay("MHz", "N0");
        _voltage.Value = cpu.Voltage.ToDisplay("V", "0.000");
        _power.Value = cpu.Power.ToDisplay("W", "0.0");
        _throttling.Value = cpu.Throttling switch
        {
            ThrottlingState.None => "Aucune limitation détectée",
            ThrottlingState.Suspected => "Limitation possible (fréquence basse sous forte charge)",
            _ => "Non disponible"
        };

        FrequencyNote = cpu.FrequencyIsEstimated
            ? "Fréquence estimée à partir du compteur de performance Windows (valeur approchée)."
            : string.Empty;

        HasAdminNotice = new[] { cpu.PackageTemperature, cpu.Voltage, cpu.Power, cpu.FrequencyMhz }
            .Any(m => m.Status == MetricStatus.RequiresAdmin);
        Warning = cpu.Warning;

        Sync(LogicalProcessors, cpu.LogicalProcessors,
            source => new LogicalRowViewModel(source.Index),
            (row, source) => row.Usage = source.Usage.ToDisplay("%"));

        Sync(Cores, cpu.Cores,
            source => new CoreRowViewModel(source.Index),
            (row, source) =>
            {
                row.Temperature = source.Temperature.ToDisplay("°C");
                row.Frequency = source.FrequencyMhz.ToDisplay("MHz", "N0");
            });
    }
}