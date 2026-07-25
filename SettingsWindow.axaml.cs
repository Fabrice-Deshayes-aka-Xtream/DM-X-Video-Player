using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using FluentIcons.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DMXVideoPlayer
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow? _owner;

        public SettingsWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            SetupKeyboardHandling();
        }

        public SettingsWindow(MainWindow owner) : this()
        {
            _owner = owner;
            SetupSettingsControls();
            SetupLanguageComboBox();
        }

        private void SetupKeyboardHandling()
        {
            this.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            this.Focusable = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.P)
            {
                e.Handled = true;
                Debug.WriteLine("SettingsWindow OnKeyDown: P key pressed, closing settings window");
                Close();
            }
        }

        private sealed class LanguageOption
        {
            public string Code { get; }
            public string DisplayName { get; }

            public LanguageOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName;
        }

        private bool _isUpdatingLanguageComboBox;

        private void SetupLanguageComboBox()
        {
            var languageComboBox = this.FindControl<ComboBox>("LanguageComboBox");
            var restartHintTextBlock = this.FindControl<TextBlock>("LanguageRestartHintTextBlock");
            if (languageComboBox == null || _owner == null)
                return;

            _isUpdatingLanguageComboBox = true;
            try
            {
                var options = new[]
                {
                    new LanguageOption("fr", LocalizationService.Instance["Settings_Language_French"]),
                    new LanguageOption("en", LocalizationService.Instance["Settings_Language_English"])
                };

                languageComboBox.ItemsSource = options;
                var currentCode = _owner.GetLanguage();
                languageComboBox.SelectedItem = options.FirstOrDefault(o => o.Code == currentCode) ?? options[0];
            }
            finally
            {
                _isUpdatingLanguageComboBox = false;
            }

            languageComboBox.SelectionChanged += (s, e) =>
            {
                if (_isUpdatingLanguageComboBox || _owner == null)
                    return;

                if (languageComboBox.SelectedItem is LanguageOption option)
                {
                    _owner.SetLanguage(option.Code);
                    if (restartHintTextBlock != null)
                    {
                        restartHintTextBlock.IsVisible = true;
                    }
                }
            };
        }

        private void SetupSettingsControls()
        {
            if (_owner == null)
                return;

            var audioOutputComboBox = this.FindControl<ComboBox>("AudioOutputComboBox");
            if (audioOutputComboBox != null)
            {
                var devices = _owner.GetAudioOutputDevices().ToList();
                audioOutputComboBox.ItemsSource = devices;
                audioOutputComboBox.SelectedItem = _owner.GetSelectedAudioOutputDevice();

                audioOutputComboBox.SelectionChanged += (s, e) =>
                {
                    if (audioOutputComboBox.SelectedItem is AudioOutputDevice device)
                    {
                        _owner.SetSelectedAudioOutputDevice(device);
                    }
                };
            }

            var timecodeCheckBox = this.FindControl<CheckBox>("TimecodeCheckBox");
            if (timecodeCheckBox != null)
            {
                timecodeCheckBox.IsChecked = _owner.GetShowTimecode();

                timecodeCheckBox.IsCheckedChanged += (s, e) =>
                {
                    _owner.SetShowTimecode(timecodeCheckBox.IsChecked == true);
                };
            }

            var bpmCheckBox = this.FindControl<CheckBox>("BpmCheckBox");
            if (bpmCheckBox != null)
            {
                bpmCheckBox.IsChecked = _owner.GetShowBpm();

                bpmCheckBox.IsCheckedChanged += (s, e) =>
                {
                    _owner.SetShowBpm(bpmCheckBox.IsChecked == true);
                };
            }

            var defaultVideoDirectoryTextBox = this.FindControl<TextBox>("DefaultVideoDirectoryTextBox");
            if (defaultVideoDirectoryTextBox != null)
            {
                defaultVideoDirectoryTextBox.Text = _owner.GetDefaultVideoDirectory();
            }

            var seekStepNumericUpDown = this.FindControl<NumericUpDown>("SeekStepNumericUpDown");
            if (seekStepNumericUpDown != null)
            {
                seekStepNumericUpDown.Value = _owner.GetSeekStepSeconds();

                seekStepNumericUpDown.ValueChanged += (s, e) =>
                {
                    if (seekStepNumericUpDown.Value.HasValue)
                    {
                        _owner.SetSeekStepSeconds((int)seekStepNumericUpDown.Value.Value);
                    }
                };
            }

            var browseButton = this.FindControl<Button>("BrowseDefaultVideoDirectoryButton");
            if (browseButton != null)
            {
                browseButton.Click += async (s, e) =>
                {
                    try
                    {
                        var storageProvider = StorageProvider;
                        if (storageProvider == null)
                            return;

                        var startPath = _owner.GetDefaultVideoDirectory();
                        IStorageFolder? startFolder = null;
                        if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
                        {
                            startFolder = await storageProvider.TryGetFolderFromPathAsync(startPath);
                        }

                        var options = new FolderPickerOpenOptions
                        {
                            Title = LocalizationService.Instance["Settings_ChooseDefaultVideoDirectoryTitle"],
                            AllowMultiple = false,
                            SuggestedStartLocation = startFolder
                        };

                        var result = await storageProvider.OpenFolderPickerAsync(options);
                        if (result != null && result.Count > 0)
                        {
                            var folderPath = result[0].TryGetLocalPath();
                            if (string.IsNullOrWhiteSpace(folderPath))
                            {
                                Debug.WriteLine("Error choosing default video directory: no local path returned for selected item.");
                                return;
                            }

                            // Un lecteur choisi (ex. "G:") doit conserver le séparateur final
                            // pour que Directory.Exists le reconnaisse correctement comme racine.
                            if (folderPath.Length == 2 && folderPath[1] == ':')
                            {
                                folderPath += Path.DirectorySeparatorChar;
                            }

                            _owner.SetDefaultVideoDirectory(folderPath);
                            if (defaultVideoDirectoryTextBox != null)
                            {
                                defaultVideoDirectoryTextBox.Text = folderPath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error choosing default video directory: {ex.Message}");
                    }
                };
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
                    Symbol = Symbol.Settings,
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
                Debug.WriteLine("Error creating settings window icon: " + ex.Message);
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
