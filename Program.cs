using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using DMXVideoPlayer.Views;
using Velopack;

namespace DMXVideoPlayer
{
    internal sealed class Program
    {
        // Named pipe used to forward a video file path from a secondary instance to the
        // already running primary instance when single-instance mode is enabled.
        private const string SingleInstancePipeName = "DMXVideoPlayer_IPC_Pipe";
        private const string SingleInstanceMutexName = "DMXVideoPlayer_SingleInstance_Mutex";

        private static Mutex? _singleInstanceMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack hook - must run before any other application logic
            VelopackApp.Build()
                .Run();

            string? videoPath = ExtractVideoPathArgument(args);

            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running: forward the video path (if any) and exit.
                if (!string.IsNullOrEmpty(videoPath))
                {
                    TrySendVideoPathToRunningInstance(videoPath);
                }

                return;
            }

            App.StartupVideoPath = videoPath;

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        private static string? ExtractVideoPathArgument(string[] args)
        {
            foreach (var arg in args)
            {
                if (!string.IsNullOrWhiteSpace(arg) && File.Exists(arg))
                {
                    return Path.GetFullPath(arg);
                }
            }

            return null;
        }

        private static void TrySendVideoPathToRunningInstance(string videoPath)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out);
                client.Connect(2000);

                using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                writer.WriteLine(videoPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to forward video path to the running instance: {ex.Message}");
            }
        }
    }
}
