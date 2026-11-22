using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Nexus.App.Services;

internal static class TitleBarTheme
{
    private const int UseImmersiveDarkMode = 20; // Windows 10 (20H1 et plus) et Windows 11

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (Exception)
        {
            // Esthétique seulement : on ignore l'échec
        }
    }
}