using System.Windows;

namespace Nexus.App.Services;

/// <summary>
/// Indique si l'interface est visible. Fenêtre réduite : plus aucune mise à jour d'affichage (économie de CPU),
/// alors que la collecte et l'historique continuent.
/// </summary>
public static class UiActivity
{
    private static volatile bool _isActive = true;

    public static bool IsActive => _isActive;

    /// <summary>Déclenché au retour de la fenêtre : les graphiques se reconstruisent.</summary>
    public static event Action? Resumed;

    public static void SetActive(bool active)
    {
        if (_isActive == active)
        {
            return;
        }

        _isActive = active;
        if (active)
        {
            Resumed?.Invoke();
        }
    }

    public static void Post(Action action)
    {
        if (!_isActive)
        {
            return;
        }

        Application.Current?.Dispatcher.InvokeAsync(action);
    }
}