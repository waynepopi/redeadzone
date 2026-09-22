using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deadzone.App.Input;
using Deadzone.App.Output;
using Deadzone.App.Settings;
using Deadzone.Core.Models;
using Deadzone.Core.Processing;

namespace Deadzone.App.Services;

public sealed class ControllerPipeline : IDisposable
{
    private readonly IControllerInput _input;
    private readonly IVirtualController _output;
    private readonly SettingsService _settingsService;
    private readonly SemaphoreSlim _outputLifecycleGate = new(1, 1);
    private readonly object _lock = new();

    private AppSettings _appSettings;
    private DeadzoneSettings _deadzoneSettings = DeadzoneSettings.Default;
    private FrameSnapshot? _latestFrame;

    private volatile bool _isInitialized;
    private volatile bool _isVirtualReady;
    private volatile bool _isOutputEnabled;
    private volatile bool _isControllerConnected;
    private volatile string _controllerName = string.Empty;
    private volatile string? _lastError;

    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private bool _disposed;

    public ControllerPipeline(IControllerInput input, IVirtualController output, SettingsService settingsService, AppSettings appSettings)
    {
        _input           = input           ?? throw new ArgumentNullException(nameof(input));
        _output          = output          ?? throw new ArgumentNullException(nameof(output));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _appSettings     = appSettings     ?? throw new ArgumentNullException(nameof(appSettings));
    }

    // ── Observable state ─────────────────────────────────────────────────────

    public FrameSnapshot? LatestFrame => Volatile.Read(ref _latestFrame);
    public bool IsControllerConnected => _isControllerConnected;
    public string ControllerName     => _controllerName;
    public string? LastError         => _lastError;
    public bool IsOutputEnabled      => _isOutputEnabled;
    public bool IsVirtualReady       => _isVirtualReady;

