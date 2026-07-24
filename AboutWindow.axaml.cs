using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentIcons.Common;
using System;
using System.Diagnostics;
using System.Reflection;

namespace DMXVideoPlayer
{
    public partial class AboutWindow : Window
    {
        private const string GitHubUrl = "https://github.com/Fabrice-Deshayes-aka-Xtream/DMX-Video-Player";

        public AboutWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            SetVersionText();

            var githubLinkTextBlock = this.FindControl<TextBlock>("GitHubLinkTextBlock");
            if (githubLinkTextBlock != null)
            {
                githubLinkTextBlock.PointerPressed += (s, e) => OpenGitHubLink();
            }
        }

        private void SetVersionText()
        {
            var versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
            if (versionTextBlock != null)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                versionTextBlock.Text = version != null
                    ? $"Version {version.Major}.{version.Minor}.{version.Build}"
                    : "Version inconnue";
            }
        }

        private void OpenGitHubLink()
        {
            try
            {
                Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening GitHub link: " + ex.Message);
            }
        }

        private void SetWindowIcon()
        {
            try
            {
                bool isDarkMode = IsDarkTheme();
                var iconBrush = isDarkMode ? Brushes.White : Brushes.Black;

                var icon = new FluentIcons.Avalonia.Fluent.SymbolIcon
                {
                    Symbol = Symbol.Info,
                    Foreground = iconBrush,
                    FontSize = 28,
                    Width = 32,
                    Height = 32
                };

                icon.Measure(new Size(32, 32));
                icon.Arrange(new Rect(0, 0, 32, 32));

                var bitmap = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(96, 96));
                bitmap.Render(icon);

                this.Icon = new WindowIcon(bitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error creating about window icon: " + ex.Message);
            }
        }

        private bool IsDarkTheme()
        {
            try
            {
                if (Application.Current?.PlatformSettings != null)
                {
                    var colorValues = Application.Current.PlatformSettings.GetColorValues();
                    return colorValues.ThemeVariant == PlatformThemeVariant.Dark;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error detecting theme: " + ex.Message);
            }

            return true;
        }
    }
}
