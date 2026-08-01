using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentIcons.Common;
using System;
using System.Diagnostics;
using System.Reflection;
using DMXVideoPlayer.Services;

namespace DMXVideoPlayer.Views
{
    public partial class AboutWindow : Window
    {
        private const string GitHubUrl = "https://github.com/Fabrice-Deshayes-aka-Xtream/DMX-Video-Player";

        public AboutWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            SetVersionText();

            var githubLinkImage = this.FindControl<Image>("GitHubLinkImage");
            if (githubLinkImage != null)
            {
                githubLinkImage.PointerPressed += (s, e) => OpenGitHubLink();
            }

            var licenseTextBlock = this.FindControl<TextBlock>("LicenseTextBlock");
            if (licenseTextBlock != null)
            {
                licenseTextBlock.PointerPressed += (s, e) => OpenLicenseLink();
            }

            var changelogTextBlock = this.FindControl<TextBlock>("ChangelogTextBlock");
            if (changelogTextBlock != null)
            {
                changelogTextBlock.PointerPressed += (s, e) => OpenChangelogLink();
            }

            // Open source components links
            var avaloniaLink = this.FindControl<TextBlock>("AvaloniaLink");
            if (avaloniaLink != null)
            {
                avaloniaLink.PointerPressed += (s, e) => OpenUrl("https://avaloniaui.net/");
            }

            var libVLCLink = this.FindControl<TextBlock>("LibVLCLink");
            if (libVLCLink != null)
            {
                libVLCLink.PointerPressed += (s, e) => OpenUrl("https://code.videolan.org/videolan/LibVLCSharp");
            }

            var mediaInfoLink = this.FindControl<TextBlock>("MediaInfoLink");
            if (mediaInfoLink != null)
            {
                mediaInfoLink.PointerPressed += (s, e) => OpenUrl("https://mediaarea.net/en/MediaInfo");
            }

            var fluentIconsLink = this.FindControl<TextBlock>("FluentIconsLink");
            if (fluentIconsLink != null)
            {
                fluentIconsLink.PointerPressed += (s, e) => OpenUrl("https://github.com/davidxuang/FluentIcons");
            }

            var velopackLink = this.FindControl<TextBlock>("VelopackLink");
            if (velopackLink != null)
            {
                velopackLink.PointerPressed += (s, e) => OpenUrl("https://github.com/velopack/velopack");
            }
        }

        private void SetVersionText()
        {
            var versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
            if (versionTextBlock != null)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                versionTextBlock.Text = version != null
                    ? string.Format(LocalizationService.Instance["About_Version"], $"{version.Major}.{version.Minor}.{version.Build}")
                    : LocalizationService.Instance["About_UnknownVersion"];
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

        private void OpenLicenseLink()
        {
            try
            {
                Process.Start(new ProcessStartInfo($"{GitHubUrl}/blob/main/LICENSE.txt") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening license link: " + ex.Message);
            }
        }

        private void OpenChangelogLink()
        {
            try
            {
                Process.Start(new ProcessStartInfo($"{GitHubUrl}/blob/main/CHANGELOG.md") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening changelog link: " + ex.Message);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening URL {url}: " + ex.Message);
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
