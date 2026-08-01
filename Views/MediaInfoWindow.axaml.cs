using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DMXVideoPlayer.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;

namespace DMXVideoPlayer.Views
{
    public partial class MediaInfoWindow : Window
    {
        private TextBox? _mediaInfoTextBox;

        public MediaInfoWindow()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _mediaInfoTextBox = this.FindControl<TextBox>("MediaInfoTextBox");
        }

        /// <summary>
        /// Analyse le fichier vidéo et affiche les informations MediaInfo
        /// </summary>
        /// <param name="filePath">Chemin complet du fichier vidéo</param>
        public void SetMediaInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                if (_mediaInfoTextBox != null)
                {
                    _mediaInfoTextBox.Text = LocalizationService.Instance["MediaInfo_FileNotFound"] ?? "File not found.";
                }
                return;
            }

            try
            {
                // Mettre le nom du fichier dans le titre de la fenêtre
                var fileName = Path.GetFileName(filePath);
                Title = $"{fileName} - {LocalizationService.Instance["MediaInfo_WindowTitle"] ?? "MediaInfo Information"}";

                // Chemin vers MediaInfo.exe
                var mediaInfoExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "MediaInfo.exe");

                if (!File.Exists(mediaInfoExePath))
                {
                    if (_mediaInfoTextBox != null)
                    {
                        _mediaInfoTextBox.Text = $"MediaInfo.exe not found at: {mediaInfoExePath}";
                    }
                    return;
                }

                // Appeler MediaInfo CLI avec la sortie standard (concise)
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = mediaInfoExePath,
                    Arguments = $"\"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        if (_mediaInfoTextBox != null)
                        {
                            _mediaInfoTextBox.Text = "Failed to start MediaInfo.exe";
                        }
                        return;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.WriteLine($"MediaInfo error: {error}");
                    }

                    if (_mediaInfoTextBox != null)
                    {
                        _mediaInfoTextBox.Text = output;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading MediaInfo: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (_mediaInfoTextBox != null)
                {
                    _mediaInfoTextBox.Text = $"{LocalizationService.Instance["MediaInfo_Error"] ?? "Error"}: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                }
            }
        }
    }
}