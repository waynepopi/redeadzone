using System;

namespace Deadzone.App.Settings;

public sealed class ControllerDeadzoneSettings
{
    private float _leftStickDeadzone = 0.08f;
    private float _rightStickDeadzone = 0.08f;

    public string DisplayName { get; set; } = string.Empty;

    public float LeftStickDeadzone
    {
        get => _leftStickDeadzone;
        set => _leftStickDeadzone = Math.Clamp(value, 0.0f, 1.00f);
    }

    public float RightStickDeadzone
    {
        get => _rightStickDeadzone;
        set => _rightStickDeadzone = Math.Clamp(value, 0.0f, 1.00f);
    }
}
