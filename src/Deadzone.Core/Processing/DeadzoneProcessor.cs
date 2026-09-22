using System;
using Deadzone.Core.Models;

namespace Deadzone.Core.Processing;

public static class DeadzoneProcessor
{
    public static StickVector ApplyRadial(
        StickVector input,
        float deadzone)
    {
        deadzone = Math.Clamp(
            deadzone,
            0f,
            1.00f);

        if (deadzone <= 0f)
            return input;

        float x = input.X;
        float y = input.Y;

        float magnitude =
            MathF.Sqrt(
                (x * x) +
                (y * y));

        if (deadzone >= 1f ||
            magnitude <= deadzone ||
            magnitude <= float.Epsilon)
        {
            return new StickVector(
                0f,
                0f);
        }

        float scaledMagnitude =
            (magnitude - deadzone) /
            (1f - deadzone);

        float scale =
            scaledMagnitude /
            magnitude;

        float outputX =
            Math.Clamp(
                x * scale,
                -1f,
                1f);

        float outputY =
            Math.Clamp(
                y * scale,
                -1f,
                1f);

        return new StickVector(
            outputX,
            outputY);
    }

    public static ControllerState Apply(
        ControllerState state,
        DeadzoneSettings settings)
    {
        StickVector left =
            ApplyRadial(
                state.LeftStick,
                settings.LeftStickDeadzone);

        StickVector right =
            ApplyRadial(
                state.RightStick,
                settings.RightStickDeadzone);

        return state with
        {
            LeftStick = left,
            RightStick = right
        };
    }
}
