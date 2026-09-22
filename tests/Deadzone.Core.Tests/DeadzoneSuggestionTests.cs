using Deadzone.Core.Models;
using Deadzone.Core.Processing;
using Xunit;

namespace Deadzone.Core.Tests;

public class DeadzoneSuggestionTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void Test_ZeroDrift_ReturnsOnePercent()
    {
        // Zero drift: max magnitude is 0.0. +1% margin = 0.01 (1%)
        var samples = new[]
        {
            new StickVector(0f, 0f),
            new StickVector(0f, 0f)
        };

        float result = DeadzoneSuggestion.Calculate(samples);
        Assert.Equal(0.01f, result, Tolerance);
    }

    [Fact]
    public void Test_SmallDrift_IncludesOnePercentMarginAndRoundsUp()
    {
        // Sample with magnitude 0.025: 0.025 + 0.010 = 0.035 -> ceiling to 0.04 (4%)
        var samples = new[]
        {
            new StickVector(0.025f, 0f),
            new StickVector(0.010f, 0.010f)
        };

        float result = DeadzoneSuggestion.Calculate(samples);
        Assert.Equal(0.04f, result, Tolerance);
    }

    [Fact]
    public void Test_RoundingUpwardToWholePercentage()
    {
        // Magnitude 0.031: 0.031 + 0.010 = 0.041 -> ceiling to 0.05 (5%)
        var samples = new[]
        {
            new StickVector(0.031f, 0f)
        };

        float result = DeadzoneSuggestion.Calculate(samples);
        Assert.Equal(0.05f, result, Tolerance);
    }

    [Fact]
    public void Test_ResultAboveThirtyPercentAllowed()
    {
        // Large drift: ~0.4924 magnitude -> suggested 0.51 (51%)
        var samples = new[]
        {
            new StickVector(0.45f, 0.20f)
        };

        float result = DeadzoneSuggestion.Calculate(samples);
        Assert.Equal(0.51f, result, Tolerance);
    }

    [Fact]
    public void Test_ResultCappedAtOneHundredPercent()
    {
        // Extreme drift: > 1.0 magnitude -> capped at 1.00 (100%)
        var samples = new[]
        {
            new StickVector(1.10f, 0.20f)
        };

        float result = DeadzoneSuggestion.Calculate(samples);
        Assert.Equal(1.00f, result, Tolerance);
    }

    [Fact]
    public void Test_RadialDiagonalDrift()
    {
        // X = 0.03, Y = 0.04 -> sqrt(0.0009 + 0.0016) = 0.05
        // 0.05 + 0.01 = 0.06 -> 6%
        var samples = new[]
        {
            new StickVector(0.03f, 0.04f)
        };

        float result = DeadzoneSuggestion.Calculate(samples);
        Assert.Equal(0.06f, result, Tolerance);
    }

    [Fact]
    public void Test_LeftAndRightCallersRemainIndependent()
    {
        var leftSamples = new[]
        {
            new StickVector(0.02f, 0.02f) // mag ~0.02828 + 0.01 = 0.03828 -> ceil 4%
        };

        var rightSamples = new[]
        {
            new StickVector(0.08f, 0.06f) // mag = 0.10 + 0.01 = 0.11 -> ceil 11%
        };

        float leftResult = DeadzoneSuggestion.Calculate(leftSamples);
        float rightResult = DeadzoneSuggestion.Calculate(rightSamples);

        Assert.Equal(0.04f, leftResult, Tolerance);
        Assert.Equal(0.11f, rightResult, Tolerance);
        Assert.NotEqual(leftResult, rightResult);
    }
}
