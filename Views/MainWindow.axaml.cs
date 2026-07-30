using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;
using IOPath = System.IO.Path;
using FluentIcons.Avalonia.Fluent;
using DMXVideoPlayer.Controls;
using DMXVideoPlayer.Services;

namespace DMXVideoPlayer.Views
{
    public partial class MainWindow : Window
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private Media? _currentMedia;
        private VideoView? _videoView;
        private Border? _videoContainer;
        private TextBlock? _placeholderText;
        private Image? _placeholderImage;
        private Button? _playPauseButton;
        private SymbolIcon? _playPauseIcon;
        private Button? _stopButton;
        private Button? _loadButton;
        private Button? _settingsButton;
        private Button? _aboutButton;
        private Slider? _volumeSlider;
        private Slider? _positionSlider;
        private TextBlock? _timeLabel;
        private TextBlock? _durationLabel;
        private Grid? _audioTrackPanel;
        private ComboBox? _audioTrackComboBox;
        private ComboBox? _audioOutputComboBox;
        private DispatcherTimer? _updateTimer;
        private bool _isDraggingPosition = false;
        private bool _isUpdatingAudioTracks = false;
        private bool _isUpdatingAudioOutput = false;
        private string? _currentVideoFilePath;
        private bool _isUserInteractingWithSlider = false;
        private int _seekStepSeconds = 5;
        private double _overlayFontSize = 22.0;
        private double _infoPanelOpacity = 0x66 / 255.0;
        private string? _selectedAudioDeviceId = null;
        private int? _selectedAudioTrackId = null;
        private Button? _audioTrackButton;
        private Button? _subtitleButton;
        private TextBlock? _subtitleButtonText;
        private TextBlock? _audioTrackButtonText;
        private Button? _volumeButton;
        private SymbolIcon? _volumeIcon;
        private int _volumeBeforeMute = 100;
        private bool _useHourFormat = false;
        private bool _isMuted = false;
        private string? _lastVideoDirectory;
        private Border? _controlsOverlay;

        private DispatcherTimer? _hideControlsTimer;
        private Button? _balanceLockButton;
        private SymbolIcon? _balanceLockIcon;
        private Slider? _balanceSlider;
        private bool _isBalanceLocked = false;
        private Grid? _compactControlsRow;
        private StackPanel? _essentialButtonsPanel;
        private StackPanel? _timeDisplayPanel;
        private StackPanel? _selectionGroupPanel;
        private StackPanel? _balanceGroupPanel;
        private Grid? _balanceSpacer;
        private StackPanel? _volumeGroupPanel;
        private TextBlock? _timecodeLabel;
        private CheckBox? _timecodeCheckBox;
        private CheckBox? _bpmCheckBox;
        private CheckBox? _timeSignatureCheckBox;
        private CheckBox? _barBeatCheckBox;
        private string _lastTimecodeText = string.Empty;
        private TextBlock? _bpmLabelOverlay;
        private TextBlock? _timeSignatureLabelOverlay;
        private TextBlock? _barBeatLabelOverlay;
        private int _lastDisplayedNumerator = -1;
        private int _lastDisplayedDenominator = -1;
        private int _lastDisplayedBar = -1;
        private int _lastDisplayedBeat = -1;
        private Border? _beatLed;
        private Canvas? _infoPanelCanvas;
        private Border? _infoPanel;
        private bool _isDraggingInfoPanel = false;
        private Point _infoPanelDragPointerStart;
        private Point _infoPanelDragOrigin;
        private bool _infoPanelPositionInitialized = false;
        // Relative position of the panel (0.0 - 1.0) within the canvas' available space,
        // to keep its relative position during size changes (e.g. fullscreen toggle).
        private double _infoPanelRelativeX = 0.5;
        private double _infoPanelRelativeY = 0.0;
        // Distance (in pixels) within which the info panel snaps to the horizontal center while dragging.
        private const double InfoPanelHorizontalSnapThreshold = 20.0;
        // Height of the Controls Overlay panel, cached (even when hidden),
        // so that the drag area of the timecode/BPM panel remains stable and does not
        // "jump" when the Controls Overlay appears or disappears.
        private double _controlsOverlayReservedHeight = 0.0;

        // Timecode interpolation
        private long _lastVlcTime = 0;
        private System.Diagnostics.Stopwatch _interpolationTimer = new System.Diagnostics.Stopwatch();
        private bool _isInterpolating = false;

        // Tempo track beat flash
        private TempoTrack? _tempoTrack = null;
        private System.Threading.Timer? _beatTimer = null; // Independent high-precision timer
        private double _nextScheduledBeat = -1.0; // The next beat to flash
        private double _lastDisplayedBpm = -1.0; // Used to smooth the displayed BPM
        private readonly object _beatTimerLock = new object();
        private AppSettings? _cachedStartupSettings;

