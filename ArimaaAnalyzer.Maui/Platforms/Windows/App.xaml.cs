using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT;

namespace ArimaaAnalyzer.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var window = Microsoft.UI.Xaml.Window.Current;
        if (window == null) return;

        // THIS REMOVES THE PINK GAP ON WINDOWS – WORKS 100%
        window.ExtendsContentIntoTitleBar = true;

        // Get the native AppWindow and TitleBar (this works in .NET 10)
        var hwnd = (long)window.GetType()
            .GetProperty("WindowHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(window)!;

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow((IntPtr)hwnd);
        if (AppWindow.GetFromWindowId(windowId) is AppWindow appWindow && 
            appWindow.TitleBar is AppWindowTitleBar titleBar)
        {
            // Make title bar area white and hide buttons
            titleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        }
    }
}