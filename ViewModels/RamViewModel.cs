using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Core.Abstractions;
using Nexus.Core.Models;
using Nexus.App.Services;

namespace Nexus.App.ViewModels;

public sealed partial class RamViewModel : PageViewModel
{
    private readonly MetricRowViewModel _installed = new("Capacité installée",
        "Somme de la capacité de toutes les barrettes, lue dans le matériel (WMI).");
    private readonly MetricRowViewModel _modules = new("Barrettes",
        "Nombre de modules mémoire installés et capacité de chacun.");
    private readonly MetricRowViewModel _type = new("Type de mémoire",
        "Génération de la mémoire (DDR3, DDR4, DDR5...), lue dans les informations du BIOS.");
    private readonly MetricRowViewModel _speed = new("Vitesse",
        "Vitesse de transfert configurée, en mégatransferts par seconde (MT/s). Elle est souvent notée « MHz » dans les spécifications commerciales ; pour de la DDR, la fréquence d'horloge réelle en est la moitié.");

    private readonly MetricRowViewModel _usable = new("Mémoire utilisable",
        "Mémoire que Windows peut réellement utiliser. Elle est un peu inférieure à la capacité installée, car une partie est réservée par le matériel (carte graphique intégrée, BIOS).");
    private readonly MetricRowViewModel _percent = new("Utilisation",
        "Part de la mémoire utilisable qui est occupée.");
    private readonly MetricRowViewModel _used = new("Mémoire utilisée",
        "Mémoire occupée par les applications, le système et les pilotes (utilisable moins disponible). Le Gestionnaire des tâches peut afficher une valeur légèrement inférieure, car il exclut les pages modifiées.");
    private readonly MetricRowViewModel _available = new("Mémoire disponible",
        "Mémoire immédiatement réutilisable (libre + cache récupérable).");
    private readonly MetricRowViewModel _committed = new("Mémoire engagée",
        "Quantité de mémoire réservée par les programmes, même si elle n'est pas encore physiquement utilisée. Elle peut dépasser la RAM grâce au fichier d'échange. Le Gestionnaire des tâches l'appelle « Validé ».");
    private readonly MetricRowViewModel _commitLimit = new("Limite d'engagement",
        "Maximum que Windows peut promettre aux programmes : RAM plus fichier d'échange.");
    private readonly MetricRowViewModel _cached = new("Mémoire en cache",
        "Mémoire servant de cache pour accélérer l'accès aux fichiers. Elle est libérée automatiquement si une application en a besoin.");

    public RamViewModel(IMonitoringService monitoring, LiveChartFactory charts, ChartSettings chartSettings)
        : base("Mémoire vive", "\uE964", "Mémoire utilisée, disponible et engagée")
    {
        Hardware = [_installed, _modules, _type, _speed];
        Usage = [_usable, _percent, _used, _available, _committed, _commitLimit, _cached];

        ChartSettings = chartSettings;
        UsageChart = charts.Create("Utilisation de la mémoire", "%",
            [new("ram_percent", "RAM", ChartColors.Violet)], 0, 100);

        monitoring.SampleCollected += OnSampleCollected;
    }

    public ChartSettings ChartSettings { get; }
    public LiveChartViewModel UsageChart { get; }

    public IReadOnlyList<MetricRowViewModel> Hardware { get; }
    public IReadOnlyList<MetricRowViewModel> Usage { get; }

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
        if (sample.Ram is not { } ram)
        {
            return;
        }

        var info = ram.Info;

        _installed.Value = info.InstalledCapacityGb is { } installed ? $"{installed:0.#} Go" : "Non disponible";

        _modules.Value = info.ModuleCapacitiesGb.Count switch
        {
            0 => "Non disponible",
            1 => $"1 barrette : {info.ModuleCapacitiesGb[0]:0.#} Go",
            var count => $"{count} barrettes : {string.Join(" + ", info.ModuleCapacitiesGb.Select(c => $"{c:0.#} Go"))}"
        };

        _type.Value = info.MemoryType ?? "Non disponible";
        _speed.Value = info.SpeedsMtPerSecond.Count > 0
            ? $"{string.Join(" / ", info.SpeedsMtPerSecond)} MT/s"
            : "Non disponible";

        _usable.Value = ram.UsableGb.ToDisplay("Go", "0.0");
        _percent.Value = ram.UsagePercent.ToDisplay("%");
        _used.Value = ram.UsedGb.ToDisplay("Go", "0.0");
        _available.Value = ram.AvailableGb.ToDisplay("Go", "0.0");
        _committed.Value = ram.CommittedGb.ToDisplay("Go", "0.0");
        _commitLimit.Value = ram.CommitLimitGb.ToDisplay("Go", "0.0");
        _cached.Value = ram.CachedGb.ToDisplay("Go", "0.0");

        Warning = ram.Warning;
    }
}