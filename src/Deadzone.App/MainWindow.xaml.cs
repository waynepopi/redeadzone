using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Deadzone.App.Input;
using Deadzone.App.Output;
using Deadzone.App.Services;
using Deadzone.App.Settings;
using Deadzone.Core.Models;
using Deadzone.Core.Processing;

namespace Deadzone.App
{
    public partial class MainWindow : Window
    {
        // ── Geometry constants ──────────────────────────────────────────────
        private const double VisualizerCenter     = 115.0;
        private const double VisualizerUsableRadius = 100.0;
        private const double OuterRadius          = 105.0;
        private const double RawDotSize           = 8.0;
        private const double OutputDotSize        = 12.0;

        // ── Core services ───────────────────────────────────────────────────
        private readonly SettingsService  _settingsService;
        private readonly ControllerPipeline _pipeline;
        private readonly TrayIconService  _trayIcon;
        private AppSettings _appSettings;

        // ── UI timer ────────────────────────────────────────────────────────
        private readonly DispatcherTimer _uiTimer;
        private bool _isTransitioning;
        private bool _suppressComboEvents;
        private bool _suppressSliderEvents;

        // ── Drift analysis ──────────────────────────────────────────────────
        // Each analysis runs for 3 s collecting raw samples at ~30 Hz GUI tick
        private enum DriftStick { None, Left, Right }
        private DriftStick _driftActive        = DriftStick.None;
        private int        _driftTicksRemaining = 0;
        private const int  DriftTotalTicks      = 90;   // 3 s × 30 Hz

        private float _leftDriftMaxRadius  = 0f;
        private float _rightDriftMaxRadius = 0f;
        private float _leftSuggested       = 0f;
        private float _rightSuggested      = 0f;

        // Drift sample dots drawn on canvas during analysis
        private readonly List<Ellipse> _leftDriftDots  = new();
        private readonly List<Ellipse> _rightDriftDots = new();

        public MainWindow()
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            _appSettings     = _settingsService.Load();

            var input  = new SdlControllerInput();
            var output = new HidMaestroVirtualController();
            _pipeline  = new ControllerPipeline(input, output, _settingsService, _appSettings);

            // Apply persisted deadzones from the preferred controller (will be
            // overridden once a physical controller is opened by the pipeline)
            var dz = new DeadzoneSettings(0.08f, 0.08f);
            if (!string.IsNullOrWhiteSpace(_appSettings.PreferredControllerKey)
                && _appSettings.Controllers.TryGetValue(_appSettings.PreferredControllerKey, out var prefCtrl))
            {
                dz = new DeadzoneSettings(prefCtrl.LeftStickDeadzone, prefCtrl.RightStickDeadzone);
            }
            _pipeline.UpdateSettings(dz);

            // Initialise slider / percentage displays to match persisted values
            _suppressSliderEvents = true;
            LeftDeadzoneSlider.Value  = Math.Round(dz.LeftStickDeadzone  * 100.0);
            RightDeadzoneSlider.Value = Math.Round(dz.RightStickDeadzone * 100.0);
            _suppressSliderEvents = false;
            UpdateDeadzoneDisplay();

            EnableButton.IsEnabled = false;
            EnableButton.Content   = "ENABLE";
            EnableButton.Style     = (Style)FindResource("PrimaryButtonStyle");

            UpdateLeftStickPreview(0, 0, 0, 0);
            UpdateRightStickPreview(0, 0, 0, 0);

            _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _uiTimer.Tick += UiTimer_Tick;

            _trayIcon = new TrayIconService(this);
            _trayIcon.OnRestoreRequested = RestoreFromTray;
            _trayIcon.OnExitRequested    = () => Application.Current.Shutdown();

            Loaded  += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _trayIcon.Initialize();

            try
            {
                ApplicationStatusText.Text = "Starting…";
                _pipeline.Initialize();
                _pipeline.StartMonitoring();
                _uiTimer.Start();
                ApplicationStatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                ApplicationStatusText.Text = $"Init error: {ex.Message}";
            }