        public MainWindow()
        {
            // Load settings and apply the saved language BEFORE loading the XAML,
            // because AXAML bindings (including the LocalizationService indexer) are
            // evaluated once when InitializeComponent() runs. Setting the language
            // after that point has no visible effect on this window's own controls.
            _cachedStartupSettings = LoadSettings();
            LocalizationService.Instance.SetLanguage(_cachedStartupSettings.Language ?? LocalizationService.DetectDefaultCulture().TwoLetterISOLanguageName);

            InitializeComponent();
            InitializeVLC();
            SetupControls();
            SetupDragAndDrop();
            SetupKeyboardHandling();
            UpdateVideoLayoutForWindowState();

            // High-precision timer for beats (independent of the video)
            _beatTimer = new System.Threading.Timer(OnBeatTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeVLC()
        {
            Core.Initialize();

            // Add audio output device option if one has been selected
            var options = new List<string>();
            if (!string.IsNullOrEmpty(_selectedAudioDeviceId))
            {
                options.Add($"--mmdevice-audio-device={_selectedAudioDeviceId}");
                Debug.WriteLine($"Initializing VLC with audio device: {_selectedAudioDeviceId}");
            }

            _libVLC = options.Count > 0 ? new LibVLC(options.ToArray()) : new LibVLC();

            _videoView = this.FindControl<VideoView>("VideoViewControl");
            if (_videoView != null)
            {
                _videoView.Initialize(_libVLC);
                _mediaPlayer = _videoView.MediaPlayer;

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = 100;

                    _mediaPlayer.Playing += OnMediaPlayerPlaying;
                    _mediaPlayer.Paused += OnMediaPlayerPaused;
                    _mediaPlayer.Stopped += OnMediaPlayerStopped;
                    _mediaPlayer.EndReached += OnMediaPlayerEndReached;
                }
            }

            _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS for smooth timecode
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        private void SetupControls()
        {
            _videoContainer = this.FindControl<Border>("VideoContainer");
            _placeholderText = this.FindControl<TextBlock>("PlaceholderText");
            _placeholderImage = this.FindControl<Image>("PlaceholderImage");
            _playPauseButton = this.FindControl<Button>("PlayPauseButton");
            _playPauseIcon = this.FindControl<SymbolIcon>("PlayPauseIcon");
            _stopButton = this.FindControl<Button>("StopButton");
            _loadButton = this.FindControl<Button>("LoadButton");
            _settingsButton = this.FindControl<Button>("SettingsButton");
            _aboutButton = this.FindControl<Button>("AboutButton");
            _volumeSlider = this.FindControl<Slider>("VolumeSlider");
            _positionSlider = this.FindControl<Slider>("PositionSlider");
            _timeLabel = this.FindControl<TextBlock>("TimeLabel");
            _durationLabel = this.FindControl<TextBlock>("DurationLabel");
            _audioTrackButton = this.FindControl<Button>("AudioTrackButton");
            _subtitleButton = this.FindControl<Button>("SubtitleButton");
            _subtitleButtonText = this.FindControl<TextBlock>("SubtitleButtonText");
            _audioTrackButtonText = this.FindControl<TextBlock>("AudioTrackButtonText");
            _volumeButton = this.FindControl<Button>("VolumeButton");
            _volumeIcon = this.FindControl<SymbolIcon>("VolumeIcon");
            _controlsOverlay = this.FindControl<Border>("ControlsOverlay");

            _balanceLockButton = this.FindControl<Button>("BalanceLockButton");
            _balanceLockIcon = this.FindControl<SymbolIcon>("BalanceLockIcon");
            _balanceSlider = this.FindControl<Slider>("BalanceSlider");
            _compactControlsRow = this.FindControl<Grid>("CompactControlsRow");
            _essentialButtonsPanel = this.FindControl<StackPanel>("EssentialButtonsPanel");
            _timeDisplayPanel = this.FindControl<StackPanel>("TimeDisplayPanel");
            _selectionGroupPanel = this.FindControl<StackPanel>("SelectionGroupPanel");
            _balanceGroupPanel = this.FindControl<StackPanel>("BalanceGroupPanel");
            _balanceSpacer = this.FindControl<Grid>("BalanceSpacer");
            _volumeGroupPanel = this.FindControl<StackPanel>("VolumeGroupPanel");
            SetupResponsiveControlsOverlay();
            // The Timecode checkbox is now managed from the settings window
            _timecodeCheckBox = new CheckBox { IsChecked = false };
            // The BPM checkbox is now managed from the settings window
            _bpmCheckBox = new CheckBox { IsChecked = false };
            // The time signature checkbox is now managed from the settings window
            _timeSignatureCheckBox = new CheckBox { IsChecked = false };
            // The bar/beat checkbox is now managed from the settings window
            _barBeatCheckBox = new CheckBox { IsChecked = false };
            // Added for the timecode overlay
            _timecodeLabel = this.FindControl<TextBlock>("TimecodeLabel");
            // Added for the BPM overlay and beat LED
            _bpmLabelOverlay = this.FindControl<TextBlock>("BpmLabelOverlay");
            // Added for the time signature overlay
            _timeSignatureLabelOverlay = this.FindControl<TextBlock>("TimeSignatureLabelOverlay");
            // Added for the bar/beat overlay
            _barBeatLabelOverlay = this.FindControl<TextBlock>("BarBeatLabelOverlay");
            _beatLed = this.FindControl<Border>("BeatLed");
            // Draggable panel grouping Timecode, BPM and LED
            _infoPanelCanvas = this.FindControl<Canvas>("InfoPanelCanvas");
            _infoPanel = this.FindControl<Border>("InfoPanel");
            SetupInfoPanelDrag();
            ApplyOverlayFontSize();
            ApplyInfoPanelOpacity();

            if (_timecodeCheckBox != null)
            {
                _timecodeCheckBox.IsCheckedChanged += (s, e) => { UpdateTimecodeVisibility(); SaveSettings(); };
            }

            if (_bpmCheckBox != null)
            {
                _bpmCheckBox.IsCheckedChanged += (s, e) => { UpdateTimecodeVisibility(); SaveSettings(); };
            }

            if (_timeSignatureCheckBox != null)
            {
                _timeSignatureCheckBox.IsCheckedChanged += (s, e) => { UpdateTimecodeVisibility(); SaveSettings(); };
            }

            if (_barBeatCheckBox != null)
            {
                _barBeatCheckBox.IsCheckedChanged += (s, e) => { UpdateTimecodeVisibility(); SaveSettings(); };
            }

            var balanceSlider = this.FindControl<Slider>("BalanceSlider");

            // Create invisible ComboBoxes for data storage (not in XAML)
            _audioTrackComboBox = new ComboBox { IsVisible = false };
            _audioOutputComboBox = new ComboBox { IsVisible = false };
            _audioTrackPanel = new Grid { IsVisible = false };

            // Setup hide controls timer
            _hideControlsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _hideControlsTimer.Tick += (s, e) =>
            {
                HideOverlays();
                _hideControlsTimer.Stop();
            };

            // Setup mouse move handler for showing controls
            if (_videoContainer != null)
            {
                _videoContainer.PointerMoved += VideoContainer_PointerMoved;
                _videoContainer.PointerExited += VideoContainer_PointerExited;
            }

            if (_playPauseButton != null)
                _playPauseButton.Click += PlayPauseButton_Click;

            if (_stopButton != null)
                _stopButton.Click += StopButton_Click;

            if (_loadButton != null)
                _loadButton.Click += LoadButton_Click;

            if (_settingsButton != null)
                _settingsButton.Click += SettingsButton_Click;

            if (_aboutButton != null)
                _aboutButton.Click += AboutButton_Click;

            if (_volumeSlider != null)
                _volumeSlider.PropertyChanged += VolumeSlider_PropertyChanged;

            if (_positionSlider != null)
            {
                _positionSlider.AddHandler(PointerPressedEvent, PositionSlider_PointerPressed, RoutingStrategies.Tunnel);
                _positionSlider.AddHandler(PointerReleasedEvent, PositionSlider_PointerReleased, RoutingStrategies.Tunnel);
                _positionSlider.AddHandler(PointerMovedEvent, PositionSlider_PointerMoved, RoutingStrategies.Tunnel);
                _positionSlider.AddHandler(PointerCaptureLostEvent, PositionSlider_PointerCaptureLost, RoutingStrategies.Tunnel);
            }

            if (_videoContainer != null)
            {
                _videoContainer.AddHandler(PointerWheelChangedEvent, VideoContainer_PointerWheelChanged, RoutingStrategies.Tunnel);
            }

            if (_audioTrackComboBox != null)
            {
                _audioTrackComboBox.SelectionChanged += AudioTrackComboBox_SelectionChanged;
            }

            if (_audioOutputComboBox != null)
            {
                _audioOutputComboBox.SelectionChanged += AudioOutputComboBox_SelectionChanged;
                LoadAudioOutputDevices();
            }

            if (_audioTrackButton != null)
            {
                _audioTrackButton.Click += AudioTrackButton_Click;
            }

            if (_subtitleButton != null)
            {
                _subtitleButton.Click += SubtitleButton_Click;
            }

            if (_volumeButton != null)
            {
                _volumeButton.Click += VolumeButton_Click;
            }

            if (_balanceLockButton != null)
            {
                _balanceLockButton.Click += BalanceLockButton_Click;
            }

            if (balanceSlider != null)
            {
                balanceSlider.PropertyChanged += BalanceSlider_PropertyChanged;
                balanceSlider.AddHandler(DoubleTappedEvent, BalanceSlider_DoubleTapped, RoutingStrategies.Tunnel);
            }

            // Load and apply saved settings (language already applied earlier, before InitializeComponent)
            var settings = _cachedStartupSettings ?? LoadSettings();
            _selectedAudioDeviceId = settings.AudioOutputDeviceId;
            ApplySettings(settings);
        }

        private void ApplySettings(AppSettings settings)
        {
            // Restore the saved position of the timecode/BPM panel first,
            // BEFORE assigning the checkboxes below: the latter trigger
            // their IsCheckedChanged handler which calls SaveSettings(), which was overwriting
            // the saved position with the default values if it wasn't already in memory.
            if (settings.InfoPanelRelativeX.HasValue && settings.InfoPanelRelativeY.HasValue)
            {
                _infoPanelRelativeX = settings.InfoPanelRelativeX.Value;
                _infoPanelRelativeY = settings.InfoPanelRelativeY.Value;
                _infoPanelPositionInitialized = true;
                ApplyInfoPanelRelativePosition();
            }

            // Apply volume
            if (_volumeSlider != null)
            {
                _volumeSlider.Value = settings.Volume;
                Debug.WriteLine($"✓ Applied saved volume: {settings.Volume}");
            }

            // Apply balance
            var balanceSlider = this.FindControl<Slider>("BalanceSlider");
            if (balanceSlider != null)
            {
                balanceSlider.Value = settings.Balance;
                // Apply balance to Windows audio
                float balance = (float)(settings.Balance / 100.0);
                if (WindowsAudio.SetAudioBalance(balance))
                {
                    Debug.WriteLine($"✓ Applied saved balance: {settings.Balance} ({balance:F2})");
                }
            }

            // Apply balance lock state
            _isBalanceLocked = settings.IsBalanceLocked;
            if (_balanceSlider != null)
            {
                _balanceSlider.IsEnabled = !_isBalanceLocked;
            }
            if (_balanceLockIcon != null)
            {
                _balanceLockIcon.Symbol = _isBalanceLocked
                    ? FluentIcons.Common.Symbol.LockClosed
                    : FluentIcons.Common.Symbol.LockOpen;
            }
            Debug.WriteLine($"✓ Applied saved balance lock state: {(_isBalanceLocked ? "Locked" : "Unlocked")}");

            // Apply the timecode checkbox state
            if (_timecodeCheckBox != null)
            {
                _timecodeCheckBox.IsChecked = settings.ShowTimecode;
            }

            // Apply the overlay font size (fallback to default for older settings files)
            _overlayFontSize = settings.OverlayFontSize > 0 ? settings.OverlayFontSize : 22.0;
            ApplyOverlayFontSize();

            // Apply the info panel background opacity (fallback to default for older settings files)
            _infoPanelOpacity = Math.Clamp(settings.InfoPanelOpacity > 0 ? settings.InfoPanelOpacity : 0x66 / 255.0, 0.0, 1.0);
            ApplyInfoPanelOpacity();

            // Apply the last used video directory
            _lastVideoDirectory = settings.LastVideoDirectory;

            // Apply the mouse wheel seek step
            _seekStepSeconds = settings.SeekStepSeconds > 0 ? settings.SeekStepSeconds : 5;

            // Apply the BPM checkbox state
            if (_bpmCheckBox != null)
            {
                _bpmCheckBox.IsChecked = settings.ShowBpm;
            }

            // Apply the time signature checkbox state
            if (_timeSignatureCheckBox != null)
            {
                _timeSignatureCheckBox.IsChecked = settings.ShowTimeSignature;
            }

            // Apply the bar/beat checkbox state
            if (_barBeatCheckBox != null)
            {
                _barBeatCheckBox.IsChecked = settings.ShowBarBeat;
            }
        }

        private void LoadAudioOutputDevices()
        {
            if (_libVLC == null || _audioOutputComboBox == null)
                return;

            try
            {
                _isUpdatingAudioOutput = true;

                var audioOutputDevices = new List<AudioOutputDevice>();

                // Get Windows default audio device ID
                string? windowsDefaultDeviceId = WindowsAudio.GetWindowsDefaultAudioDeviceId();
                string? windowsDefaultDeviceName = WindowsAudio.GetWindowsDefaultAudioDeviceName();

                Debug.WriteLine($"Windows default device - ID: {windowsDefaultDeviceId}, Name: {windowsDefaultDeviceName}");

                // Use LibVLC to enumerate audio output modules and devices
                using (var tempPlayer = new MediaPlayer(_libVLC))
                {
                    var devices = tempPlayer.AudioOutputDeviceEnum;

                    if (devices != null)
                    {
                        foreach (var device in devices)
                        {
                            if (!string.IsNullOrWhiteSpace(device.DeviceIdentifier))
                            {
                                var displayName = !string.IsNullOrWhiteSpace(device.Description)
                                    ? device.Description
                                    : device.DeviceIdentifier;

                                audioOutputDevices.Add(new AudioOutputDevice
                                {
                                    DeviceId = device.DeviceIdentifier,
                                    DeviceName = displayName
                                });

                                Debug.WriteLine($"Audio device found: {displayName} (ID: {device.DeviceIdentifier})");
                            }
                        }
                    }
                }

                if (audioOutputDevices.Count > 0)
                {
                    _audioOutputComboBox.ItemsSource = audioOutputDevices;

                    AudioOutputDevice? defaultDevice = null;

                    // Try to restore saved device first
                    if (!string.IsNullOrEmpty(_selectedAudioDeviceId))
                    {
                        defaultDevice = audioOutputDevices.FirstOrDefault(d =>
                            d.DeviceId.Equals(_selectedAudioDeviceId, StringComparison.OrdinalIgnoreCase));

                        if (defaultDevice != null)
                        {
                            Debug.WriteLine($"✓ Restored saved audio device: {defaultDevice.DeviceName}");
                        }
                        else
                        {
                            Debug.WriteLine($"⚠ Saved device not found (ID: {_selectedAudioDeviceId}), using system default");
                        }
                    }

                    // If saved device not found, try to match by Windows default device name
                    if (defaultDevice == null && !string.IsNullOrEmpty(windowsDefaultDeviceName))
                    {
                        defaultDevice = audioOutputDevices.FirstOrDefault(d =>
                            d.DeviceName.Contains(windowsDefaultDeviceName, StringComparison.OrdinalIgnoreCase));

                        if (defaultDevice != null)
                        {
                            Debug.WriteLine($"Found default device by name match: {defaultDevice.DeviceName}");
                        }
                    }

                    // If not found by name, try to match by partial device ID
                    if (defaultDevice == null && !string.IsNullOrEmpty(windowsDefaultDeviceId))
                    {
                        // Extract the GUID part from Windows device ID (format: {0.0.0.00000000}.{GUID})
                        var guidMatch = System.Text.RegularExpressions.Regex.Match(windowsDefaultDeviceId, @"\{([a-fA-F0-9\-]+)\}$");
                        if (guidMatch.Success)
                        {
                            string guid = guidMatch.Groups[1].Value;
                            defaultDevice = audioOutputDevices.FirstOrDefault(d =>
                                d.DeviceId.Contains(guid, StringComparison.OrdinalIgnoreCase));

                            if (defaultDevice != null)
                            {
                                Debug.WriteLine($"Found default device by GUID match: {defaultDevice.DeviceName}");
                            }
                        }
                    }

                    // Fallback to first device if no match found
                    if (defaultDevice == null)
                    {
                        defaultDevice = audioOutputDevices.FirstOrDefault();
                        Debug.WriteLine($"Using fallback (first device): {defaultDevice?.DeviceName ?? "none"}");
                    }

                    if (defaultDevice != null)
                    {
                        _audioOutputComboBox.SelectedItem = defaultDevice;
                        Debug.WriteLine($"Selected audio output device: {defaultDevice.DeviceName}");
                    }

                    Debug.WriteLine($"Loaded {audioOutputDevices.Count} audio output devices");
                }
                else
                {
                    Debug.WriteLine("No audio output devices found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading audio output devices: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _isUpdatingAudioOutput = false;
            }
        }

        private void AudioOutputComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingAudioOutput || _mediaPlayer == null || _audioOutputComboBox == null)
                return;

            if (_audioOutputComboBox.SelectedItem is AudioOutputDevice selectedDevice)
            {
                try
                {
                    Debug.WriteLine($"Attempting to change audio output to: {selectedDevice.DeviceName} (ID: {selectedDevice.DeviceId})");

                    // Store the current playback state
                    bool wasPlaying = _mediaPlayer.IsPlaying;
                    long currentTime = _mediaPlayer.Time;
                    int currentVolume = _mediaPlayer.Volume;
                    var currentMedia = _mediaPlayer.Media;
                    int? currentAudioTrackId = _selectedAudioTrackId ?? _mediaPlayer.AudioTrack;

                    Debug.WriteLine($"Saving current audio track ID: {currentAudioTrackId}");

                    // Save the selected device
                    _selectedAudioDeviceId = selectedDevice.DeviceId;

                    // Update the button text

                    // Stop current playback
                    if (wasPlaying)
                    {
                        _mediaPlayer.Stop();
                    }

                    // Dispose and recreate LibVLC with new audio device
                    _mediaPlayer.Playing -= OnMediaPlayerPlaying;
                    _mediaPlayer.Paused -= OnMediaPlayerPaused;
                    _mediaPlayer.Stopped -= OnMediaPlayerStopped;
                    _mediaPlayer.EndReached -= OnMediaPlayerEndReached;

                    _libVLC?.Dispose();

                    // Reinitialize with new audio device
                    var options = new List<string>
                    {
                        $"--mmdevice-audio-device={_selectedAudioDeviceId}"
                    };

                    Debug.WriteLine($"Reinitializing VLC with audio device: {_selectedAudioDeviceId}");
                    _libVLC = new LibVLC(options.ToArray());

                    // Reinitialize video view
                    if (_videoView != null)
                    {
                        _videoView.Initialize(_libVLC);
                        _mediaPlayer = _videoView.MediaPlayer;

                        if (_mediaPlayer != null)
                        {
                            _mediaPlayer.Volume = currentVolume;

                            _mediaPlayer.Playing += OnMediaPlayerPlaying;
                            _mediaPlayer.Paused += OnMediaPlayerPaused;
                            _mediaPlayer.Stopped += OnMediaPlayerStopped;
                            _mediaPlayer.EndReached += OnMediaPlayerEndReached;

                            // Resume playback if it was playing
                            if (wasPlaying && currentMedia != null && !string.IsNullOrEmpty(_currentVideoFilePath))
                            {
                                // Reload the video and restore audio track
                                _ = LoadAndPlayVideoWithAudioTrack(_currentVideoFilePath, currentTime, currentAudioTrackId);
                            }
                        }
                    }

                    Debug.WriteLine($"? Audio output successfully changed to: {selectedDevice.DeviceName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error changing audio output: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        private async Task LoadAndPlayVideoWithAudioTrack(string filePath, long restoreTime, int? audioTrackId)
        {
            await LoadAndPlayVideo(filePath);

            // Wait for the video to be fully loaded and audio tracks available
            await Task.Delay(2000);

            if (_mediaPlayer != null && audioTrackId.HasValue)
            {
                Debug.WriteLine($"Restoring audio track ID: {audioTrackId.Value}");
                _mediaPlayer.SetAudioTrack(audioTrackId.Value);
                _selectedAudioTrackId = audioTrackId.Value;

                // Update the UI selection
                UpdateAudioTrackSelection();
            }

            // Restore playback position
            if (_mediaPlayer != null && _mediaPlayer.Length > 0 && restoreTime > 0)
            {
                await Task.Delay(300);
                _mediaPlayer.Time = restoreTime;
                Debug.WriteLine($"Restored playback position to: {restoreTime}ms");
            }
        }

        private void SetupDragAndDrop()
        {
            if (_videoContainer != null)
            {
                _videoContainer.AddHandler(DragDrop.DropEvent, Drop);
                _videoContainer.AddHandler(DragDrop.DragOverEvent, DragOver);
            }
        }

        private void SetupKeyboardHandling()
        {
            this.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

            // Ensure the window can receive keyboard input
            this.Focusable = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            Debug.WriteLine($"OnKeyDown: Key pressed = {e.Key}, Handled = {e.Handled}");

            if (e.Key == Key.Space || e.Key == Key.Enter || e.Key == Key.Return)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: Space/Enter key pressed, calling TogglePlayPause");
                TogglePlayPause();
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: 0 key pressed, calling StopAndResetPosition");
                StopAndResetPosition();
            }
            else if (e.Key == Key.P)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: P key pressed, toggling settings window");
                ToggleSettingsWindow();
            }
            else if (e.Key == Key.I)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: I key pressed, toggling about window");
                ToggleAboutWindow();
            }
            else if (e.Key == Key.Left)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: Left key pressed, seeking backward");
                SeekRelative(-_seekStepSeconds * 1000L, "Clavier");
            }
            else if (e.Key == Key.Right)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: Right key pressed, seeking forward");
                SeekRelative(_seekStepSeconds * 1000L, "Clavier");
            }
        }

        private void DragOver(object? sender, Avalonia.Input.DragEventArgs e)
        {
            // Always allow copy operation for file drops
            e.DragEffects = DragDropEffects.Copy;
        }

        private async void Drop(object? sender, Avalonia.Input.DragEventArgs e)
        {
            try
            {
                var files = e.DataTransfer.TryGetFiles();
                if (files != null)
                {
                    var file = files.FirstOrDefault();
                    if (file != null)
                    {
                        var filePath = file.TryGetLocalPath();
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            await LoadAndPlayVideo(filePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling file drop: {ex.Message}");
            }
        }

        private async Task LoadAndPlayVideo(string filePath)
        {
            if (_mediaPlayer == null || _libVLC == null) return;

            try
            {
                if (_mediaPlayer.IsPlaying)
                {
                    _mediaPlayer.Stop();
                }

                _currentMedia?.Dispose();
                _currentVideoFilePath = filePath;
                _useHourFormat = false;

                // Load tempo track if .smt file exists
                var smtFilePath = IOPath.ChangeExtension(filePath, ".smt");
                _tempoTrack = TempoTrack.LoadFromFile(smtFilePath);
                if (_tempoTrack != null && _tempoTrack.IsLoaded)
                {
                    _lastDisplayedBpm = -1.0;
                    _lastDisplayedNumerator = -1;
                    _lastDisplayedDenominator = -1;
                    _lastDisplayedBar = -1;
                    _lastDisplayedBeat = -1;
                }
                else
                {
                    _tempoTrack = null;
                }

                // Update window title with video filename
                var videoFileName = IOPath.GetFileName(filePath);
                this.Title = $"{videoFileName} - DMX Video Player";

                var externalAudioFiles = FindExternalAudioFiles(filePath);

                if (externalAudioFiles.Count > 0)
                {
                    _currentMedia = new Media(_libVLC, filePath, FromType.FromPath);

                    var audiouris = externalAudioFiles.Select(af => new Uri(af.FilePath).AbsoluteUri);
                    var inputSlaveOption = string.Join("#", audiouris);

                    Debug.WriteLine($"Input-slave option: :input-slave={inputSlaveOption}");
                    _currentMedia.AddOption($":input-slave={inputSlaveOption}");
                }
                else
                {
                    _currentMedia = new Media(_libVLC, filePath, FromType.FromPath);
                }

                await _currentMedia.Parse(MediaParseOptions.ParseNetwork);

                _useHourFormat = TimeSpan.FromMilliseconds(_currentMedia.Duration).TotalHours >= 1;

                _mediaPlayer.Media = _currentMedia;

                if (_volumeSlider != null)
                {
                    _mediaPlayer.Volume = (int)_volumeSlider.Value;
                }

                if (externalAudioFiles.Count > 0)
                {
                    _mediaPlayer.Play();
                    await Task.Delay(100);
                    _mediaPlayer.Pause();

                    await Task.Delay(1500);

                    var audioTracks = _mediaPlayer.AudioTrackDescription;
                    if (audioTracks != null)
                    {
                        if (audioTracks.Length > 1)
                        {
                            var lastTrack = audioTracks[audioTracks.Length - 1];
                            _mediaPlayer.SetAudioTrack(lastTrack.Id);
                            _selectedAudioTrackId = lastTrack.Id;
                            Debug.WriteLine($"Initial audio track set to: {lastTrack.Id}");

                            await Task.Delay(200);
                        }
                    }

                    UpdateAudioTrackList(externalAudioFiles);
                    UpdateAudioTrackSelection();

                    _mediaPlayer.Play();

                    // Sync the beat timer after a short delay
                    SyncBeatTimer();
                }
                else
                {
                    _mediaPlayer.Play();
                    await Task.Delay(1000);
                    UpdateAudioTrackList(externalAudioFiles);

                    // Synchroniser le beat timer
                    SyncBeatTimer();
                }

                if (_placeholderText != null)
                    _placeholderText.IsVisible = false;
                if (_placeholderImage != null)
                    _placeholderImage.IsVisible = false;

                // Initialize subtitle button text
                UpdateSubtitleButtonText(LocalizationService.Instance["Main_SubtitleDisabled"]);

                // Show overlays initially when video loads
                ShowOverlays();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading video: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateAudioTrackList(List<ExternalAudioFile> externalAudioFiles)
        {
            if (_mediaPlayer == null || _audioTrackComboBox == null || _audioTrackPanel == null)
                return;

            try
            {
                _isUpdatingAudioTracks = true;

                var audioTracks = _mediaPlayer.AudioTrackDescription;
                Debug.WriteLine($"=== UpdateAudioTrackList ===");
                Debug.WriteLine($"Track count from VLC: {audioTracks?.Length ?? 0}");
                Debug.WriteLine($"External audio files count: {externalAudioFiles.Count}");

                if (audioTracks != null && audioTracks.Length > 0)
                {
                    var trackList = new List<AudioTrackItem>();

                    var reorderedFiles = new List<ExternalAudioFile>();
                    if (externalAudioFiles.Count > 1)
                    {
                        for (int i = 1; i < externalAudioFiles.Count; i++)
                        {
                            reorderedFiles.Add(externalAudioFiles[i]);
                        }
                        reorderedFiles.Add(externalAudioFiles[0]);
                    }
                    else if (externalAudioFiles.Count == 1)
                    {
                        reorderedFiles.Add(externalAudioFiles[0]);
                    }

                    Debug.WriteLine($"Reordered external files (VLC order):");
                    for (int i = 0; i < reorderedFiles.Count; i++)
                    {
                        Debug.WriteLine($"  [{i}]: {reorderedFiles[i].DisplayName}");
                    }

                    for (int i = 0; i < audioTracks.Length; i++)
                    {
                        var track = audioTracks[i];

                        if (track.Id == -1)
                        {
                            Debug.WriteLine($"  Skipping track {i} (Id=-1, disabled)");
                            continue;
                        }

                        string displayName;
                        int validTrackIndex = trackList.Count;

                        if (validTrackIndex == 0)
                        {
                            displayName = string.IsNullOrEmpty(track.Name) ? "Video audio" : track.Name;
                            Debug.WriteLine($"  Track {i} (Id={track.Id}): Video track -> '{displayName}'");
                        }
                        else
                        {
                            int externalIndex = validTrackIndex - 1;
                            if (externalIndex < reorderedFiles.Count)
                            {
                                displayName = reorderedFiles[externalIndex].DisplayName;
                                Debug.WriteLine($"  Track {i} (Id={track.Id}): Reordered[{externalIndex}] -> '{displayName}'");
                            }
                            else
                            {
                                displayName = string.IsNullOrEmpty(track.Name) ? $"Track {track.Id}" : track.Name;
                                Debug.WriteLine($"  Track {i} (Id={track.Id}): Fallback -> '{displayName}'");
                            }
                        }

                        trackList.Add(new AudioTrackItem
                        {
                            Id = track.Id,
                            Name = displayName
                        });
                    }

                    Debug.WriteLine($"Final track list:");
                    for (int i = 0; i < trackList.Count; i++)
                    {
                        Debug.WriteLine($"  [{i}] Id={trackList[i].Id}, Name={trackList[i].Name}");
                    }

                    _audioTrackComboBox.ItemsSource = trackList;

                    // Always show audio track selection if there are any tracks
                    _audioTrackPanel.IsVisible = trackList.Count > 0;
                    Debug.WriteLine($"Audio panel visible: {_audioTrackPanel.IsVisible}");

                    UpdateAudioTrackSelection();
                }
                else
                {
                    Debug.WriteLine("No audio tracks available");
                    _audioTrackPanel.IsVisible = false;
                }
            }
            finally
            {
                _isUpdatingAudioTracks = false;
            }
        }

        private void UpdateAudioTrackSelection()
        {
            if (_mediaPlayer == null || _audioTrackComboBox == null || _audioTrackComboBox.ItemsSource == null)
                return;

            try
            {
                _isUpdatingAudioTracks = true;

                var currentTrackId = _mediaPlayer.AudioTrack;
                var tracks = _audioTrackComboBox.ItemsSource as List<AudioTrackItem>;

                if (tracks != null)
                {
                    var selectedTrack = tracks.FirstOrDefault(t => t.Id == currentTrackId);
                    if (selectedTrack != null)
                    {
                        _audioTrackComboBox.SelectedItem = selectedTrack;
                        UpdateAudioTrackButtonText(selectedTrack.Name);
                    }
                }
            }
            finally
            {
                _isUpdatingAudioTracks = false;
            }
        }

        private void UpdateAudioTrackButtonText(string trackName)
        {
            if (_audioTrackButtonText != null)
            {
                _audioTrackButtonText.Text = trackName;
            }
        }

        private void AudioTrackComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingAudioTracks || _mediaPlayer == null || _audioTrackComboBox == null)
                return;

            if (_audioTrackComboBox.SelectedItem is AudioTrackItem selectedTrack)
            {
                _mediaPlayer.SetAudioTrack(selectedTrack.Id);
                _selectedAudioTrackId = selectedTrack.Id;
                UpdateAudioTrackButtonText(selectedTrack.Name);
                Debug.WriteLine($"Audio track changed to: {selectedTrack.Name} (ID: {selectedTrack.Id})");
            }
        }

        private void AudioTrackButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_audioTrackButton == null || _audioTrackComboBox == null)
                return;

            var menu = new ContextMenu();
            var selectedItem = _audioTrackComboBox.SelectedItem;

            if (_audioTrackComboBox.ItemsSource != null)
            {
                foreach (var item in _audioTrackComboBox.ItemsSource)
                {
                    var menuItem = new MenuItem
                    {
                        Header = item.ToString(),
                        Tag = item
                    };

                    menuItem.Click += (s, args) =>
                    {
                        if (menuItem.Tag is AudioTrackItem track)
                        {
                            _audioTrackComboBox.SelectedItem = track;
                        }
                    };

                    menu.Items.Add(menuItem);
                }
            }

            menu.PlacementTarget = _audioTrackButton;
            menu.Placement = PlacementMode.Top;
            menu.Open(_audioTrackButton);
        }

        private void ShowContextMenuFromComboBox(Control button, ComboBox comboBox, Action<object> onItemSelected)
        {
            if (comboBox.ItemsSource == null)
                return;

            var menu = new ContextMenu();
            var selectedItem = comboBox.SelectedItem;
            var accentColor = TryGetSystemAccentColor();

            foreach (var item in comboBox.ItemsSource)
            {
                var menuItem = new MenuItem
                {
                    Header = item.ToString(),
                    Tag = item
                };

                if (item == selectedItem)
                {
                    menuItem.Foreground = new SolidColorBrush(accentColor);
                    menuItem.FontWeight = FontWeight.Bold;
                }

                menuItem.Click += (s, args) =>
                {
                    if (menuItem.Tag != null)
                    {
                        onItemSelected(menuItem.Tag);
                    }
                };

                menu.Items.Add(menuItem);
            }

            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Top;
            menu.Open(button);
        }

        private void SubtitleButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_subtitleButton == null || _mediaPlayer == null)
                return;

            var subtitleTracks = new List<SubtitleTrackItem>();
            subtitleTracks.Add(new SubtitleTrackItem { Id = -1, Name = LocalizationService.Instance["Main_SubtitleDisabled"] });

            var spuDescriptions = _mediaPlayer.SpuDescription;
            if (spuDescriptions != null && spuDescriptions.Length > 0)
            {
                foreach (var desc in spuDescriptions)
                {
                    if (desc.Id != -1)
                    {
                        subtitleTracks.Add(new SubtitleTrackItem
                        {
                            Id = desc.Id,
                            Name = string.IsNullOrEmpty(desc.Name) ? string.Format(LocalizationService.Instance["Main_SubtitleTrackFormat"], desc.Id) : desc.Name
                        });
                    }
                }
            }

            var menu = new ContextMenu();
            var currentSpu = _mediaPlayer.Spu;

            foreach (var track in subtitleTracks)
            {
                var menuItem = new MenuItem
                {
                    Header = track.Name,
                    Tag = track
                };

                menuItem.Click += (s, args) =>
                {
                    if (menuItem.Tag is SubtitleTrackItem subTrack)
                    {
                        _mediaPlayer.SetSpu(subTrack.Id);
                        UpdateSubtitleButtonText(subTrack.Name);
                        Debug.WriteLine($"Subtitle track changed to: {subTrack.Name} (ID: {subTrack.Id})");
                    }
                };

                menu.Items.Add(menuItem);
            }

            menu.PlacementTarget = _subtitleButton;
            menu.Placement = PlacementMode.Top;
            menu.Open(_subtitleButton);
        }

