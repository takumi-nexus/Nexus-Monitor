using Nexus.Core.Abstractions;
using Nexus.Core.Diagnostics;
using Nexus.Core.Models;

namespace Nexus.App.Services;

public sealed class SettingsApplier
{
    private readonly IThemeService _theme;
    private readonly IMonitoringService _monitoring;
    private readonly HistoryOptions _history;
    private readonly IStartupService _startup;

    public SettingsApplier(
        IThemeService theme,
        IMonitoringService monitoring,
        HistoryOptions history,
        IStartupService startup)
    {
        _theme = theme;
        _monitoring = monitoring;
        _history = history;
        _startup = startup;
    }

    public void Apply(AppSettings settings)
    {
        _theme.Apply(settings.Theme == "light" ? AppTheme.Light : AppTheme.Dark);
        _monitoring.RefreshInterval = TimeSpan.FromMilliseconds(settings.RefreshIntervalMs);

        _history.RecordingEnabled = settings.RecordingEnabled;
        _history.RecordingInterval = TimeSpan.FromSeconds(settings.RecordingIntervalSeconds);
        _history.RetentionDays = settings.RetentionDays;

        try
        {
            // Réécrit aussi le chemin de l'exécutable si l'application a été déplacée
            _startup.SetEnabled(settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            AppLog.Error("Démarrage automatique", ex);
        }
    }
}