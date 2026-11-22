using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Core.Models;

namespace Nexus.App.ViewModels;

public sealed partial class DiskViewModel : ObservableObject
{
    private readonly MetricRowViewModel _model = new("Modèle", "Nom du disque tel que déclaré par le fabricant.");
    private readonly MetricRowViewModel _manufacturer = new("Fabricant",
        "Nom du fabricant quand le disque le déclare. Beaucoup de SSD ne le fournissent pas : la marque figure alors le plus souvent dans le modèle.");
    private readonly MetricRowViewModel _type = new("Type",
        "Technologie du disque (SSD ou disque dur) et interface de connexion (NVMe, SATA...).");
    private readonly MetricRowViewModel _volumes = new("Volumes", "Lettres de lecteur situées sur ce disque.");
    private readonly MetricRowViewModel _capacity = new("Capacité totale",
        "Capacité physique du disque. Windows exprime les capacités en Gio : un SSD vendu 512 Go apparaît donc autour de 477 Go.");
    private readonly MetricRowViewModel _used = new("Espace occupé",
        "Espace utilisé sur les volumes de ce disque (les partitions système masquées ne sont pas comptées).");
    private readonly MetricRowViewModel _free = new("Espace disponible", "Espace encore libre sur les volumes de ce disque.");
    private readonly MetricRowViewModel _percent = new("Occupation", "Part de l'espace des volumes qui est occupée.");

    private readonly MetricRowViewModel _read = new("Vitesse de lecture",
        "Débit de lecture actuel du disque, en Mo/s. C'est le débit instantané réellement observé, pas la vitesse maximale du disque.");
    private readonly MetricRowViewModel _write = new("Vitesse d'écriture",
        "Débit d'écriture actuel du disque, en Mo/s.");
    private readonly MetricRowViewModel _activity = new("Activité du disque",
        "Part du temps pendant laquelle le disque est occupé à lire ou écrire (équivalent du « Temps d'activité » du Gestionnaire des tâches).");

    private readonly MetricRowViewModel _temperature = new("Température",
        "Température du disque lue via les indicateurs de santé (SMART). Peut nécessiter les droits administrateur. Actualisée toutes les 15 secondes.");
    private readonly MetricRowViewModel _health = new("État de santé",
        "Évaluation globale fournie par Windows : Sain, Avertissement ou Défaillant.");
    private readonly MetricRowViewModel _wear = new("Usure",
        "Pourcentage de la durée de vie estimée du SSD déjà consommé (100 % = limite d'usure atteinte). Valeur fournie par le disque lui-même ; certains modèles ne la déclarent pas.");
    private readonly MetricRowViewModel _hours = new("Heures de fonctionnement",
        "Durée totale pendant laquelle le disque a été sous tension depuis sa fabrication.");
    private readonly MetricRowViewModel _readErrors = new("Erreurs de lecture",
        "Nombre total d'erreurs de lecture rencontrées par le disque. 0 est normal ; une valeur qui augmente doit alerter.");
    private readonly MetricRowViewModel _writeErrors = new("Erreurs d'écriture",
        "Nombre total d'erreurs d'écriture rencontrées par le disque. 0 est normal ; une valeur qui augmente doit alerter.");

    public DiskViewModel(int number)
    {
        Number = number;
        Identity = [_model, _manufacturer, _type, _volumes, _capacity, _used, _free, _percent];
        Live = [_read, _write, _activity];
        Health = [_temperature, _health, _wear, _hours, _readErrors, _writeErrors];
    }

    public int Number { get; }

    public IReadOnlyList<MetricRowViewModel> Identity { get; }
    public IReadOnlyList<MetricRowViewModel> Live { get; }
    public IReadOnlyList<MetricRowViewModel> Health { get; }

    [ObservableProperty]
    private string _title = "Disque";

    public void Update(DiskSnapshot disk)
    {
        const string unavailable = "Non disponible";

        Title = $"Disque {disk.Number} : {disk.Model}";

        _model.Value = disk.Model;
        _manufacturer.Value = disk.Manufacturer ?? unavailable;

        var typeParts = new[] { disk.MediaType, disk.BusType }.OfType<string>().ToList();
        _type.Value = typeParts.Count switch
        {
            0 => unavailable,
            1 => typeParts[0],
            _ => $"{typeParts[0]} ({typeParts[1]})"
        };

        _volumes.Value = disk.Volumes ?? unavailable;
        _capacity.Value = disk.CapacityGb.ToDisplay("Go", "0.0");
        _used.Value = disk.UsedGb.ToDisplay("Go", "0.0");
        _free.Value = disk.FreeGb.ToDisplay("Go", "0.0");
        _percent.Value = disk.UsagePercent.ToDisplay("%");

        _read.Value = disk.ReadMbPerSecond.ToDisplay("Mo/s", "0.0");
        _write.Value = disk.WriteMbPerSecond.ToDisplay("Mo/s", "0.0");
        _activity.Value = disk.ActivityPercent.ToDisplay("%");

        _temperature.Value = disk.Temperature.ToDisplay("°C");
        _health.Value = disk.Health ?? unavailable;
        _wear.Value = disk.WearPercent.ToDisplay("%");
        _hours.Value = disk.PowerOnHours.ToDisplay("h", "N0");
        _readErrors.Value = disk.ReadErrors.ToDisplay(string.Empty, "N0");
        _writeErrors.Value = disk.WriteErrors.ToDisplay(string.Empty, "N0");
    }
}