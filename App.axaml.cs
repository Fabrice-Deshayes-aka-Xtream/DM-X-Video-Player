using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DMXVideoPlayer.Views;

namespace DMXVideoPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Video file path passed as a startup argument (e.g. double-click in Explorer), if any.</summary>
        public static string? StartupVideoPath { get; set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                mainWindow.StartSingleInstancePipeServer();

                if (!string.IsNullOrEmpty(StartupVideoPath))
                {
                    var startupVideoPath = StartupVideoPath;
                    mainWindow.Opened += async (_, _) => await mainWindow.LoadVideoFromPathAsync(startupVideoPath);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