    public IReadOnlyList<ControllerDescriptor> AvailableControllers => _input.AvailableControllers;
    public ControllerDescriptor? CurrentDescriptor                  => _input.CurrentDescriptor;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Initialize()
    {
        // §40: output first, then input
        try
        {
            _output.Initialize();
            _isVirtualReady = true;
        }
        catch (Exception ex)
        {
            _lastError = $"Virtual controller initialization failed: {ex.Message}";
            throw;
        }

        _input.Initialize(); // also runs immediate enumeration
        _isInitialized = true;
    }

    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_disposed || _monitoringTask != null)
                return;

            _cts = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
        }
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    public void UpdateSettings(DeadzoneSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        Volatile.Write(ref _deadzoneSettings, settings);
    }

    public void UpdateAppSettings(AppSettings appSettings)
    {
        if (appSettings == null) throw new ArgumentNullException(nameof(appSettings));
        Volatile.Write(ref _appSettings, appSettings);
    }

    public void SaveDeadzoneForCurrentController(float leftDz, float rightDz)
    {
        var descriptor = _input.CurrentDescriptor;
        if (descriptor == null) return;

        var settings = Volatile.Read(ref _appSettings);
        if (!settings.Controllers.TryGetValue(descriptor.Key, out var ctrlSettings))
        {
            ctrlSettings = new ControllerDeadzoneSettings { DisplayName = descriptor.DisplayName };
            settings.Controllers[descriptor.Key] = ctrlSettings;
        }

        ctrlSettings.LeftStickDeadzone  = leftDz;
        ctrlSettings.RightStickDeadzone = rightDz;
        _settingsService.RequestSave(settings);
    }

    // ── Controller switching ─────────────────────────────────────────────────

    /// <summary>
    /// Enumerates available controllers. Call from UI thread to refresh the dropdown.
    /// </summary>
    public IReadOnlyList<ControllerDescriptor> RefreshAvailableControllers()
    {
        return _input.EnumerateControllers();
    }

    /// <summary>
    /// Switch to the controller with the given key. If output is active it is
    /// stopped first; the pipeline remains disabled after switching.
    /// </summary>
    public async Task<(bool Success, string? Error, DeadzoneSettings LoadedDeadzone)> SwitchControllerAsync(string targetKey)
    {
        await _outputLifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Disable output first (§spec: never silently keep virtual while switching)
            if (_isOutputEnabled)
            {
                _isOutputEnabled = false;
                try { _output.Stop(); } catch { /* swallow */ }
            }

            _input.Close();
            _isControllerConnected = false;
            _controllerName = string.Empty;
            Volatile.Write(ref _latestFrame, null);

            bool opened = _input.TryOpen(targetKey, out string? errMsg);
            if (!opened)
            {
                return (false, errMsg ?? "Could not open controller.", DeadzoneSettings.Default);
            }

            _isControllerConnected = true;
            _controllerName = _input.ControllerName;

            // Update preferred key
            var appSettings = Volatile.Read(ref _appSettings);
            appSettings.PreferredControllerKey = _input.CurrentDescriptor?.Key ?? targetKey;
            _settingsService.RequestSave(appSettings);

            // Load per-controller deadzone
            DeadzoneSettings loadedDz = LoadDeadzoneForKey(appSettings, appSettings.PreferredControllerKey);
            Volatile.Write(ref _deadzoneSettings, loadedDz);

            return (true, null, loadedDz);
        }
        finally
        {
            _outputLifecycleGate.Release();
        }
    }

    // ── Virtual output ───────────────────────────────────────────────────────

    public async Task<bool> EnableOutputAsync()
    {
        await _outputLifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isInitialized || !_isVirtualReady)
            {
                _lastError = "Subsystems not initialized.";
                return false;
            }

            if (!_isControllerConnected || !_input.IsOpen || !_input.IsConnected)
            {
                _lastError = "No physical controller detected.";
                return false;
            }

            if (_isOutputEnabled)
                return true;

            _lastError = null;

            try
            {
                await Task.Run(() => _output.Start()).ConfigureAwait(false);
                _isOutputEnabled = true;
                return true;
            }
            catch (Exception ex)
            {
                _isOutputEnabled = false;
                try { _output.Stop(); } catch { /* swallow */ }
                _lastError = $"Failed to start virtual controller: {ex.Message}";
                return false;
            }
        }
        finally
        {
            _outputLifecycleGate.Release();
        }
    }

    public async Task DisableOutputAsync()
    {
        await _outputLifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _isOutputEnabled = false;
            await Task.Run(() => _output.Stop()).ConfigureAwait(false);
        }
        finally
        {
            _outputLifecycleGate.Release();
        }
    }

    // ── Monitoring ───────────────────────────────────────────────────────────

    public async Task StopMonitoringAsync()
    {
        Task? taskToWait = null;
        lock (_lock)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                taskToWait = _monitoringTask;
            }
        }

        if (taskToWait != null)
        {
            try   { await taskToWait.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex) { _lastError = ex.Message; }
        }

        await _outputLifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _isOutputEnabled = false;
            _output.Stop();
            _input.Close();
            _isControllerConnected = false;
            _controllerName = string.Empty;
            Volatile.Write(ref _latestFrame, null);
        }
        finally
        {
            _outputLifecycleGate.Release();
        }

        lock (_lock)
        {
            _cts?.Dispose();
            _cts = null;
            _monitoringTask = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try   { StopMonitoringAsync().GetAwaiter().GetResult(); }
        catch { /* suppress */ }

        _outputLifecycleGate.Wait();
        try
        {
            _output.Dispose();
            _input.Dispose();
        }
        finally
        {
            _outputLifecycleGate.Release();
            _outputLifecycleGate.Dispose();
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void TeardownOnDisconnect()
    {
        _outputLifecycleGate.Wait();
        try
        {
            _isOutputEnabled = false;
            try { _output.Stop(); } catch (Exception ex) { _lastError = ex.Message; }
            _input.Close();
            Volatile.Write(ref _latestFrame, null);
            _isControllerConnected = false;
            _controllerName = string.Empty;
        }
        finally
        {
            _outputLifecycleGate.Release();
        }
    }

    private static DeadzoneSettings LoadDeadzoneForKey(AppSettings appSettings, string? key)
    {
        if (key != null && appSettings.Controllers.TryGetValue(key, out var ctrl))
        {
            return new DeadzoneSettings(ctrl.LeftStickDeadzone, ctrl.RightStickDeadzone);
        }
        return DeadzoneSettings.Default;
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        // On first run, attempt to open the preferred controller (or first available)
        string? preferredKey = Volatile.Read(ref _appSettings).PreferredControllerKey;

        while (!token.IsCancellationRequested)
        {
            // Phase 1: Disconnected — try to (re)open
            if (!_input.IsOpen || !_input.IsConnected)
            {
                _isControllerConnected = false;
                _controllerName = string.Empty;
                Volatile.Write(ref _latestFrame, null);

                bool opened = false;
                try
                {
                    opened = _input.TryOpen(preferredKey, out string? errorMsg);
                    if (opened)
                    {
                        _isControllerConnected = true;
                        _controllerName = _input.ControllerName;
                        _lastError = null;

                        // Update preferred key if this was a first-run open
                        var appSettings = Volatile.Read(ref _appSettings);
                        string? newKey = _input.CurrentDescriptor?.Key;
                        if (newKey != null && appSettings.PreferredControllerKey != newKey)
                        {
                            appSettings.PreferredControllerKey = newKey;
                            preferredKey = newKey;
                            _settingsService.RequestSave(appSettings);
                        }

                        // Load per-controller deadzone for new session
                        DeadzoneSettings dz = LoadDeadzoneForKey(appSettings, newKey);
                        Volatile.Write(ref _deadzoneSettings, dz);
                    }
                    else
                    {
                        // Do NOT silently substitute a different controller — just wait
                        if (errorMsg != null && errorMsg != "No controller detected." && errorMsg != "Preferred controller not found.")
                        {
                            _lastError = errorMsg;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                }

                if (!opened)
                {
                    try { await Task.Delay(500, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }
            }

            // Phase 2: Connected — ~250 Hz polling loop (4 ms)
            long lastDiscoveryTicks = Environment.TickCount64;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(4));
            while (!token.IsCancellationRequested && _input.IsOpen && _input.IsConnected)
            {
                // Continuous discovery every 750ms while virtual output is DISABLED (spec §6)
                if (!_isOutputEnabled)
                {
                    long now = Environment.TickCount64;
                    if (now - lastDiscoveryTicks >= 750)
                    {
                        lastDiscoveryTicks = now;
                        _input.EnumerateControllers();
                    }
                }

                bool readSuccess = false;
                ControllerState raw = default;

                try   { readSuccess = _input.TryRead(out raw); }
                catch (Exception ex) { _lastError = ex.Message; readSuccess = false; }

                if (!readSuccess)
                {
                    TeardownOnDisconnect();
                    break;
                }

                DeadzoneSettings settings = Volatile.Read(ref _deadzoneSettings);
                ControllerState processed = DeadzoneProcessor.Apply(raw, settings);
                var snapshot = new FrameSnapshot(raw, processed);
                Volatile.Write(ref _latestFrame, snapshot);

                if (_isOutputEnabled)
                {
                    try
                    {
                        _output.Submit(processed);
                    }
                    catch (Exception ex)
                    {
                        _lastError = $"Virtual controller output failed: {ex.Message}";
                        _outputLifecycleGate.Wait();
                        try   { _isOutputEnabled = false; _output.Stop(); }
                        catch { /* swallow */ }
                        finally { _outputLifecycleGate.Release(); }
                    }
                }

                try
                {
                    if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                        break;
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
