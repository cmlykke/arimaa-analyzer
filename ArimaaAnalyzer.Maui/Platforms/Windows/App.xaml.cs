using Microsoft.UI.Xaml;
using ArimaaAnalyzer.Maui.Services;

namespace ArimaaAnalyzer.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var window = Microsoft.UI.Xaml.Window.Current;
        var appWindow = window?.AppWindow;

        if (appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false); // Remove native title bar
        }

        WindowService.AppWindow = appWindow;
    }
}