        private void UpdateSubtitleButtonText(string trackName)
        {
            if (_subtitleButtonText != null)
            {
                _subtitleButtonText.Text = trackName;
            }
        }

        private Color TryGetSystemAccentColor()
        {
            try
            {
                if (Application.Current?.PlatformSettings != null)
                {
                    var accentColor = Application.Current.PlatformSettings.GetColorValues().AccentColor1;
                    if (accentColor != default)
                    {
                        return accentColor;
                    }
                }
            }
            catch
            {
                // Fallback silently
            }

            return Color.FromRgb(0, 120, 215);
        }

        private List<ExternalAudioFile> FindExternalAudioFiles(string videoFilePath)
        {
            var result = new List<ExternalAudioFile>();

            if (string.IsNullOrEmpty(videoFilePath) || !File.Exists(videoFilePath))
                return result;

            var directory = IOPath.GetDirectoryName(videoFilePath);
            var fileNameWithoutExtension = IOPath.GetFileNameWithoutExtension(videoFilePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExtension))
                return result;

            string[] audioExtensions = { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma" };

            var allFiles = Directory.GetFiles(directory, $"{fileNameWithoutExtension}_*.*");

            foreach (var file in allFiles)
            {
                var extension = IOPath.GetExtension(file).ToLowerInvariant();

                if (audioExtensions.Contains(extension))
                {
                    var fileName = IOPath.GetFileNameWithoutExtension(file);

                    if (fileName.StartsWith(fileNameWithoutExtension + "_"))
                    {
                        var suffix = fileName.Substring(fileNameWithoutExtension.Length + 1);

                        var displayName = char.ToUpper(suffix[0]) + suffix.Substring(1) + " track";

                        result.Add(new ExternalAudioFile
                        {
                            FilePath = file,
                            Suffix = suffix,
                            DisplayName = displayName
                        });

                        Debug.WriteLine($"Found external audio: {file} -> {displayName}");
                    }
                }
            }

            Debug.WriteLine($"Files in alphabetical order:");
            for (int i = 0; i < result.Count; i++)
            {
                Debug.WriteLine($"  [{i}]: {result[i].DisplayName} from {IOPath.GetFileName(result[i].FilePath)}");
            }

            return result;
        }

