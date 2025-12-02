// Services/WindowService.cs
using System.Runtime.InteropServices;
using Microsoft.JSInterop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace ArimaaAnalyzer.Maui.Services;

public static class WindowService
{
    public static AppWindow? AppWindow { get; set; }

    public static void Minimize()
    {
        if (AppWindow?.Presenter is OverlappedPresenter p)
            p.Minimize();
    }

    public static void ToggleMaximize()
    {
        if (AppWindow?.Presenter is OverlappedPresenter p)
        {
            if (p.State == OverlappedPresenterState.Maximized)
                p.Restore();
            else
                p.Maximize();
        }
    }

    public static void Close()
    {
        Application.Current?.Quit();
    }

    [JSInvokable]
    public static void StartWindowDrag()
    {
        if (AppWindow is null) return;

        var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        if (hwnd == IntPtr.Zero) return;

        ReleaseCapture();
        SendMessage(hwnd, 0x0112, 0xF012, IntPtr.Zero); // WM_SYSCOMMAND + SC_MOVE+HTCAPTION
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);
}