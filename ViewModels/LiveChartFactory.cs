using Nexus.Core.Abstractions;
using Nexus.Core.Metrics;

namespace Nexus.App.ViewModels;

/// <summary>Crée les graphiques en leur fournissant les services communs.</summary>
public sealed class LiveChartFactory
{
    private readonly IMonitoringService _monitoring;
    private readonly LiveMetricsBuffer _buffer;
    private readonly ChartSettings _settings;

    public LiveChartFactory(IMonitoringService monitoring, LiveMetricsBuffer buffer, ChartSettings settings)
    {
        _monitoring = monitoring;
        _buffer = buffer;
        _settings = settings;
    }

    public LiveChartViewModel Create(
        string title,
        string unit,
        IReadOnlyList<ChartSeriesSpec> series,
        double? minY = null,
        double? maxY = null,
        string valueFormat = "0") =>
        new(title, unit, series, minY, maxY, valueFormat, _monitoring, _buffer, _settings);
}