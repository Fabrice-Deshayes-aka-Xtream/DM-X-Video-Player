using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DMXVideoPlayer.Views;
using DMXVideoPlayer.Services;
using System.Threading.Tasks;

namespace DMXVideoPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Video file path passed as a startup argument (e.g. double-click in Explorer), if any.</summary>
        public static string? StartupVideoPath { get; set; }

        /// <summary>Service de gestion des mises à jour</summary>
        public static UpdateService UpdateService { get; private set; } = new UpdateService();

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

                // Vérifier les mises à jour en arrière-plan après le démarrage
                Task.Run(async () => await CheckForUpdatesAsync(mainWindow));
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task CheckForUpdatesAsync(MainWindow mainWindow)
        {
            // Attendre 2 secondes après le démarrage pour ne pas ralentir l'ouverture
            await Task.Delay(2000);

            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                // Notifier la fenêtre principale qu'une mise à jour est disponible
                await mainWindow.NotifyUpdateAvailableAsync(updateInfo);
            }
        }
    }
}

