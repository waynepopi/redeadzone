namespace Deadzone.Core.Models;

public sealed record DeadzoneSettings(
    float LeftStickDeadzone,
    float RightStickDeadzone)
{
    public static DeadzoneSettings Default { get; }
        = new(0.08f, 0.08f);
}
