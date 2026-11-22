using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nexus.App.Services;
using Nexus.App.ViewModels;
using Nexus.Core.Abstractions;
using Nexus.Core.Diagnostics;
using Nexus.Core.Metrics;
using Nexus.Core.Models;
using Nexus.Infrastructure.Monitoring;
using Nexus.Infrastructure.Sensors;
using Nexus.Infrastructure.Storage;

namespace Nexus.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _instanceMutex;
    private bool _errorDialogShown;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("Erreur fatale", e.ExceptionObject as Exception
                                          ?? new InvalidOperationException(e.ExceptionObject?.ToString()));

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Tâche en erreur", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!AcquireSingleInstance())
        {
            MessageBox.Show(
                "NEXUS Hardware Monitor est déjà en cours d'exécution.",
                "NEXUS Hardware Monitor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _services = BuildServices();

        // Thème, intervalles, historique et démarrage automatique, avant l'affichage de la fenêtre
        var settings = _services.GetRequiredService<ISettingsService>().Current;
        _services.GetRequiredService<SettingsApplier>().Apply(settings);

        _services.GetRequiredService<MainWindow>().Show();

        // Les consommateurs de mesures doivent être branchés avant la première mesure
        _services.GetRequiredService<LiveMetricsFeeder>();
        _services.GetRequiredService<HistoryService>().Start();
        _services.GetRequiredService<IMonitoringService>().Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Arrête la collecte, écrit les dernières mesures et ferme proprement les capteurs
        _services?.Dispose();

        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch (Exception)
        {
            // verrou non détenu : sans importance à la fermeture
        }

        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private bool AcquireSingleInstance()
    {
        try
        {
            _instanceMutex = new Mutex(false, @"Local\NexusHardwareMonitor.SingleInstance");

            // On attend un peu : lors d'une relance en administrateur, l'ancienne instance finit de se fermer
            return _instanceMutex.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (AbandonedMutexException)
        {
            return true; // l'ancienne instance s'est arrêtée brutalement
        }
        catch (UnauthorizedAccessException)
        {
            return false; // une instance lancée avec d'autres droits tourne déjà
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Erreur non gérée (interface)", e.Exception);
        e.Handled = true;

        if (_errorDialogShown)
        {
            return;
        }

        _errorDialogShown = true;
        MessageBox.Show(
            $"Une erreur inattendue est survenue. Elle a été enregistrée dans le journal :\n{AppLog.FilePath}",
            "NEXUS Hardware Monitor",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Paramètres et services système
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISettingsService>(_ => new JsonSettingsService());
        services.AddSingleton<IStartupService, RegistryStartupService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<SettingsApplier>();
        services.AddSingleton(new HistoryOptions());

        // Capteurs
        services.AddSingleton<HardwareSensorHost>();
        services.AddSingleton<ICpuMonitor, CpuMonitor>();
        services.AddSingleton<IGpuMonitor, GpuMonitor>();
        services.AddSingleton<IRamMonitor, RamMonitor>();
        services.AddSingleton<IStorageMonitor, StorageMonitor>();
        services.AddSingleton<IMonitoringService, MonitoringService>();

        // Mesures récentes, graphiques et historique
        services.AddSingleton(new LiveMetricsBuffer());
        services.AddSingleton<LiveMetricsFeeder>();
        services.AddSingleton<ChartSettings>();
        services.AddSingleton<LiveChartFactory>();
        services.AddSingleton<IHistoryStore>(_ => new SqliteHistoryStore());
        services.AddSingleton<HistoryService>();
        services.AddSingleton<IHistoryRecorder>(provider => provider.GetRequiredService<HistoryService>());

        // Pages : l'ordre d'enregistrement = l'ordre du menu latéral
        services.AddSingleton<PageViewModel, DashboardViewModel>();
        services.AddSingleton<PageViewModel, CpuViewModel>();
        services.AddSingleton<PageViewModel, GpuViewModel>();
        services.AddSingleton<PageViewModel, RamViewModel>();
        services.AddSingleton<PageViewModel, StorageViewModel>();
        services.AddSingleton<PageViewModel, HistoryViewModel>();
        services.AddSingleton<PageViewModel, SettingsViewModel>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}