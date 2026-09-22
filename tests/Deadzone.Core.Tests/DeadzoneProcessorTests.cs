using Deadzone.Core.Models;
using Deadzone.Core.Processing;
using Xunit;

namespace Deadzone.Core.Tests;

public class DeadzoneProcessorTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void Test_ZeroDeadzone()
    {
        // 16. TEST — ZERO DEADZONE
        var input = new StickVector(0.25f, -0.50f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.00f);

        Assert.Equal(0.25f, result.X, Tolerance);
        Assert.Equal(-0.50f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_InsideDeadzone()
    {
        // 17. TEST — INSIDE DEADZONE
        var input = new StickVector(0.05f, 0.02f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.10f);

        Assert.Equal(0f, result.X, Tolerance);
        Assert.Equal(0f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_ExactlyOnDeadzone()
    {
        // 18. TEST — EXACTLY ON DEADZONE
        var input = new StickVector(0.10f, 0.00f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.10f);

        Assert.Equal(0f, result.X, Tolerance);
        Assert.Equal(0f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_OutsideDeadzone()
    {
        // 19. TEST — OUTSIDE DEADZONE
        var input = new StickVector(0.50f, 0f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.10f);

        float expectedX = (0.50f - 0.10f) / (1.0f - 0.10f);
        Assert.Equal(expectedX, result.X, Tolerance);
        Assert.Equal(0f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_FullPositiveAxis()
    {
        // 20. TEST — FULL POSITIVE AXIS
        var input = new StickVector(1f, 0f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.10f);

        Assert.Equal(1f, result.X, Tolerance);
        Assert.Equal(0f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_FullNegativeAxis()
    {
        // 21. TEST — FULL NEGATIVE AXIS
        var input = new StickVector(-1f, 0f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.10f);

        Assert.Equal(-1f, result.X, Tolerance);
        Assert.Equal(0f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_DiagonalDirection()
    {
        // 22. TEST — DIAGONAL DIRECTION
        var input = new StickVector(0.30f, 0.40f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.10f);

        Assert.Equal(0.2666667f, result.X, Tolerance);
        Assert.Equal(0.3555556f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_IndependentLeftRightDeadzones()
    {
        // 23. TEST — INDEPENDENT LEFT/RIGHT DEADZONES
        var state = new ControllerState(
            LeftStick: new StickVector(0.15f, 0f),
            RightStick: new StickVector(0.15f, 0f),
            LeftTrigger: 0f,
            RightTrigger: 0f,
            Buttons: ControllerButtons.None,
            DPad: DPadDirection.None);

        var settings = new DeadzoneSettings(0.10f, 0.20f);
        var processed = DeadzoneProcessor.Apply(state, settings);

        Assert.True(processed.LeftStick.X > 0f);
        Assert.Equal(0f, processed.RightStick.X, Tolerance);
        Assert.Equal(0f, processed.RightStick.Y, Tolerance);
    }

    [Fact]
    public void Test_PassThroughData()
    {
        // 24. TEST — PASS-THROUGH DATA
        var state = new ControllerState(
            LeftStick: new StickVector(0.5f, 0.5f),
            RightStick: new StickVector(0.5f, 0.5f),
            LeftTrigger: 0.33f,
            RightTrigger: 0.77f,
            Buttons: ControllerButtons.A | ControllerButtons.RightBumper | ControllerButtons.Menu,
            DPad: DPadDirection.NorthEast);

        var settings = new DeadzoneSettings(0.10f, 0.10f);
        var processed = DeadzoneProcessor.Apply(state, settings);

        Assert.Equal(0.33f, processed.LeftTrigger, Tolerance);
        Assert.Equal(0.77f, processed.RightTrigger, Tolerance);
        Assert.Equal(ControllerButtons.A | ControllerButtons.RightBumper | ControllerButtons.Menu, processed.Buttons);
        Assert.Equal(DPadDirection.NorthEast, processed.DPad);
    }

    [Fact]
    public void Test_MaximumDeadzoneClamp()
    {
        // 25. TEST — MAXIMUM DEADZONE CLAMP
        var input = new StickVector(0.20f, 0f);
        var result = DeadzoneProcessor.ApplyRadial(input, 0.50f);

        Assert.Equal(0f, result.X, Tolerance);
        Assert.Equal(0f, result.Y, Tolerance);
    }

    [Fact]
    public void Test_NegativeDeadzoneClamp()
    {
        // 26. TEST — NEGATIVE DEADZONE CLAMP
        var input = new StickVector(0.20f, -0.10f);
        var result = DeadzoneProcessor.ApplyRadial(input, -1.00f);

        Assert.Equal(0.20f, result.X, Tolerance);
        Assert.Equal(-0.10f, result.Y, Tolerance);
    }
}
