using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Nexus.App.Services;
using Nexus.Core.Abstractions;
using Nexus.Core.Metrics;
using Nexus.Core.Models;
using SkiaSharp;

namespace Nexus.App.ViewModels;

public sealed partial class HistoryViewModel : PageViewModel
{
    private const int MaxPoints = 600;

    private static readonly SKColor Violet = SKColor.Parse(ChartColors.Violet);
    private static readonly SKColor Amber = SKColor.Parse(ChartColors.Amber);
    private static readonly SKColor LabelColor = SKColor.Parse("#8A8A98");
    private static readonly SKColor GridColor = SKColor.Parse("#8A8A98").WithAlpha(40);

    private readonly IHistoryStore _store;
    private readonly IHistoryRecorder _recorder;
    private readonly HistoryOptions _options;
    private readonly IDialogService _dialogs;
    private readonly LineSeries<DateTimePoint> _currentSeries;
    private readonly LineSeries<DateTimePoint> _previousSeries;
    private CancellationTokenSource? _loadCts;
    private TimeSpan _displayedRange = TimeSpan.FromHours(24);

    public HistoryViewModel(
        IHistoryStore store,
        IHistoryRecorder recorder,
        HistoryOptions options,
        IDialogService dialogs)
        : base("Historique", "\uE81C", "Consultation et export des mesures enregistrées")
    {
        _store = store;
        _recorder = recorder;
        _options = options;
        _dialogs = dialogs;

        MetricChoices = MetricCatalog.All.Select(m => new ChoiceViewModel(m.Name, m)).ToList();
        MetricChoices[0].IsSelected = true;

        RangeChoices =
        [
            new("1 heure", TimeSpan.FromHours(1)),
            new("24 heures", TimeSpan.FromHours(24)),
            new("7 jours", TimeSpan.FromDays(7)),
            new("30 jours", TimeSpan.FromDays(30))
        ];
        RangeChoices[1].IsSelected = true;

        CompareChoice = new ChoiceViewModel("Comparer avec la période précédente");

        _currentSeries = new LineSeries<DateTimePoint>
        {
            Name = "Période sélectionnée",
            Values = new List<DateTimePoint>(),
            GeometrySize = 0,
            LineSmoothness = 0,
            Stroke = new SolidColorPaint(Violet) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(Violet.WithAlpha(35))
        };

        _previousSeries = new LineSeries<DateTimePoint>
        {
            Name = "Période précédente (décalée)",
            Values = new List<DateTimePoint>(),
            GeometrySize = 0,
            LineSmoothness = 0,
            Stroke = new SolidColorPaint(Amber) { StrokeThickness = 2 },
            Fill = null
        };

        Series = [_currentSeries];

        XAxes =
        [
            new Axis
            {
                Labeler = FormatTime,
                UnitWidth = TimeSpan.FromMinutes(1).Ticks,
                MinStep = TimeSpan.FromMinutes(1).Ticks,
                ShowSeparatorLines = false,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(LabelColor)
            }
        ];

        YAxes =
        [
            new Axis
            {
                Labeler = value => $"{value:0.#}",
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(LabelColor),
                SeparatorsPaint = new SolidColorPaint(GridColor) { StrokeThickness = 1 }
            }
        ];

        // La page se recharge à chaque fois qu'on l'affiche
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsSelected) && IsSelected)
            {
                _ = LoadAsync();
            }
        };
    }

    public IReadOnlyList<ChoiceViewModel> MetricChoices { get; }
    public IReadOnlyList<ChoiceViewModel> RangeChoices { get; }
    public ChoiceViewModel CompareChoice { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    [ObservableProperty]
    private ISeries[] _series;

    [ObservableProperty]
    private bool _isComparing;

    [ObservableProperty]
    private string _chartTitle = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _statsText = string.Empty;

    [ObservableProperty]
    private string _retentionText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand]
    private async Task SelectMetricAsync(ChoiceViewModel choice)
    {
        SelectOnly(MetricChoices, choice);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SelectRangeAsync(ChoiceViewModel choice)
    {
        SelectOnly(RangeChoices, choice);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleCompareAsync()
    {
        CompareChoice.IsSelected = !CompareChoice.IsSelected;
        IsComparing = CompareChoice.IsSelected;
        await LoadAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (!_store.IsAvailable)
        {
            _dialogs.ShowError("Export impossible", _store.Error ?? "L'historique est indisponible.");
            return;
        }

        var path = _dialogs.PickCsvSavePath($"nexus-historique-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        if (path is null)
        {
            return;
        }

        try
        {
            await _recorder.FlushAsync();

            var now = DateTimeOffset.Now;
            var count = await _store.ExportCsvAsync(path, now - SelectedRange(), now);
            _dialogs.ShowInfo("Export terminé", $"{count:N0} lignes exportées vers :\n{path}");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Export impossible", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteHistoryAsync()
    {
        var confirmed = _dialogs.Confirm(
            "Supprimer l'historique",
            "Toutes les mesures enregistrées seront définitivement supprimées. Voulez-vous continuer ?");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _recorder.FlushAsync(); // évite qu'une mesure en attente soit écrite après la suppression
            await _store.DeleteAllAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Suppression impossible", ex.Message);
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;

        RetentionText =
            $"Conservation : {_options.RetentionDays} jours • enregistrement toutes les {_options.RecordingInterval.TotalSeconds:0} s";

        if (!_store.IsAvailable)
        {
            Message = _store.Error ?? "L'historique est indisponible.";
            ClearChart();
            return;
        }

        try
        {
            await _recorder.FlushAsync();

            var metric = SelectedMetric();
            var range = SelectedRange();
            var now = DateTimeOffset.Now;
            var from = now - range;

            var current = await _store.QueryAsync(metric.Id, from, now, MaxPoints, token);
            var summary = await _store.GetSummaryAsync(metric.Id, from, now, token);
            var stats = await _store.GetStatsAsync(token);

            IReadOnlyList<MetricPoint> previous = [];
            if (IsComparing)
            {
                previous = await _store.QueryAsync(metric.Id, from - range, from, MaxPoints, token);
            }

            token.ThrowIfCancellationRequested();

            ApplyChart(metric, range, from, now, current, previous, summary);
            ApplyStats(stats);
        }
        catch (OperationCanceledException)
        {
            // une consultation plus récente a pris le relais
        }
        catch (Exception ex)
        {
            Message = $"Lecture de l'historique impossible : {ex.Message}";
        }
    }

    private void ApplyChart(
        MetricDefinition metric,
        TimeSpan range,
        DateTimeOffset from,
        DateTimeOffset now,
        IReadOnlyList<MetricPoint> current,
        IReadOnlyList<MetricPoint> previous,
        MetricSummary summary)
    {
        _displayedRange = range;

        var bucket = HistoryMath.BucketSeconds(range, MaxPoints);
        var gapThreshold = TimeSpan.FromSeconds(Math.Max(bucket * 3, 30));

        _currentSeries.Values = HistoryMath.InsertGaps(current, gapThreshold)
            .Select(p => new DateTimePoint(p.Time.LocalDateTime, p.Value))
            .ToList();

        // La période précédente est décalée d'une durée égale à la période : les deux courbes se superposent
        _previousSeries.Values = IsComparing
            ? HistoryMath.InsertGaps(previous, gapThreshold)
                .Select(p => new DateTimePoint(p.Time.LocalDateTime + range, p.Value))
                .ToList()
            : new List<DateTimePoint>();

        Series = IsComparing
            ? new ISeries[] { _currentSeries, _previousSeries }
            : new ISeries[] { _currentSeries };

        XAxes[0].MinLimit = from.LocalDateTime.Ticks;
        XAxes[0].MaxLimit = now.LocalDateTime.Ticks;

        switch (metric.Unit)
        {
            case "%":
                YAxes[0].MinLimit = 0;
                YAxes[0].MaxLimit = 100;
                break;
            case "°C":
                YAxes[0].MinLimit = null;
                YAxes[0].MaxLimit = null;
                break;
            default:
                YAxes[0].MinLimit = 0;
                YAxes[0].MaxLimit = null;
                break;
        }

        ChartTitle = $"{metric.Name} ({metric.Unit}) — {SelectedRangeLabel()}";

        if (summary.Minimum is { } min && summary.Average is { } avg && summary.Maximum is { } max)
        {
            Message = null;
            SummaryText = $"Minimum {min:0.#} {metric.Unit}  •  Moyenne {avg:0.#} {metric.Unit}  •  Maximum {max:0.#} {metric.Unit}";
        }
        else
        {
            SummaryText = string.Empty;
            Message = "Aucune mesure enregistrée pour cette période.";
        }
    }

    private void ApplyStats(HistoryStats stats)
    {
        if (stats.Count == 0 || stats.First is not { } first || stats.Last is not { } last)
        {
            StatsText = $"Aucune mesure enregistrée • fichier : {_store.DatabasePath}";
            return;
        }

        StatsText =
            $"{stats.Count:N0} mesures enregistrées, du {first.LocalDateTime:dd/MM/yyyy HH:mm} " +
            $"au {last.LocalDateTime:dd/MM/yyyy HH:mm} • taille : {stats.SizeBytes / 1048576.0:0.0} Mo • fichier : {_store.DatabasePath}";
    }

    private void ClearChart()
    {
        _currentSeries.Values = new List<DateTimePoint>();
        _previousSeries.Values = new List<DateTimePoint>();
        SummaryText = string.Empty;
    }

    private MetricDefinition SelectedMetric() => (MetricDefinition)MetricChoices.First(c => c.IsSelected).Value!;

    private TimeSpan SelectedRange() => (TimeSpan)RangeChoices.First(c => c.IsSelected).Value!;

    private string SelectedRangeLabel() => RangeChoices.First(c => c.IsSelected).Label;

    private static void SelectOnly(IReadOnlyList<ChoiceViewModel> choices, ChoiceViewModel selected)
    {
        foreach (var choice in choices)
        {
            choice.IsSelected = ReferenceEquals(choice, selected);
        }
    }

    private string FormatTime(double ticks)
    {
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return string.Empty;
        }

        var format = _displayedRange <= TimeSpan.FromHours(24) ? "HH:mm" : "dd/MM HH:mm";
        return new DateTime((long)ticks).ToString(format);
    }
}