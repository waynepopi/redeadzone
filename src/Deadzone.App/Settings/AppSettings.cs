using System;
using System.Collections.Generic;

namespace Deadzone.App.Settings;

public sealed class AppSettings
{
    public string? PreferredControllerKey { get; set; }
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimizedToTray { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public Dictionary<string, ControllerDeadzoneSettings> Controllers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