            // Initial controller list
            RefreshControllerComboBox();

            // If --startup argument and StartMinimizedToTray → hide immediately
            string[] args = Environment.GetCommandLineArgs();
            bool isStartup = Array.Exists(args, a =>
                string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase));
            if (isStartup && _appSettings.StartMinimizedToTray)
            {
                HideToTray();
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Close button always exits; never minimize to tray (spec §Close rule)
            _uiTimer.Stop();
            _settingsService.Save(_appSettings);
            _pipeline.Dispose();
            _trayIcon.Dispose();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _appSettings.MinimizeToTray)
            {
                HideToTray();
            }
        }

        private void HideToTray()
        {
            _trayIcon.ShowTrayIcon();
            Hide();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _trayIcon.RemoveTrayIcon();
        }

        // ── UI Timer (30 Hz) ─────────────────────────────────────────────────

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            UpdateControllerStatus();
            UpdateStickVisuals();
            UpdateDriftSampling();
            RefreshControllerComboBoxIfNeeded();
        }

        private void UpdateControllerStatus()
        {
            bool connected = _pipeline.IsControllerConnected;

            ControllerStatusDot.Fill  = connected
                ? new SolidColorBrush(Color.FromRgb(0x2B, 0xBF, 0x4B))
                : new SolidColorBrush(Color.FromRgb(0x59, 0x63, 0x6F));

            string connLabel = _pipeline.CurrentDescriptor?.ConnectionLabel ?? "Disconnected";
            ControllerStatusText.Text = connected
                ? $"{_pipeline.ControllerName}  ·  {connLabel}"
                : "Controller disconnected";

            ConnectionStatusText.Text = connected ? connLabel : "Disconnected";

            EnableButton.IsEnabled = connected && !_isTransitioning;

            if (!_pipeline.IsOutputEnabled && (string)EnableButton.Content == "DISABLE")
            {
                EnableButton.Content = "ENABLE";
                EnableButton.Style   = (Style)FindResource("PrimaryButtonStyle");
            }

            if (_pipeline.LastError is string err && err.Length > 0 && !connected)
                ApplicationStatusText.Text = err;
        }

        private void UpdateStickVisuals()
        {
            var frame = _pipeline.LatestFrame;
            if (frame == null)
            {
                UpdateLeftStickPreview(0, 0, 0, 0);
                UpdateRightStickPreview(0, 0, 0, 0);
                return;
            }

            UpdateLeftStickPreview(
                frame.Raw.LeftStick.X, frame.Raw.LeftStick.Y,
                frame.Processed.LeftStick.X, frame.Processed.LeftStick.Y);

            UpdateRightStickPreview(
                frame.Raw.RightStick.X, frame.Raw.RightStick.Y,
                frame.Processed.RightStick.X, frame.Processed.RightStick.Y);
        }

        // Fingerprint of available controllers + preferred key + active key
        private string _lastControllerSnapshotFingerprint = string.Empty;

        private void RefreshControllerComboBoxIfNeeded()
        {
            var available = _pipeline.AvailableControllers;
            string? preferredKey = _appSettings.PreferredControllerKey;
            string? currentKey = _pipeline.CurrentDescriptor?.Key;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < available.Count; i++)
            {
                sb.Append(available[i].Key).Append(':').Append(available[i].DisplayName).Append(';');
            }
            sb.Append("pref=").Append(preferredKey ?? "").Append(';');
            sb.Append("curr=").Append(currentKey ?? "");

            string fingerprint = sb.ToString();
            if (fingerprint != _lastControllerSnapshotFingerprint)
            {
                _lastControllerSnapshotFingerprint = fingerprint;
                PopulateControllerComboBox(available, preferredKey, currentKey);
            }
        }

        // ── Controller ComboBox ──────────────────────────────────────────────

        private void RefreshControllerComboBox()
        {
            _pipeline.RefreshAvailableControllers();
            _lastControllerSnapshotFingerprint = string.Empty;
            RefreshControllerComboBoxIfNeeded();
        }

        private void PopulateControllerComboBox(IReadOnlyList<ControllerDescriptor> available, string? preferredKey, string? currentKey)
        {
            _suppressComboEvents = true;
            try
            {
                ControllerComboBox.Items.Clear();

                bool preferredIsConnected = !string.IsNullOrWhiteSpace(preferredKey)
                    && available.Any(c => string.Equals(c.Key, preferredKey, StringComparison.OrdinalIgnoreCase));

                // If preferred controller is absent, show it as disconnected (spec §4, §11)
                if (!string.IsNullOrWhiteSpace(preferredKey) && !preferredIsConnected)
                {
                    string prefDisplayName = "Preferred Controller";
                    if (_appSettings.Controllers.TryGetValue(preferredKey, out var savedPref) && !string.IsNullOrWhiteSpace(savedPref.DisplayName))
                    {
                        prefDisplayName = savedPref.DisplayName;
                    }

                    ControllerComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = $"{prefDisplayName} — Disconnected",
                        Tag     = preferredKey
                    });
                }

                // Add all currently connected SDL gamepads (spec §3, §10)
                foreach (var d in available)
                {
                    ControllerComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = d.DisplayName,
                        Tag     = d.Key
                    });
                }

                // Selection handling: select active controller or disconnected preferred (spec §11)
                if (!string.IsNullOrWhiteSpace(currentKey))
                {
                    for (int i = 0; i < ControllerComboBox.Items.Count; i++)
                    {
                        if (ControllerComboBox.Items[i] is ComboBoxItem item
                            && string.Equals((string?)item.Tag, currentKey, StringComparison.OrdinalIgnoreCase))
                        {
                            ControllerComboBox.SelectedIndex = i;
                            return;
                        }
                    }
                }

                if (!preferredIsConnected && !string.IsNullOrWhiteSpace(preferredKey) && ControllerComboBox.Items.Count > 0)
                {
                    ControllerComboBox.SelectedIndex = 0;
                    return;
                }

                if (ControllerComboBox.Items.Count > 0)
                {
                    ControllerComboBox.SelectedIndex = 0;
                }
            }
            finally
            {
                _suppressComboEvents = false;
            }
        }

        private async void ControllerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboEvents) return;
            if (ControllerComboBox.SelectedItem is not ComboBoxItem item) return;
            string? key = (string?)item.Tag;
            if (key == null) return;

            // If same controller is already active, no-op
            if (string.Equals(_pipeline.CurrentDescriptor?.Key, key, StringComparison.OrdinalIgnoreCase))
                return;

            ApplicationStatusText.Text = "Switching controller…";
            var (success, error, loadedDz) = await _pipeline.SwitchControllerAsync(key);

            if (success)
            {
                // Update sliders to loaded per-controller deadzones (spec §19, §20)
                _suppressSliderEvents = true;
                LeftDeadzoneSlider.Value  = Math.Round(loadedDz.LeftStickDeadzone  * 100.0);
                RightDeadzoneSlider.Value = Math.Round(loadedDz.RightStickDeadzone * 100.0);
                _suppressSliderEvents = false;
                UpdateDeadzoneDisplay();
                ApplicationStatusText.Text = "Ready";
            }
            else
            {
                ApplicationStatusText.Text = error ?? "Switch failed.";
                // Revert ComboBox to active controller
                _lastControllerSnapshotFingerprint = string.Empty;
                RefreshControllerComboBoxIfNeeded();
            }
        }

        private void RefreshControllersButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshControllerComboBox();
        }

        // ── Enable / Disable ─────────────────────────────────────────────────

        private async void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTransitioning) return;
            _isTransitioning  = true;
            EnableButton.IsEnabled = false;

            try
            {
                if (!_pipeline.IsOutputEnabled)
                {
                    ApplicationStatusText.Text = "Starting virtual controller…";
                    bool success = await _pipeline.EnableOutputAsync();
                    if (success)
                    {
                        EnableButton.Content = "DISABLE";
                        EnableButton.Style   = (Style)FindResource("DangerButtonStyle");
                        ApplicationStatusText.Text = "Virtual controller active";
                    }
                    else
                    {
                        ApplicationStatusText.Text = _pipeline.LastError ?? "Enable failed.";
                    }
                }
                else
                {
                    ApplicationStatusText.Text = "Stopping virtual controller…";
                    await _pipeline.DisableOutputAsync();
                    EnableButton.Content = "ENABLE";
                    EnableButton.Style   = (Style)FindResource("PrimaryButtonStyle");
                    ApplicationStatusText.Text = "Ready";
                }
            }
            finally
            {
                _isTransitioning = false;
                EnableButton.IsEnabled = _pipeline.IsControllerConnected;
            }
        }

        // ── Settings ─────────────────────────────────────────────────────────

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settingsService, _appSettings) { Owner = this };
            win.ShowDialog();
            // Re-read (SettingsWindow may have mutated _appSettings in place)
            _pipeline.UpdateAppSettings(_appSettings);
        }

        // ── Drift Analysis ───────────────────────────────────────────────────

        private void LeftAnalyzeDriftButton_Click(object sender, RoutedEventArgs e)
        {
            if (_driftActive != DriftStick.None) return;
            if (!_pipeline.IsControllerConnected) return;
            StartDriftAnalysis(DriftStick.Left);
        }

        private void RightAnalyzeDriftButton_Click(object sender, RoutedEventArgs e)
        {
            if (_driftActive != DriftStick.None) return;
            if (!_pipeline.IsControllerConnected) return;
            StartDriftAnalysis(DriftStick.Right);
        }

        private void StartDriftAnalysis(DriftStick stick)
        {
            ClearDriftDots();
            _driftActive         = stick;
            _driftTicksRemaining = DriftTotalTicks;

            if (stick == DriftStick.Left)
            {
                _leftDriftMaxRadius = 0f;
                LeftDriftSuggestionText.Text = "Hold the stick still for 3 seconds…";
                LeftApplyDriftButton.Visibility  = Visibility.Collapsed;
                LeftAnalyzeDriftButton.IsEnabled = false;
            }
            else
            {
                _rightDriftMaxRadius = 0f;
                RightDriftSuggestionText.Text = "Hold the stick still for 3 seconds…";
                RightApplyDriftButton.Visibility  = Visibility.Collapsed;
                RightAnalyzeDriftButton.IsEnabled = false;
            }
        }

        private void UpdateDriftSampling()
        {
            if (_driftActive == DriftStick.None) return;

            var frame = _pipeline.LatestFrame;
            if (frame == null) return;

            bool isLeft = _driftActive == DriftStick.Left;

            float rawX = isLeft ? frame.Raw.LeftStick.X : frame.Raw.RightStick.X;
            float rawY = isLeft ? frame.Raw.LeftStick.Y : frame.Raw.RightStick.Y;

            float radius = MathF.Sqrt(rawX * rawX + rawY * rawY);

            // Draw drift dot on canvas
            var canvas = isLeft ? LeftStickCanvas : RightStickCanvas;
            double cx = VisualizerCenter + rawX * VisualizerUsableRadius;
            double cy = VisualizerCenter - rawY * VisualizerUsableRadius;

            var dot = new Ellipse
            {
                Width  = 4, Height = 4,
                Fill   = new SolidColorBrush(Color.FromArgb(160, 0xE8, 0xB8, 0x4B))
            };
            Canvas.SetLeft(dot, cx - 2);
            Canvas.SetTop(dot,  cy - 2);
            canvas.Children.Add(dot);

            if (isLeft)
            {
                _leftDriftDots.Add(dot);
                _leftDriftMaxRadius = MathF.Max(_leftDriftMaxRadius, radius);
            }
            else
            {
                _rightDriftDots.Add(dot);
                _rightDriftMaxRadius = MathF.Max(_rightDriftMaxRadius, radius);
            }

            _driftTicksRemaining--;

            if (_driftTicksRemaining <= 0)
            {
                FinishDriftAnalysis(isLeft);
            }
            else
            {
                int secs = (int)Math.Ceiling(_driftTicksRemaining / 30.0);
                string msg = $"Sampling… {secs}s remaining";
                if (isLeft) LeftDriftSuggestionText.Text  = msg;
                else        RightDriftSuggestionText.Text = msg;
            }
        }

        private void FinishDriftAnalysis(bool isLeft)
        {
            float maxR    = isLeft ? _leftDriftMaxRadius : _rightDriftMaxRadius;
            float suggested = DeadzoneSuggestion.Calculate(maxR);

            if (isLeft)
            {
                _leftSuggested = suggested;
                int pct = (int)Math.Round(suggested * 100.0);
                LeftDriftSuggestionText.Text = pct == 0
                    ? "No drift detected."
                    : $"Suggested: {pct}%";
                LeftApplyDriftButton.Visibility  = pct > 0 ? Visibility.Visible : Visibility.Collapsed;
                LeftAnalyzeDriftButton.IsEnabled = true;
                _driftActive = DriftStick.None;
            }
            else
            {
                _rightSuggested = suggested;
                int pct = (int)Math.Round(suggested * 100.0);
                RightDriftSuggestionText.Text = pct == 0
                    ? "No drift detected."
                    : $"Suggested: {pct}%";
                RightApplyDriftButton.Visibility  = pct > 0 ? Visibility.Visible : Visibility.Collapsed;
                RightAnalyzeDriftButton.IsEnabled = true;
                _driftActive = DriftStick.None;
            }

            // Clear drift dots after 2 more seconds (6 more UI ticks then remove)
            ScheduleDriftDotClear(isLeft ? _leftDriftDots : _rightDriftDots,
                                  isLeft ? LeftStickCanvas  : RightStickCanvas);
        }

        private void ScheduleDriftDotClear(List<Ellipse> dots, Canvas canvas)
        {
            // Clear dots after 2 s delay using a one-shot timer
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                foreach (var d in dots)
                    canvas.Children.Remove(d);
                dots.Clear();
            };
            timer.Start();
        }

        private void ClearDriftDots()
        {
            foreach (var d in _leftDriftDots)  LeftStickCanvas.Children.Remove(d);
            foreach (var d in _rightDriftDots) RightStickCanvas.Children.Remove(d);
            _leftDriftDots.Clear();
            _rightDriftDots.Clear();
        }

        private void LeftApplyDriftButton_Click(object sender, RoutedEventArgs e)
        {
            int pct = (int)Math.Round(_leftSuggested * 100.0);
            LeftDeadzoneSlider.Value = pct;
            // Also persist immediately
            _pipeline.SaveDeadzoneForCurrentController(
                (float)(LeftDeadzoneSlider.Value  / 100.0),
                (float)(RightDeadzoneSlider.Value / 100.0));
            LeftApplyDriftButton.Visibility = Visibility.Collapsed;
            LeftDriftSuggestionText.Text    = $"Applied {pct}%";
        }

        private void RightApplyDriftButton_Click(object sender, RoutedEventArgs e)
        {
            int pct = (int)Math.Round(_rightSuggested * 100.0);
            RightDeadzoneSlider.Value = pct;
            _pipeline.SaveDeadzoneForCurrentController(
                (float)(LeftDeadzoneSlider.Value  / 100.0),
                (float)(RightDeadzoneSlider.Value / 100.0));
            RightApplyDriftButton.Visibility = Visibility.Collapsed;
            RightDriftSuggestionText.Text    = $"Applied {pct}%";
        }

        // ── Deadzone sliders ─────────────────────────────────────────────────

        private void LeftDeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;
            UpdateLeftDeadzoneDisplay();
            PushDeadzoneToPipeline();
        }

        private void RightDeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;
            UpdateRightDeadzoneDisplay();
            PushDeadzoneToPipeline();
        }

        private void UpdateLeftDeadzoneDisplay()
        {
            if (LeftDeadzoneSlider == null || LeftDeadzonePercentText == null) return;
            int leftPct = (int)Math.Round(LeftDeadzoneSlider.Value);
            LeftDeadzonePercentText.Text = $"{leftPct}%";
            UpdateDeadzoneCircle(LeftDeadzoneCircle, leftPct);
        }

        private void UpdateRightDeadzoneDisplay()
        {
            if (RightDeadzoneSlider == null || RightDeadzonePercentText == null) return;
            int rightPct = (int)Math.Round(RightDeadzoneSlider.Value);
            RightDeadzonePercentText.Text = $"{rightPct}%";
            UpdateDeadzoneCircle(RightDeadzoneCircle, rightPct);
        }

        private void UpdateDeadzoneDisplay()
        {
            UpdateLeftDeadzoneDisplay();
            UpdateRightDeadzoneDisplay();
        }

        private void PushDeadzoneToPipeline()
        {
            if (_pipeline == null) return;
            float leftDz  = (float)(LeftDeadzoneSlider.Value  / 100.0);
            float rightDz = (float)(RightDeadzoneSlider.Value / 100.0);
            _pipeline.UpdateSettings(new DeadzoneSettings(leftDz, rightDz));
            _pipeline.SaveDeadzoneForCurrentController(leftDz, rightDz);
        }

        private static void UpdateDeadzoneCircle(Ellipse circle, int pct)
        {
            if (circle == null) return;
            // Radius must match usable stick radius exactly (spec §21)
            double radius   = VisualizerUsableRadius * (pct / 100.0);
            double diameter = radius * 2;
            circle.Width   = diameter;
            circle.Height  = diameter;
            Canvas.SetLeft(circle, VisualizerCenter - radius);
            Canvas.SetTop(circle,  VisualizerCenter - radius);
        }

        // ── Stick visualizer helpers ─────────────────────────────────────────

        private void UpdateLeftStickPreview(float rawX, float rawY, float outX, float outY)
        {
            UpdateStickDot(LeftRawDot,    rawX, rawY, RawDotSize);
            UpdateStickDot(LeftOutputDot, outX, outY, OutputDotSize);
            LeftRawXText.Text    = FormatAxis(rawX);
            LeftRawYText.Text    = FormatAxis(rawY);
            LeftOutputXText.Text = FormatAxis(outX);
            LeftOutputYText.Text = FormatAxis(outY);
        }

        private void UpdateRightStickPreview(float rawX, float rawY, float outX, float outY)
        {
            UpdateStickDot(RightRawDot,    rawX, rawY, RawDotSize);
            UpdateStickDot(RightOutputDot, outX, outY, OutputDotSize);
            RightRawXText.Text    = FormatAxis(rawX);
            RightRawYText.Text    = FormatAxis(rawY);
            RightOutputXText.Text = FormatAxis(outX);
            RightOutputYText.Text = FormatAxis(outY);
        }

        private static void UpdateStickDot(Ellipse dot, float x, float y, double dotSize)
        {
            if (dot == null) return;
            double cx = VisualizerCenter + x * VisualizerUsableRadius;
            double cy = VisualizerCenter - y * VisualizerUsableRadius;
            Canvas.SetLeft(dot, cx - dotSize / 2.0);
            Canvas.SetTop(dot,  cy - dotSize / 2.0);
        }

        private static string FormatAxis(float v)
        {
            return v >= 0 ? $"+{v:F3}" : v.ToString("F3");
        }
    }
}
