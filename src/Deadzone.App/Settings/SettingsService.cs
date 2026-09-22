using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Deadzone.App.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private AppSettings? _pendingSettings;

    public SettingsService()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Deadzone");
        _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");
    }

    public string SettingsFilePath => _settingsFilePath;

    public AppSettings Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings == null)
                {
                    return new AppSettings();
                }

                // Ensure controller dictionary is ordinal case-insensitive and values clamped
                var sanitizedControllers = new System.Collections.Generic.Dictionary<string, ControllerDeadzoneSettings>(StringComparer.OrdinalIgnoreCase);
                if (settings.Controllers != null)
                {
                    foreach (var (key, value) in settings.Controllers)
                    {
                        if (value != null)
                        {
                            value.LeftStickDeadzone = Math.Clamp(value.LeftStickDeadzone, 0.0f, 0.30f);
                            value.RightStickDeadzone = Math.Clamp(value.RightStickDeadzone, 0.0f, 0.30f);
                            sanitizedControllers[key] = value;
                        }
                    }
                }
                settings.Controllers = sanitizedControllers;

                return settings;
            }
            catch
            {
                // Safe fallback on corrupted or unreadable settings
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        if (settings == null) return;

        lock (_lock)
        {
            // Cancel any pending debounced timer
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _pendingSettings = null;

            SaveInternal(settings);
        }
    }

    public void RequestSave(AppSettings settings, int delayMs = 500)
    {
        if (settings == null) return;

        lock (_lock)
        {
            _pendingSettings = settings;
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceElapsed, null, delayMs, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        lock (_lock)
        {
            if (_pendingSettings != null)
            {
                SaveInternal(_pendingSettings);
                _pendingSettings = null;
            }
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    private void SaveInternal(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }

            string tempFile = Path.Combine(_settingsDirectory, $"settings.{Guid.NewGuid():N}.tmp");
            string json = JsonSerializer.Serialize(settings, JsonOptions);

            File.WriteAllText(tempFile, json);

            // Atomic file replacement
            File.Move(tempFile, _settingsFilePath, overwrite: true);
        }
        catch
        {
            // Suppress non-fatal write errors
        }
    }
}
