using System.Windows.Controls;
using Nexus.App.ViewModels;

namespace Nexus.App.Views;

public partial class LiveChartView : UserControl
{
    public LiveChartView()
    {
        InitializeComponent();

        // Le graphique ne se met à jour que lorsqu'il est affiché
        Loaded += (_, _) => (DataContext as LiveChartViewModel)?.Activate();
        Unloaded += (_, _) => (DataContext as LiveChartViewModel)?.Deactivate();
    }
}