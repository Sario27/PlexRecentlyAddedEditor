using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PlexRecentlyAddedEditor.Services;

/// WPF windows get a light title bar by default regardless of the icon or the
/// system theme, unless you explicitly opt in to DWM's dark-mode title bar via
/// this attribute. There's no managed API for this - it's a straight Win32 call.
public static class DarkTitleBar
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private const int DwmwaUseImmersiveDarkMode = 20;      // Windows 11 / Windows 10 20H1+
    private const int DwmwaUseImmersiveDarkModeLegacy = 19; // older Windows 10 builds

    public static void Apply(Window window)
    {
        if (PresentationSource.FromVisual(window) != null)
        {
            EnableFor(window);
        }
        else
        {
            window.SourceInitialized += (_, _) => EnableFor(window);
        }
    }

    private static void EnableFor(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int enabled = 1;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
    }
}
