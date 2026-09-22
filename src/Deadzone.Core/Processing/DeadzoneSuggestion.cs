using System;
using System.Collections.Generic;
using Deadzone.Core.Models;

namespace Deadzone.Core.Processing;

public static class DeadzoneSuggestion
{
    /// <summary>
    /// Calculates a suggested radial deadzone from a collection of raw stick samples.
    /// Uses maximum observed radial drift + 1 percentage point safety margin,
    /// rounded upward to the nearest 1% step and clamped to [0.00, 0.30].
    /// </summary>
    public static float Calculate(IEnumerable<StickVector> samples)
    {
        if (samples == null)
            return 0.08f;

        float maxMagnitude = 0f;
        bool hasSamples = false;

        foreach (var sample in samples)
        {
            hasSamples = true;
            float mag = MathF.Sqrt((sample.X * sample.X) + (sample.Y * sample.Y));
            if (mag > maxMagnitude)
            {
                maxMagnitude = mag;
            }
        }

        if (!hasSamples)
            return 0.08f;

        return Calculate(maxMagnitude);
    }

    /// <summary>
    /// Calculates a suggested radial deadzone from a pre-computed maximum drift radius
    /// (range 0..1). Formula: clamp(ceil((maxRadius + 0.01) × 100) / 100, 0.00, 1.00).
    /// </summary>
    public static float Calculate(float maxRadius)
    {
        float suggested = MathF.Ceiling((maxRadius + 0.01f) * 100f) / 100f;
        return Math.Clamp(suggested, 0f, 1.00f);
    }
}