        private void TogglePlayPause()
        {
            if (_mediaPlayer == null)
            {
                Debug.WriteLine("TogglePlayPause: _mediaPlayer is null");
                return;
            }

            if (_mediaPlayer.Media == null)
            {
                Debug.WriteLine("TogglePlayPause: _mediaPlayer.Media is null");
                return;
            }

            // Check if media is actually playing by checking the state
            var state = _mediaPlayer.State;
            Debug.WriteLine($"TogglePlayPause: Current state = {state}");

            if (state == VLCState.Playing)
            {
                Debug.WriteLine("TogglePlayPause: Pausing playback");
                _mediaPlayer.Pause();
                _updateTimer?.Stop();
                StopBeatTimer();
            }
            else
            {
                Debug.WriteLine($"TogglePlayPause: Starting playback from state {state}");
                _mediaPlayer.Play();
                _updateTimer?.Start();

                // Sync the beat timer after a short delay to let the player change state
                Task.Delay(50).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => SyncBeatTimer());
                });

                Debug.WriteLine("TogglePlayPause: Play() called and timer started");
            }
        }

        private void PlayPauseButton_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("PlayPauseButton_Click: Button clicked");
            TogglePlayPause();
        }

        private void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("StopButton_Click: Stop button clicked");
            StopAndResetPosition();
        }

        private SettingsWindow? _settingsWindow;

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.Show(this);
        }

        private void ToggleSettingsWindow()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Close();
                return;
            }

            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.Show(this);
        }

        private AboutWindow? _aboutWindow;

        private void AboutButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_aboutWindow != null)
            {
                _aboutWindow.Activate();
                return;
            }

            _aboutWindow = new AboutWindow();
            _aboutWindow.Closed += (s, args) => _aboutWindow = null;
            _aboutWindow.Show(this);
        }

        private void ToggleAboutWindow()
        {
            if (_aboutWindow != null)
            {
                _aboutWindow.Close();
                return;
            }

            _aboutWindow = new AboutWindow();
            _aboutWindow.Closed += (s, args) => _aboutWindow = null;
            _aboutWindow.Show(this);
        }

        // API exposed for the settings window (audio output + timecode)
        public IEnumerable<AudioOutputDevice> GetAudioOutputDevices()
        {
            return (_audioOutputComboBox?.ItemsSource as IEnumerable<AudioOutputDevice>) ?? Enumerable.Empty<AudioOutputDevice>();
        }

        public AudioOutputDevice? GetSelectedAudioOutputDevice()
        {
            return _audioOutputComboBox?.SelectedItem as AudioOutputDevice;
        }

        public void SetSelectedAudioOutputDevice(AudioOutputDevice device)
        {
            if (_audioOutputComboBox != null)
            {
                _audioOutputComboBox.SelectedItem = device;
            }
        }

        public bool GetShowTimecode()
        {
            return _timecodeCheckBox?.IsChecked == true;
        }

        public void SetShowTimecode(bool value)
        {
            if (_timecodeCheckBox != null)
            {
                _timecodeCheckBox.IsChecked = value;
            }
        }

        public bool GetShowBpm()
        {
            return _bpmCheckBox?.IsChecked == true;
        }

        public void SetShowBpm(bool value)
        {
            if (_bpmCheckBox != null)
            {
                _bpmCheckBox.IsChecked = value;
            }
        }

        public bool GetShowTimeSignature()
        {
            return _timeSignatureCheckBox?.IsChecked == true;
        }

        public void SetShowTimeSignature(bool value)
        {
            if (_timeSignatureCheckBox != null)
            {
                _timeSignatureCheckBox.IsChecked = value;
            }
        }

        public bool GetShowBarBeat()
        {
            return _barBeatCheckBox?.IsChecked == true;
        }

        public void SetShowBarBeat(bool value)
        {
            if (_barBeatCheckBox != null)
            {
                _barBeatCheckBox.IsChecked = value;
            }
        }

        public string? GetLastVideoDirectory()
        {
            return _lastVideoDirectory;
        }

        public void SetLastVideoDirectory(string? path)
        {
            _lastVideoDirectory = string.IsNullOrWhiteSpace(path) ? null : path;
            SaveSettings();
        }

        public int GetSeekStepSeconds()
        {
            return _seekStepSeconds;
        }

        public void SetSeekStepSeconds(int seconds)
        {
            _seekStepSeconds = seconds < 1 ? 1 : seconds;
            SaveSettings();
        }

        public double GetOverlayFontSize()
        {
            return _overlayFontSize;
        }

        public void SetOverlayFontSize(double size)
        {
            _overlayFontSize = Math.Clamp(size, 16.0, 128.0);
            ApplyOverlayFontSize();
            SaveSettings();
        }

        public int GetInfoPanelOpacityPercent()
        {
            return (int)Math.Round(_infoPanelOpacity * 100.0);
        }

        public void SetInfoPanelOpacityPercent(int percent)
        {
            _infoPanelOpacity = Math.Clamp(percent, 0, 100) / 100.0;
            ApplyInfoPanelOpacity();
            SaveSettings();
        }

        /// <summary>
        /// Applies the current opacity to the background of the timecode/BPM info panel.
        /// </summary>
        private void ApplyInfoPanelOpacity()
        {
            if (_infoPanel != null)
            {
                byte alpha = (byte)Math.Round(Math.Clamp(_infoPanelOpacity, 0.0, 1.0) * 255.0);
                _infoPanel.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
            }
        }

        public string GetLanguage()
        {
            return LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName;
        }

        public void SetLanguage(string twoLetterCode)
        {
            LocalizationService.Instance.SetLanguage(twoLetterCode);
            SaveSettings();
        }

        private string GetEffectiveVideoStartDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_lastVideoDirectory) && Directory.Exists(_lastVideoDirectory))
            {
                return _lastVideoDirectory;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        private async void LoadButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var storageProvider = StorageProvider;

                if (storageProvider == null)
                {
                    Debug.WriteLine("StorageProvider is not available");
                    return;
                }

                var fileTypes = new FilePickerFileType[]
                {
                    new(LocalizationService.Instance["Main_VideoFilesFilter"])
                    {
                        Patterns = new[] { "*.mp4", "*.avi", "*.mkv", "*.mov", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.mpg", "*.mpeg", "*.3gp", "*.ts" }
                    },
                    FilePickerFileTypes.All
                };

                var options = new FilePickerOpenOptions
                {
                    Title = LocalizationService.Instance["Main_SelectVideoDialogTitle"],
                    AllowMultiple = false,
                    FileTypeFilter = fileTypes
                };

                var startDirectory = GetEffectiveVideoStartDirectory();
                try
                {
                    options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(startDirectory);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error resolving start directory '{startDirectory}': {ex.Message}");
                }

                var result = await storageProvider.OpenFilePickerAsync(options);

                if (result != null && result.Count > 0)
                {
                    var filePath = result[0].Path.LocalPath;
                    await LoadAndPlayVideo(filePath);

                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        _lastVideoDirectory = directory;
                        SaveSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening file dialog: {ex.Message}");
            }
        }

        private bool _isStopped = false;

        private void StopAndResetPosition()
        {
            if (_mediaPlayer == null || _mediaPlayer.Media == null) return;

            // Do nothing if we are already stopped: repeatedly pausing/resetting the
            // position while already stopped confuses the underlying VLC player and
            // can make the next Play() start without video or require several clicks.
            if (_isStopped)
            {
                Debug.WriteLine("StopAndResetPosition: Already stopped, ignoring");
                return;
            }

            Debug.WriteLine($"StopAndResetPosition: Current state = {_mediaPlayer.State}");

            // Pause the playback first, but only if it's actually playing.
            // Calling Pause() while already paused/stopped toggles playback and would start it again.
            if (_mediaPlayer.State == VLCState.Playing)
            {
                _mediaPlayer.Pause();
            }

            Debug.WriteLine("StopAndResetPosition: Paused, now resetting position");

            // Reset position to beginning
            _mediaPlayer.Time = 0;

            // Seeking while paused can leave the VLC video output showing a corrupted/garbled
            // frame (especially with hardware decoding), because the decoder pipeline needs a
            // frame to actually flow through before the renderer redraws correctly. Forcing a
            // single frame step after the seek makes VLC decode and display the correct frame
            // at the new position instead of the stale/corrupted one.
            if (_mediaPlayer.State == VLCState.Paused)
            {
                _mediaPlayer.NextFrame();
            }

            // Stop the update timer
            _updateTimer?.Stop();

            // Reset UI controls
            if (_positionSlider != null)
                _positionSlider.Value = 0;

            if (_timeLabel != null)
                _timeLabel.Text = FormatTime(0, _useHourFormat);

            _isStopped = true;

            Debug.WriteLine($"StopAndResetPosition: Complete. New state = {_mediaPlayer.State}");
        }

        private void VolumeSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Value" && _volumeSlider != null && _mediaPlayer != null)
            {
                _mediaPlayer.Volume = (int)_volumeSlider.Value;

                // Update mute state based on volume
                if (_volumeSlider.Value > 0 && _isMuted)
                {
                    _isMuted = false;
                    UpdateVolumeIcon();
                }
                else if (_volumeSlider.Value == 0 && !_isMuted)
                {
                    _isMuted = true;
                    UpdateVolumeIcon();
                }
            }
        }

        private void VolumeButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null || _volumeSlider == null)
                return;

            if (_isMuted)
            {
                // Unmute: restore previous volume
                _volumeSlider.Value = _volumeBeforeMute;
                _mediaPlayer.Volume = _volumeBeforeMute;
                _isMuted = false;
                Debug.WriteLine($"Volume unmuted: restored to {_volumeBeforeMute}");
            }
            else
            {
                // Mute: save current volume and set to 0
                _volumeBeforeMute = (int)_volumeSlider.Value;
                _volumeSlider.Value = 0;
                _mediaPlayer.Volume = 0;
                _isMuted = true;
                Debug.WriteLine($"Volume muted: saved volume {_volumeBeforeMute}");
            }

            UpdateVolumeIcon();
        }

        private void BalanceLockButton_Click(object? sender, RoutedEventArgs e)
        {
            _isBalanceLocked = !_isBalanceLocked;

            if (_balanceSlider != null)
            {
                _balanceSlider.IsEnabled = !_isBalanceLocked;
            }

            if (_balanceLockIcon != null)
            {
                _balanceLockIcon.Symbol = _isBalanceLocked
                    ? FluentIcons.Common.Symbol.LockClosed
                    : FluentIcons.Common.Symbol.LockOpen;
            }

            Debug.WriteLine($"Balance lock toggled: {(_isBalanceLocked ? "Locked" : "Unlocked")}");

            // Save settings
            SaveSettings();
        }

        private void BalanceSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Value" && sender is Slider slider)
            {
                double value = slider.Value;

                // Snap to center when value is between -25 and +25
                if (value > -25 && value < 25 && value != 0)
                {
                    slider.Value = 0;
                    Debug.WriteLine("Balance auto-centrée (snap to center)");
                    return;
                }

                float balance = (float)(slider.Value / 100.0);
                if (WindowsAudio.SetAudioBalance(balance))
                {
                    Debug.WriteLine($"Balance audio ajustée: {balance:F2} ({slider.Value})");
                }
            }
        }

        private void BalanceSlider_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.Value = 0;
                Debug.WriteLine("Balance audio réinitialisée au centre");
            }
        }

        private void UpdateVolumeIcon()
        {
            if (_volumeIcon == null || _volumeSlider == null)
                return;

            var volume = (int)_volumeSlider.Value;

            if (volume == 0 || _isMuted)
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.SpeakerMute;
            }
            else if (volume < 33)
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.Speaker0;
            }
            else if (volume < 66)
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.Speaker1;
            }
            else
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.Speaker2;
            }
        }

        private void PositionSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isDraggingPosition = true;
            _isUserInteractingWithSlider = true;
            Debug.WriteLine("PositionSlider: PointerPressed - Timer désactivé");
        }

        private void PositionSlider_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                if (!_isDraggingPosition)
                {
                    _isDraggingPosition = true;
                    _isUserInteractingWithSlider = true;
                    Debug.WriteLine("PositionSlider: PointerMoved with button pressed - Timer désactivé");
                }
                UpdateTimecodeLabelFromSlider();
            }
        }

        private void PositionSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            Debug.WriteLine("PositionSlider: PointerReleased - Mise à jour de la position");

            Dispatcher.UIThread.Post(() =>
            {
                if (_mediaPlayer != null && _positionSlider != null && _isUserInteractingWithSlider)
                {
                    float position = (float)(_positionSlider.Value / 100.0);
                    _mediaPlayer.Position = position;
                    Debug.WriteLine($"Position vidéo mise à jour: {position * 100:F1}%");

                    // Resync the beat timer after the seek with a short delay
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() => SyncBeatTimer());
                    });
                }
                UpdateTimecodeLabelFromSlider();
                _isDraggingPosition = false;
                _isUserInteractingWithSlider = false;
                Debug.WriteLine("PositionSlider: Timer réactivé");
            }, DispatcherPriority.Background);
        }

        private void PositionSlider_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            Debug.WriteLine("PositionSlider: PointerCaptureLost - Mise à jour de la position");

            Dispatcher.UIThread.Post(() =>
            {
                if (_mediaPlayer != null && _positionSlider != null && _isUserInteractingWithSlider)
                {
                    float position = (float)(_positionSlider.Value / 100.0);
                    _mediaPlayer.Position = position;
                    Debug.WriteLine($"Position vidéo mise à jour: {position * 100:F1}%");
                }
                UpdateTimecodeLabelFromSlider();
                _isDraggingPosition = false;
                _isUserInteractingWithSlider = false;
                Debug.WriteLine("PositionSlider: Timer réactivé");
            }, DispatcherPriority.Background);
        }

        private void VideoContainer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Treat wheel usage as user activity to prevent controls from auto-hiding
            ShowOverlays();
            _hideControlsTimer?.Stop();
            if (_currentMedia != null && _mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _hideControlsTimer?.Start();
            }

            e.Handled = true;

            long seekStepMs = _seekStepSeconds * 1000L;
            // Support both the classic vertical wheel and horizontal wheels/tilt
            // (e.g. Logitech MX Master 3s). Prefer the vertical delta when present,
            // otherwise fall back to the horizontal delta.
            double rawDelta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            long delta = rawDelta > 0 ? seekStepMs : -seekStepMs;

            SeekRelative(delta, "Molette");
        }

        /// <summary>
        /// Seeks the current media by the given offset (in milliseconds), shared by the
        /// mouse wheel and the keyboard arrow shortcuts. A positive delta seeks forward,
        /// a negative delta seeks backward. The resulting time is clamped to the media length.
        /// </summary>
        private void SeekRelative(long deltaMs, string source)
        {
            if (_mediaPlayer == null || _positionSlider == null)
                return;

            long length = _mediaPlayer.Length;
            if (length <= 0)
                return;

            long currentTime = _mediaPlayer.Time;
            long newTime = Math.Clamp(currentTime + deltaMs, 0, length);

            _mediaPlayer.Time = newTime;
            _positionSlider.Value = (double)newTime / length * 100;

            Debug.WriteLine($"PositionSlider: {source} - seek à {newTime}ms");

            UpdateTimecodeLabelFromSlider();

            Task.Delay(50).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() => SyncBeatTimer());
            });
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaPlayer == null) return;

            if (!_isDraggingPosition && _positionSlider != null)
            {
                _positionSlider.Value = _mediaPlayer.Position * 100;
            }

            if (_timeLabel != null)
            {
                _timeLabel.Text = FormatTime(_mediaPlayer.Time, _useHourFormat);
            }

            if (_durationLabel != null && _mediaPlayer.Length > 0)
            {
                _durationLabel.Text = FormatTime(_mediaPlayer.Length, _useHourFormat);
            }

            // Update timecode at 60 FPS for smooth display
            var isPlaying = _mediaPlayer.IsPlaying;
            if (_timecodeLabel != null && isPlaying)
            {
                var vlcTime = _mediaPlayer.Time;
                var fps = _mediaPlayer.Fps;

                // Interpolate time between VLC updates for smooth display
                long interpolatedTime;

                if (vlcTime != _lastVlcTime)
                {
                    // VLC time changed - reset interpolation
                    _lastVlcTime = vlcTime;
                    _interpolationTimer.Restart();
                    interpolatedTime = vlcTime;
                    _isInterpolating = true;
                }
                else if (_isInterpolating && _interpolationTimer.IsRunning)
                {
                    // Interpolate based on elapsed time
                    interpolatedTime = _lastVlcTime + _interpolationTimer.ElapsedMilliseconds;
                }
                else
                {
                    // Fallback to VLC time
                    interpolatedTime = vlcTime;
                }

                // Calculate timecode HH:MM:SS:FF using interpolated time
                var totalSeconds = (int)(interpolatedTime / 1000);
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                var frameInSecond = fps > 0 ? (int)((interpolatedTime % 1000) * fps / 1000.0) : 0;

                var timecodeText = $"TC {hours:D2}:{minutes:D2}:{seconds:D2}:{frameInSecond:D2} ";

                // Only update if text changed (avoid unnecessary layout)
                if (timecodeText != _lastTimecodeText)
                {
                    _timecodeLabel.Text = timecodeText;
                    _lastTimecodeText = timecodeText;
                }
            }

            // Update BPM display for tempo track
            if (isPlaying && _mediaPlayer.Time > 0)
            {
                var currentTimeInSeconds = _mediaPlayer.Time / 1000.0;
                UpdateBeatFlash(currentTimeInSeconds);
            }
        }

        private void OnMediaPlayerPlaying(object? sender, EventArgs e)
        {
            // Playback has actually (re)started, so a subsequent Stop is meaningful again.
            _isStopped = false;

            // Reset interpolation when playback starts
            _interpolationTimer.Reset();
            _isInterpolating = false;

            _updateTimer?.Start();

            Dispatcher.UIThread.Post(() =>
            {
                UpdatePlayPauseIcon(true);
                // Start auto-hide timer when playing
                _hideControlsTimer?.Start();
            });
        }

        private void OnMediaPlayerPaused(object? sender, EventArgs e)
        {
            _updateTimer?.Stop();

            // Stop interpolation when paused
            _interpolationTimer.Stop();
            _isInterpolating = false;

            Dispatcher.UIThread.Post(() =>
            {
                UpdatePlayPauseIcon(false);
                // Show controls when paused
                ShowOverlays();
                _hideControlsTimer?.Stop();
            });
        }

        private void OnMediaPlayerStopped(object? sender, EventArgs e)
        {
            _updateTimer?.Stop();

            Dispatcher.UIThread.Post(() =>
            {
                UpdatePlayPauseIcon(false);
                if (_positionSlider != null)
                    _positionSlider.Value = 0;
                if (_timeLabel != null)
                    _timeLabel.Text = FormatTime(0, _useHourFormat);
                // Show controls when stopped
                ShowOverlays();
                _hideControlsTimer?.Stop();
            });
        }

        private void OnMediaPlayerEndReached(object? sender, EventArgs e)
        {
            _updateTimer?.Stop();

            Dispatcher.UIThread.Post(() =>
            {
                UpdatePlayPauseIcon(false);

                // Reset position to beginning so the video can be replayed
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                }

                if (_positionSlider != null)
                    _positionSlider.Value = 0;

                if (_timeLabel != null)
                    _timeLabel.Text = FormatTime(0, _useHourFormat);
            });
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            if (_playPauseIcon != null)
            {
                _playPauseIcon.Symbol = isPlaying ? FluentIcons.Common.Symbol.Pause : FluentIcons.Common.Symbol.Play;
            }
        }

        private System.Timers.Timer? _singleClickTimer;
        private const int SingleClickDelay = 150; // ms
        private bool _doubleClickDetected = false;

        private void VideoContainer_Tapped(object? sender, TappedEventArgs e)
        {
            // Start a timer to defer the single click action
            _doubleClickDetected = false;
            _singleClickTimer?.Stop();
            _singleClickTimer = new System.Timers.Timer(SingleClickDelay);
            _singleClickTimer.Elapsed += (s, args) =>
            {
                _singleClickTimer?.Stop();
                if (!_doubleClickDetected)
                {
                    Dispatcher.UIThread.Post(() => TogglePlayPause());
                }
            };
            _singleClickTimer.AutoReset = false;
            _singleClickTimer.Start();
        }

        private void VideoContainer_DoubleTapped(object? sender, TappedEventArgs e)
        {
            _doubleClickDetected = true;
            _singleClickTimer?.Stop();
            ToggleFullScreen();
        }

        private void ToggleFullScreen()
        {
            if (WindowState == WindowState.FullScreen)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.FullScreen;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                UpdateVideoLayoutForWindowState();
            }
        }

        /// <summary>
        /// In fullscreen mode, the video area spans over the Controls Overlay row so the
        /// video fills the whole screen and the overlay floats on top of it (auto-hidden
        /// after a few seconds). In windowed mode, the video area only spans its own row so
        /// it never goes behind the Controls Overlay, which stays permanently visible.
        /// </summary>
        private void UpdateVideoLayoutForWindowState()
        {
            bool isFullScreen = WindowState == WindowState.FullScreen;

            if (_videoContainer != null)
            {
                Grid.SetRowSpan(_videoContainer, isFullScreen ? 2 : 1);
            }

            if (!isFullScreen)
            {
                // The Controls Overlay must never auto-hide in windowed mode.
                _hideControlsTimer?.Stop();
                ShowOverlays();
            }

            ApplyInfoPanelRelativePosition();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Save settings before closing
            SaveSettings();

            // Stop all timers
            _updateTimer?.Stop();
            _hideControlsTimer?.Stop();
            StopBeatTimer();

            // Dispose the beat timer and wait for any in-flight callback to fully
            // complete before touching _mediaPlayer. OnBeatTimerCallback runs on a
            // thread-pool thread and accesses _mediaPlayer concurrently; disposing
            // the MediaPlayer while that callback is still running corrupts the
            // native libvlc handle and crashes the process with an
            // ExecutionEngineException.
            using (var beatTimerStopped = new System.Threading.ManualResetEvent(false))
            {
                _beatTimer?.Dispose(beatTimerStopped);
                beatTimerStopped.WaitOne(TimeSpan.FromSeconds(2));
            }
            _beatTimer = null;

            // Note: the MediaPlayer instance is owned by VideoView, which already
            // stops playback and disposes it safely in OnDetachedFromVisualTree
            // (called when the window closes, before OnClosed runs). Touching
            // _mediaPlayer here would access an already-released native handle
            // and crash the process with an ExecutionEngineException.
            _mediaPlayer = null;

            _currentMedia?.Dispose();
            _libVLC?.Dispose();
        }

        private void VideoContainer_PointerMoved(object? sender, PointerEventArgs e)
        {
            ShowOverlays();
            _hideControlsTimer?.Stop();

            // Only start auto-hide timer if media is loaded and playing
            if (_currentMedia != null && _mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _hideControlsTimer?.Start();
            }
        }

        private void VideoContainer_PointerExited(object? sender, PointerEventArgs e)
        {
            _hideControlsTimer?.Stop();

            // Only start auto-hide timer if media is loaded and playing
            if (_currentMedia != null && _mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _hideControlsTimer?.Start();
            }
        }

        private void ShowOverlays()
        {
            // Always show controls overlay (even without media)
            if (_controlsOverlay != null)
                _controlsOverlay.IsVisible = true;

            // Afficher le timecode overlay
            UpdateTimecodeVisibility();
        }

        private void HideOverlays()
        {
            // The Controls Overlay must never auto-hide in windowed mode, only in fullscreen.
            if (WindowState != WindowState.FullScreen)
                return;

            // Don't hide if media player is paused or stopped
            if (_mediaPlayer != null && !_mediaPlayer.IsPlaying)
                return;

            // Don't hide controls if no media is loaded (to allow audio output selection)
            if (_currentMedia == null)
                return;

            if (_controlsOverlay != null)
                _controlsOverlay.IsVisible = false;

            // No longer hide the timecode here
        }

        private static string GetSettingsFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = IOPath.Combine(appDataPath, "DMXVideoPlayer");
            Directory.CreateDirectory(appFolder);
            return IOPath.Combine(appFolder, "settings.json");
        }

        private AppSettings LoadSettings()
        {
            try
            {
                string settingsPath = GetSettingsFilePath();

                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        Debug.WriteLine($"Settings loaded: AudioDeviceId={settings.AudioOutputDeviceId}, Volume={settings.Volume}, Balance={settings.Balance}, IsBalanceLocked={settings.IsBalanceLocked}");
                        return settings;
                    }
                }

                Debug.WriteLine("No settings file found, using defaults");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    AudioOutputDeviceId = _selectedAudioDeviceId,
                    Volume = _volumeSlider != null ? (int)_volumeSlider.Value : 100,
                    Balance = 0.0,
                    IsBalanceLocked = _isBalanceLocked,
                    ShowTimecode = _timecodeCheckBox != null && _timecodeCheckBox.IsChecked == true, // Save the timecode state
                    ShowBpm = _bpmCheckBox != null && _bpmCheckBox.IsChecked == true, // Save the BPM state
                    ShowTimeSignature = _timeSignatureCheckBox != null && _timeSignatureCheckBox.IsChecked == true, // Save the time signature state
                    ShowBarBeat = _barBeatCheckBox != null && _barBeatCheckBox.IsChecked == true, // Save the bar/beat state
                    LastVideoDirectory = _lastVideoDirectory,
                    SeekStepSeconds = _seekStepSeconds,
                    InfoPanelRelativeX = _infoPanelRelativeX,
                    InfoPanelRelativeY = _infoPanelRelativeY,
                    Language = LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName,
                    OverlayFontSize = _overlayFontSize,
                    InfoPanelOpacity = _infoPanelOpacity
                };
                var balanceSlider = this.FindControl<Slider>("BalanceSlider");
                if (balanceSlider != null)
                {
                    settings.Balance = balanceSlider.Value;
                }

                string settingsPath = GetSettingsFilePath();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsPath, json);

                Debug.WriteLine($"Settings saved: AudioDeviceId={settings.AudioOutputDeviceId}, Volume={settings.Volume}, Balance={settings.Balance}, IsBalanceLocked={settings.IsBalanceLocked}, ShowTimecode={settings.ShowTimecode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the current overlay font size to the timecode/tempo track overlay elements
        /// (timecode, BPM, bar.beat, time signature) and proportionally resizes the beat LED.
        /// </summary>
        private void ApplyOverlayFontSize()
        {
            double topPadding = _overlayFontSize * 4.0 / 22.0;

            if (_timecodeLabel != null)
            {
                _timecodeLabel.FontSize = _overlayFontSize;
                _timecodeLabel.Padding = new Thickness(0, topPadding, topPadding*3, 0);
            }

            if (_beatLed != null)
            {
                _beatLed.Width = _overlayFontSize;
                _beatLed.Height = _overlayFontSize;
                _beatLed.CornerRadius = new CornerRadius(Math.Max(2.0, _overlayFontSize * 8.0 / 22.0));
            }

            if (_bpmLabelOverlay != null)
            {
                _bpmLabelOverlay.FontSize = _overlayFontSize;
                _bpmLabelOverlay.Padding = new Thickness(0, topPadding, 0, 0);
            }


            if (_timeSignatureLabelOverlay != null)
            {
                _timeSignatureLabelOverlay.FontSize = _overlayFontSize;
                _timeSignatureLabelOverlay.Padding = new Thickness(topPadding*4, topPadding, 0, 0);
            }

            if (_barBeatLabelOverlay != null)
            {
                _barBeatLabelOverlay.FontSize = _overlayFontSize;
                _barBeatLabelOverlay.Padding = new Thickness(topPadding, topPadding, 0, 0);
            }

        }
        private void UpdateTimecodeVisibility()
        {
            if (_timecodeLabel != null && _timecodeCheckBox != null)
            {
                _timecodeLabel.IsVisible = _timecodeCheckBox.IsChecked == true;
            }

            // BPM visibility is now independent of the timecode
            bool bpmVisible = _bpmCheckBox != null && _bpmCheckBox.IsChecked == true && _tempoTrack != null && _tempoTrack.IsLoaded;
            if (_bpmLabelOverlay != null)
            {
                _bpmLabelOverlay.IsVisible = bpmVisible;
            }
            if (_beatLed != null)
            {
                _beatLed.IsVisible = bpmVisible;
            }

            // Time signature visibility is also independent of the timecode/BPM
            bool timeSignatureVisible = _timeSignatureCheckBox != null && _timeSignatureCheckBox.IsChecked == true && _tempoTrack != null && _tempoTrack.IsLoaded;
            if (_timeSignatureLabelOverlay != null)
            {
                _timeSignatureLabelOverlay.IsVisible = timeSignatureVisible;
            }

            // Bar/beat visibility is also independent of the timecode/BPM/signature
            bool barBeatVisible = _barBeatCheckBox != null && _barBeatCheckBox.IsChecked == true && _tempoTrack != null && _tempoTrack.IsLoaded;
            if (_barBeatLabelOverlay != null)
            {
                _barBeatLabelOverlay.IsVisible = barBeatVisible;
            }

            // The panel remains visible as long as at least one element is displayed
            if (_infoPanel != null)
            {
                bool timecodeVisible = _timecodeLabel?.IsVisible == true;
                _infoPanel.IsVisible = timecodeVisible || bpmVisible || timeSignatureVisible || barBeatVisible;
            }
        }

        private void SetupInfoPanelDrag()
        {
            if (_infoPanel == null || _infoPanelCanvas == null)
            {
                return;
            }

            _infoPanel.PointerPressed += InfoPanelDragHandle_PointerPressed;
            _infoPanel.PointerMoved += InfoPanelDragHandle_PointerMoved;
            _infoPanel.PointerReleased += InfoPanelDragHandle_PointerReleased;
            _infoPanelCanvas.SizeChanged += InfoPanelCanvas_SizeChanged;
            _infoPanel.SizeChanged += InfoPanelCanvas_SizeChanged;

            if (_controlsOverlay != null)
            {
                _controlsOverlay.SizeChanged += ControlsOverlay_SizeChanged;
                if (_controlsOverlay.Bounds.Height > 0)
                {
                    _controlsOverlayReservedHeight = _controlsOverlay.Bounds.Height;
                }
            }
        }

        private void ControlsOverlay_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_controlsOverlay == null || _controlsOverlay.Bounds.Height <= 0)
            {
                return;
            }

            if (Math.Abs(_controlsOverlayReservedHeight - _controlsOverlay.Bounds.Height) > 0.01)
            {
                _controlsOverlayReservedHeight = _controlsOverlay.Bounds.Height;
                ApplyInfoPanelRelativePosition();
            }
        }

        private void InfoPanelCanvas_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!_infoPanelPositionInitialized)
            {
                CenterInfoPanelIfNeeded();
            }
            else
            {
                ApplyInfoPanelRelativePosition();
            }
        }

        private void CenterInfoPanelIfNeeded()
        {
            if (_infoPanelPositionInitialized || _infoPanelCanvas == null || _infoPanel == null)
            {
                return;
            }

            if (_infoPanelCanvas.Bounds.Width <= 0 || _infoPanel.Bounds.Width <= 0)
            {
                return;
            }

            double maxTop = Math.Max(0, _infoPanelCanvas.Bounds.Height - _infoPanel.Bounds.Height);
            _infoPanelRelativeX = 0.5;
            _infoPanelRelativeY = maxTop > 0 ? Math.Min(1.0, 8.0 / maxTop) : 0.0;
            ApplyInfoPanelRelativePosition();
            _infoPanelPositionInitialized = true;
        }

        private void ApplyInfoPanelRelativePosition()
        {
            if (_infoPanelCanvas == null || _infoPanel == null)
            {
                return;
            }

            if (_infoPanelCanvas.Bounds.Width <= 0 || _infoPanelCanvas.Bounds.Height <= 0)
            {
                return;
            }

            double maxLeft = Math.Max(0, _infoPanelCanvas.Bounds.Width - _infoPanel.Bounds.Width);
            double maxTop = GetInfoPanelMaxTop();

            double left = maxLeft * _infoPanelRelativeX;
            double top = maxTop * _infoPanelRelativeY;

            Canvas.SetLeft(_infoPanel, left);
            Canvas.SetTop(_infoPanel, top);
        }

        /// <summary>
        /// Computes the maximum allowed vertical position for the info panel,
        /// to prevent it from being moved under the Controls Overlay panel (which
        /// would make the panel difficult to grab again to reposition it afterwards).
        /// </summary>
        private double GetInfoPanelMaxTop()
        {
            if (_infoPanelCanvas == null || _infoPanel == null)
            {
                return 0;
            }

            double maxTop = Math.Max(0, _infoPanelCanvas.Bounds.Height - _infoPanel.Bounds.Height);

            // The Controls Overlay only overlaps the video/info panel area in fullscreen mode;
            // in windowed mode it sits in its own row and never overlaps, so no restriction is needed.
            if (WindowState == WindowState.FullScreen && _controlsOverlayReservedHeight > 0)
            {
                double controlsOverlayTop = _infoPanelCanvas.Bounds.Height - _controlsOverlayReservedHeight;
                double limitedMaxTop = controlsOverlayTop - _infoPanel.Bounds.Height;
                maxTop = Math.Max(0, Math.Min(maxTop, limitedMaxTop));
            }

            return maxTop;
        }

        private void InfoPanelDragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_infoPanel == null)
            {
                return;
            }

            _isDraggingInfoPanel = true;
            _infoPanelDragPointerStart = e.GetPosition(this);
            _infoPanelDragOrigin = new Point(Canvas.GetLeft(_infoPanel), Canvas.GetTop(_infoPanel));
            e.Pointer.Capture(_infoPanel);
            e.Handled = true;
        }

        private void InfoPanelDragHandle_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDraggingInfoPanel || _infoPanel == null || _infoPanelCanvas == null)
            {
                return;
            }

            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _infoPanelDragPointerStart;
            double newLeft = _infoPanelDragOrigin.X + delta.X;
            double newTop = _infoPanelDragOrigin.Y + delta.Y;

            double maxLeft = Math.Max(0, _infoPanelCanvas.Bounds.Width - _infoPanel.Bounds.Width);
            double maxTop = GetInfoPanelMaxTop();

            newLeft = Math.Min(Math.Max(0, newLeft), maxLeft);
            newTop = Math.Min(Math.Max(0, newTop), maxTop);

            // Magnetic snap: when close enough to the horizontal center of the available
            // space, snap the panel exactly to the center to make re-centering easy.
            double centerLeft = maxLeft / 2.0;
            bool isSnappedToCenter = Math.Abs(newLeft - centerLeft) <= InfoPanelHorizontalSnapThreshold;
            if (isSnappedToCenter)
            {
                newLeft = centerLeft;
            }

            Canvas.SetLeft(_infoPanel, newLeft);
            Canvas.SetTop(_infoPanel, newTop);

            // Store the position as a percentage of the available space to keep it
            // consistent across window size changes (e.g. fullscreen toggle).
            _infoPanelRelativeX = isSnappedToCenter ? 0.5 : (maxLeft > 0 ? newLeft / maxLeft : 0.5);
            _infoPanelRelativeY = maxTop > 0 ? newTop / maxTop : 0.0;

            e.Handled = true;
        }

        private void InfoPanelDragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDraggingInfoPanel)
            {
                return;
            }

            _isDraggingInfoPanel = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            SaveSettings();
        }

        private static string FormatTime(long milliseconds, bool useHourFormat)
        {
            var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
            return useHourFormat
                ? $"{(int)time.TotalHours}:{time:mm\\:ss}"
                : $"{(int)time.TotalMinutes}:{time:ss}";
        }

        private void UpdateTimecodeLabelFromSlider()
        {
            if (_timecodeLabel != null && _mediaPlayer != null && _positionSlider != null)
            {
                var duration = _mediaPlayer.Length;
                var sliderPosition = _positionSlider.Value / 100.0;
                var ms = duration > 0 ? (long)(duration * sliderPosition) : 0;
                var fps = _mediaPlayer.Fps;

                // Calculate timecode
                var totalSeconds = (int)(ms / 1000);
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                var frameInSecond = fps > 0 ? (int)((ms % 1000) * fps / 1000.0) : 0;

                _timecodeLabel.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frameInSecond:D2}";
            }
        }

        private void UpdateTimecodeLabelFromPlayer()
        {
            if (_timecodeLabel != null && _mediaPlayer != null)
            {
                var ms = _mediaPlayer.Time;
                var fps = _mediaPlayer.Fps;

                // Calculate timecode
                var totalSeconds = (int)(ms / 1000);
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                var frameInSecond = fps > 0 ? (int)((ms % 1000) * fps / 1000.0) : 0;

                _timecodeLabel.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frameInSecond:D2}";
            }
        }
    }

    public class AudioOutputDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;

        public override string ToString() => DeviceName;
    }

    public class AudioTrackItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class SubtitleTrackItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class ExternalAudioFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class AppSettings
    {
        public string? AudioOutputDeviceId { get; set; }
        public int Volume { get; set; } = 100;
        public double Balance { get; set; } = 0.0;
        public bool IsBalanceLocked { get; set; } = false;
        public bool ShowTimecode { get; set; } = false; // Added for the timecode checkbox
        public bool ShowBpm { get; set; } = false; // Added for the BPM checkbox
        public bool ShowTimeSignature { get; set; } = false; // Added for the time signature checkbox
        public bool ShowBarBeat { get; set; } = false; // Added for the bar/beat checkbox
        public string? LastVideoDirectory { get; set; } // Last directory used for loading videos
        public int SeekStepSeconds { get; set; } = 5; // Step (in seconds) for the mouse wheel seek
        public double? InfoPanelRelativeX { get; set; } // Saved horizontal position of the timecode/BPM panel (0-1)
        public double? InfoPanelRelativeY { get; set; } // Saved vertical position of the timecode/BPM panel (0-1)
        public string? Language { get; set; } // Two-letter ISO language code ("fr" or "en"); null = auto-detect from OS
        public double OverlayFontSize { get; set; } = 22.0; // Font size for the timecode/tempo track overlay elements
        public double InfoPanelOpacity { get; set; } = 0x66 / 255.0; // Opacity (0.0-1.0) of the timecode/BPM info panel background
    }

    public partial class MainWindow
    {
        /// <summary>
        /// Synchronizes the beat timer with the current video position.
        /// Should be called on initial play, after a seek, or after pause/stop.
        /// </summary>
        private void SyncBeatTimer()
        {
            lock (_beatTimerLock)
            {
                // Stop the current timer
                _beatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _nextScheduledBeat = -1.0;

                if (_mediaPlayer == null || _tempoTrack == null || !_tempoTrack.IsLoaded)
                    return;

                // Note: We don't check IsPlaying here because this method is called right after Play()
                // and the state may not be synchronized yet

                var currentTime = _mediaPlayer.Time / 1000.0;

                // Find the next beat from the current position
                var nextBeat = _tempoTrack.GetNextBeatTime(currentTime);

                if (nextBeat < 0)
                    return; // No upcoming beats

                // Compute the delay until the next beat
                var delay = nextBeat - currentTime;

                if (delay <= 0)
                {
                    // Beat already passed, look for the next one
                    nextBeat = _tempoTrack.GetNextBeatTime(nextBeat + 0.001);
                    if (nextBeat < 0)
                        return;
                    delay = nextBeat - currentTime;
                }

                if (delay > 0)
                {
                    _nextScheduledBeat = nextBeat;
                    int delayMs = Math.Max(1, (int)(delay * 1000.0));
                    _beatTimer?.Change(delayMs, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Callback for the beat timer (high precision, independent of video frames)
        /// </summary>
        private void OnBeatTimerCallback(object? state)
        {
            try
            {
                if (_mediaPlayer == null || !_mediaPlayer.IsPlaying)
                    return;

                lock (_beatTimerLock)
                {
                    var currentTime = _mediaPlayer.Time / 1000.0;
                    var beatTime = _nextScheduledBeat;

                    if (beatTime < 0)
                        return;

                    // Flash le beat
                    var currentBpm = _tempoTrack?.GetBpmAtTime(beatTime) ?? 120.0;
                    var secondsPerBeat = 60.0 / currentBpm;
                    var flashDuration = (int)((secondsPerBeat * 0.15) * 1000.0);

                    Dispatcher.UIThread.Post(() => FlashBeatLed(true));

                    // Update the bar.beat label at the exact same moment as the LED flash,
                    // so both stay perfectly in sync (rather than waiting for the next
                    // ~16ms UI tick, which reads the interpolated VLC position).
                    UpdateBarBeatLabel(beatTime);

                    // Auto-off after the flash duration
                    Task.Delay(flashDuration).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() => FlashBeatLed(false));
                    });

                    // Planifier le prochain beat
                    ScheduleNextBeat();
                }
            }
            catch (ObjectDisposedException)
            {
                // The MediaPlayer/LibVLC instance was disposed concurrently
                // (e.g. window closing) while this callback was already running.
                // Safe to ignore: the window is shutting down.
            }
        }

        /// <summary>
        /// Schedules the next beat after the one that just occurred
        /// </summary>
        private void ScheduleNextBeat()
        {
            if (_tempoTrack == null || !_tempoTrack.IsLoaded)
                return;

            var currentBeat = _nextScheduledBeat;
            if (currentBeat < 0)
                return;

            // Find the next beat in the timeline
            var nextBeat = _tempoTrack.GetNextBeatTime(currentBeat + 0.001);

            if (nextBeat < 0)
            {
                _nextScheduledBeat = -1.0;
                return; // No more beats
            }

            // Compute the exact delay between the two beats
            var delay = nextBeat - currentBeat;

            if (delay > 0)
            {
                _nextScheduledBeat = nextBeat;
                int delayMs = Math.Max(1, (int)(delay * 1000.0));
                _beatTimer?.Change(delayMs, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Stops the beat timer
        /// </summary>
        private void StopBeatTimer()
        {
            lock (_beatTimerLock)
            {
                _beatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _nextScheduledBeat = -1.0;
                FlashBeatLed(false);
            }
        }

        private void UpdateBeatFlash(double currentTimeInSeconds)
        {
            if (_tempoTrack == null || !_tempoTrack.IsLoaded)
            {
                return;
            }

            // Display and update the BPM (with smoothing)
            var currentBpm = _tempoTrack.GetBpmAtTime(currentTimeInSeconds);
            var roundedBpm = Math.Round(currentBpm);

            if (_bpmLabelOverlay != null && Math.Abs(roundedBpm - _lastDisplayedBpm) >= 1.0)
            {
                _lastDisplayedBpm = roundedBpm;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_bpmLabelOverlay != null)
                    {
                        _bpmLabelOverlay.Text = $"{roundedBpm:F0} BPM";
                    }
                    UpdateTimecodeVisibility();
                });
            }
            else if (_lastDisplayedBpm < 0)
            {
                _lastDisplayedBpm = roundedBpm;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_bpmLabelOverlay != null)
                    {
                        _bpmLabelOverlay.Text = $"{roundedBpm:F0} BPM";
                    }
                    UpdateTimecodeVisibility();
                });
            }

            // Display and update the time signature (only when it changes)
            var (numerator, denominator) = _tempoTrack.GetTimeSignatureAtTime(currentTimeInSeconds);
            if (numerator != _lastDisplayedNumerator || denominator != _lastDisplayedDenominator)
            {
                _lastDisplayedNumerator = numerator;
                _lastDisplayedDenominator = denominator;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_timeSignatureLabelOverlay != null)
                    {
                        _timeSignatureLabelOverlay.Text = $"{numerator}/{denominator}";
                    }
                    UpdateTimecodeVisibility();
                });
            }

            // Display and update the bar.beat position (only when it changes).
            // While playing, the bar.beat label is driven exclusively by the high-precision
            // beat timer (OnBeatTimerCallback), which uses the exact scheduled beat time.
            // Here we would instead read the interpolated VLC position (~16ms UI tick),
            // which can briefly disagree with the beat timer and cause the label to flicker
            // back and forth (e.g. 27 then 26). So we only update it from here when not
            // actively playing (paused/stopped/scrubbing).
            if (_mediaPlayer == null || !_mediaPlayer.IsPlaying)
            {
                UpdateBarBeatLabel(currentTimeInSeconds);
            }

            // Beats are handled by the independent timer, not here
        }

        /// <summary>
        /// Updates the bar.beat overlay text if it changed, on the UI thread.
        /// Called both from the high-precision beat timer (exact beat time, for perfect
        /// sync with the LED flash) and from the regular UI tick (for scrubbing/pause).
        /// </summary>
        private void UpdateBarBeatLabel(double timeInSeconds)
        {
            if (_tempoTrack == null || !_tempoTrack.IsLoaded)
                return;

            var (bar, beat) = _tempoTrack.GetBarBeatAtTime(timeInSeconds);
            if (bar != _lastDisplayedBar || beat != _lastDisplayedBeat)
            {
                _lastDisplayedBar = bar;
                _lastDisplayedBeat = beat;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_barBeatLabelOverlay != null)
                    {
                        _barBeatLabelOverlay.Text = $"{bar}.{beat}";
                    }
                    UpdateTimecodeVisibility();
                });
            }
        }

        private void FlashBeatLed(bool flash)
        {
            if (_beatLed == null)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (_beatLed == null)
                    return;

                if (flash)
                {
                    // LED on with intense glow
                    _beatLed.Opacity = 1.0;
                }
                else
                {
                    // LED off (but visible with reduced opacity)
                    _beatLed.Opacity = 0.3;
                }
            });
        }

        // === Responsive Controls Overlay ===
        // Const fixed widths of the two 50px spacer columns in CompactControlsRow
        private const double SpacerColumnWidth = 50;
        // Padding of the ControlsOverlay border (10,5,10,5 => 20 horizontal)
        private const double ControlsOverlayHorizontalPadding = 20;

        private void SetupResponsiveControlsOverlay()
        {
            this.Opened += (s, e) => AdjustControlsOverlayLayout();

            this.SizeChanged += (s, e) => AdjustControlsOverlayLayout();
        }

        private static double MeasureDesiredWidth(Control? control)
        {
            if (control == null)
                return 0;

            control.Measure(Size.Infinity);
            return control.DesiredSize.Width;
        }

        private void SetBalanceGroupVisible(bool visible)
        {
            if (_balanceGroupPanel != null)
                _balanceGroupPanel.IsVisible = visible;

            if (_balanceSpacer != null)
                _balanceSpacer.IsVisible = visible;

            if (_compactControlsRow != null && _compactControlsRow.ColumnDefinitions.Count > 5)
                _compactControlsRow.ColumnDefinitions[5].Width = new GridLength(visible ? SpacerColumnWidth : 0);
        }

        /// <summary>
        /// Progressively hides the least important components of the ControlsOverlay
        /// (in order: balance, audio track, subtitles, position/duration) when
        /// the available width no longer allows all of them to be displayed.
        /// </summary>
        private void AdjustControlsOverlayLayout()
        {
            if (_compactControlsRow == null || _essentialButtonsPanel == null || _volumeGroupPanel == null)
                return;

            // Show everything by default before recalculating what needs to be hidden.
            SetBalanceGroupVisible(true);
            if (_audioTrackButton != null)
                _audioTrackButton.IsVisible = true;
            if (_subtitleButton != null)
                _subtitleButton.IsVisible = true;
            if (_timeDisplayPanel != null)
                _timeDisplayPanel.IsVisible = true;

            double available = (_controlsOverlay?.Bounds.Width ?? this.ClientSize.Width) - ControlsOverlayHorizontalPadding;
            if (available <= 0)
                available = this.ClientSize.Width - ControlsOverlayHorizontalPadding;

            double RequiredWidth()
            {
                double width = MeasureDesiredWidth(_essentialButtonsPanel);

                if (_timeDisplayPanel?.IsVisible == true)
                    width += MeasureDesiredWidth(_timeDisplayPanel);

                width += SpacerColumnWidth; // Fixed space between block 1 and selection block

                if ((_audioTrackButton?.IsVisible ?? false) || (_subtitleButton?.IsVisible ?? false))
                    width += MeasureDesiredWidth(_selectionGroupPanel);

                if (_balanceGroupPanel?.IsVisible == true)
                {
                    width += MeasureDesiredWidth(_balanceGroupPanel);
                    width += SpacerColumnWidth; // Espace fixe entre balance et volume
                }

                width += MeasureDesiredWidth(_volumeGroupPanel);

                return width;
            }

            if (RequiredWidth() > available)
                SetBalanceGroupVisible(false);

            if (RequiredWidth() > available && _audioTrackButton != null)
                _audioTrackButton.IsVisible = false;

            if (RequiredWidth() > available && _subtitleButton != null)
                _subtitleButton.IsVisible = false;

            if (RequiredWidth() > available && _timeDisplayPanel != null)
                _timeDisplayPanel.IsVisible = false;
        }

    }
}

