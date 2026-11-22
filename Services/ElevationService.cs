using System.ComponentModel;
using System.Diagnostics;

namespace Nexus.App.Services;

public static class ElevationService
{
    /// <summary>Lance une nouvelle instance en administrateur. Renvoie false si l'utilisateur refuse la fenêtre UAC.</summary>
    public static bool StartElevatedInstance()
    {
        var path = Environment.ProcessPath;
        if (path is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception)
        {
            return false; // refus de l'utilisateur dans la fenêtre UAC
        }
    }
}