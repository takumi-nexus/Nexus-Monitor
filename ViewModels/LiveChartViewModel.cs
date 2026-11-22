using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Nexus.Core.Abstractions;
using Nexus.Core.Metrics;
using Nexus.Core.Models;
using SkiaSharp;
using Nexus.App.Services;

namespace Nexus.App.ViewModels;

public static class ChartColors
{
    public const string Violet = "#8B5CF6";
    public const string Cyan = "#22D3EE";
    public const string Amber = "#F59E0B";
}

public sealed record ChartSeriesSpec(string MetricId, string Name, string ColorHex);

/// <summary>Élément de légende : pastille de couleur, nom et valeur actuelle.</summary>
public sealed partial class LegendItemViewModel : ObservableObject
{
    public LegendItemViewModel(string name, Brush swatch)
    {
        Name = name;
        Swatch = swatch;
    }

    public string Name { get; }
    public Brush Swatch { get; }

    [ObservableProperty]
    private string _value = "Non disponible";
}

/// <summary>
/// Graphique temps réel d'une ou plusieurs mesures. Il ne se met à jour que lorsque sa vue est affichée.
/// </summary>
public sealed class LiveChartViewModel : ObservableObject
{
    private const int MaxPoints = 600;

    private static readonly SKColor LabelColor = SKColor.Parse("#8A8A98");
    private static readonly SKColor GridColor = SKColor.Parse("#8A8A98").WithAlpha(40);

    private sealed class SeriesState
    {
        public required string MetricId { get; init; }
        public required LineSeries<DateTimePoint> Series { get; init; }
        public required LegendItemViewModel Legend { get; init; }
        public ObservableCollection<DateTimePoint> Values { get; set; } = [];
        public DateTime LastAdded { get; set; } = DateTime.MinValue;
    }

    private readonly List<SeriesState> _states = [];
    private readonly LiveMetricsBuffer _buffer;
    private readonly ChartSettings _settings;
    private readonly string _valueFormat;
    private volatile bool _active;

    private void OnUiResumed()
    {
        if (_active)
        {
            Rebuild();
        }
    }

    public LiveChartViewModel(
        string title,
        string unit,
        IReadOnlyList<ChartSeriesSpec> specs,
        double? minY,
        double? maxY,
        string valueFormat,
        IMonitoringService monitoring,
        LiveMetricsBuffer buffer,
        ChartSettings settings)
    {
        Title = title;
        Unit = unit;
        _valueFormat = valueFormat;
        _buffer = buffer;
        _settings = settings;

        var series = new List<ISeries>();
        foreach (var spec in specs)
        {
            var color = SKColor.Parse(spec.ColorHex);
            var values = new ObservableCollection<DateTimePoint>();

            var line = new LineSeries<DateTimePoint>
            {
                Name = spec.Name,
                Values = values,
                GeometrySize = 0,
                LineSmoothness = 0,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
                Fill = specs.Count == 1 ? new SolidColorPaint(color.WithAlpha(35)) : null
            };

            series.Add(line);
            _states.Add(new SeriesState
            {
                MetricId = spec.MetricId,
                Series = line,
                Legend = new LegendItemViewModel(spec.Name, CreateBrush(spec.ColorHex)),
                Values = values
            });
        }

        Series = series.ToArray();
        Legend = _states.Select(s => s.Legend).ToList();

        XAxes =
        [
            new Axis
            {
                Labeler = FormatTime,
                UnitWidth = TimeSpan.FromSeconds(1).Ticks,
                MinStep = TimeSpan.FromSeconds(1).Ticks,
                ShowSeparatorLines = false,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(LabelColor)
            }
        ];

        YAxes =
        [
            new Axis
            {
                MinLimit = minY,
                MaxLimit = maxY,
                Labeler = value => $"{value:0}",
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(LabelColor),
                SeparatorsPaint = new SolidColorPaint(GridColor) { StrokeThickness = 1 }
            }
        ];

        monitoring.SampleCollected += OnSampleCollected;
        settings.PropertyChanged += OnSettingsChanged;
        UiActivity.Resumed += OnUiResumed;
    }

    public string Title { get; }
    public string Unit { get; }
    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }
    public IReadOnlyList<LegendItemViewModel> Legend { get; }

    /// <summary>Appelé quand la vue s'affiche : reconstruit le graphique depuis le tampon.</summary>
    public void Activate()
    {
        _active = true;
        Rebuild();
    }

    public void Deactivate() => _active = false;

    private TimeSpan Spacing => TimeSpan.FromTicks(_settings.Period.Ticks / MaxPoints);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChartSettings.Period) && _active)
        {
            Rebuild();
        }
    }

    private void OnSampleCollected(object? sender, HardwareSample sample)
    {
        if (!_active)
        {
            return;
        }

        UiActivity.Post(() => Append(sample));
    }

    private void Append(HardwareSample sample)
    {
        if (!_active)
        {
            return;
        }

        var time = sample.Timestamp.LocalDateTime;
        var spacing = Spacing;

        foreach (var state in _states)
        {
            var value = MetricCatalog.Extract(state.MetricId, sample);
            state.Legend.Value = value is { } v ? $"{v.ToString(_valueFormat)} {Unit}" : "Non disponible";

            if (state.Values.Count == 0 || time - state.LastAdded >= spacing)
            {
                state.Values.Add(new DateTimePoint(time, value));
                state.LastAdded = time;
            }

            var cutoff = time - _settings.Period - spacing;
            while (state.Values.Count > 0 && state.Values[0].DateTime < cutoff)
            {
                state.Values.RemoveAt(0);
            }
        }

        UpdateAxis(time);
    }

    private void Rebuild()
    {
        var now = DateTimeOffset.Now;
        var spacing = Spacing;

        foreach (var state in _states)
        {
            var values = new ObservableCollection<DateTimePoint>();
            var last = DateTime.MinValue;
            double? lastValue = null;

            foreach (var point in _buffer.Get(state.MetricId, _settings.Period, now))
            {
                var time = point.Time.LocalDateTime;
                lastValue = point.Value;

                if (time - last < spacing)
                {
                    continue;
                }

                values.Add(new DateTimePoint(time, point.Value));
                last = time;
            }

            state.Values = values;
            state.LastAdded = last;
            state.Series.Values = values;
            state.Legend.Value = lastValue is { } v ? $"{v.ToString(_valueFormat)} {Unit}" : "Non disponible";
        }

        UpdateAxis(now.LocalDateTime);
    }

    private void UpdateAxis(DateTime now)
    {
        XAxes[0].MinLimit = (now - _settings.Period).Ticks;
        XAxes[0].MaxLimit = now.Ticks;
    }

    private string FormatTime(double ticks)
    {
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return string.Empty;
        }

        var format = _settings.Period <= TimeSpan.FromMinutes(5) ? "HH:mm:ss" : "HH:mm";
        return new DateTime((long)ticks).ToString(format);
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}