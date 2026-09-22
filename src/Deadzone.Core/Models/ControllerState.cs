namespace Deadzone.Core.Models;

public readonly record struct ControllerState(
    StickVector LeftStick,
    StickVector RightStick,
    float LeftTrigger,
    float RightTrigger,
    ControllerButtons Buttons,
    DPadDirection DPad);
