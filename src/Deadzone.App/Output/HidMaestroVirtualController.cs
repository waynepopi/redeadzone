using System;
using System.Collections.Generic;
using Deadzone.Core.Models;
using HIDMaestro;

namespace Deadzone.App.Output;

public sealed class HidMaestroVirtualController : IVirtualController
{
    private const string TargetProfileId = "xbox-series-xs-bt";
    private const string DurableSlotKey = "deadzone-slot0";

    private readonly object _sync = new();

    private HMContext? _context;
    private HMProfile? _profile;
    private HMController? _controller;
    private Dictionary<HMAxis, float>? _axes;
    private HMGamepadState _gamepadState;

    private HMAxis _leftStickX;
    private HMAxis _leftStickY;
    private HMAxis _rightStickX;
    private HMAxis _rightStickY;
    private HMAxis _leftTrigger;
    private HMAxis _rightTrigger;

    private bool _isInitialized;

    public bool IsInitialized
    {
        get
        {
            lock (_sync)
            {
                return _isInitialized;
            }
        }
    }

    public bool IsStarted
    {
        get
        {
            lock (_sync)
            {
                return _controller != null;
            }
        }
    }

    public string ProfileName => TargetProfileId;

    public void Initialize()
    {
        lock (_sync)
        {
            if (_isInitialized)
                return;

            try
            {
                _context = new HMContext();

                int profilesLoaded = _context.LoadDefaultProfiles();
                if (profilesLoaded <= 0)
                {
                    throw new InvalidOperationException("HIDMaestro failed to load default profiles.");
                }

                _context.InstallDriver();

                _profile = _context.GetProfile(TargetProfileId);
                if (_profile == null)
                {
                    throw new InvalidOperationException($"HIDMaestro profile '{TargetProfileId}' was not found.");
                }

                if (!_profile.IsDeployable)
                {
                    throw new InvalidOperationException($"HIDMaestro profile '{TargetProfileId}' is not deployable.");
                }

                if (_profile.Sticks.Count < 2 || _profile.Triggers.Count < 2)
                {
                    throw new InvalidOperationException($"Profile '{TargetProfileId}' does not expose required sticks/triggers.");
                }

                _leftStickX = _profile.Sticks[0].XAxis;
                _leftStickY = _profile.Sticks[0].YAxis;
                _rightStickX = _profile.Sticks[1].XAxis;
                _rightStickY = _profile.Sticks[1].YAxis;

                _leftTrigger = _profile.Triggers[0].Axis;
                _rightTrigger = _profile.Triggers[1].Axis;

                _axes = HMGamepadStateHelpers.StandardAxes(_profile, 0.5f, 0.5f, 0.5f, 0.5f, 0.0f, 0.0f);
                _gamepadState = new HMGamepadState
                {
                    Axes = _axes,
                    Buttons = HMButton.None,
                    Hat = HMHat.None
                };

                _isInitialized = true;
            }
            catch
            {
                _context?.Dispose();
                _context = null;
                _profile = null;
                _controller = null;
                _axes = null;
                _isInitialized = false;
                throw;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Virtual controller is not initialized.");
            }

            if (_controller != null)
            {
                return;
            }

            _controller = _context!.CreateController(_profile!, DurableSlotKey);

            if (_axes != null)
            {
                _axes[_leftStickX] = 0.5f;
                _axes[_leftStickY] = 0.5f;
                _axes[_rightStickX] = 0.5f;
                _axes[_rightStickY] = 0.5f;
                _axes[_leftTrigger] = 0.0f;
                _axes[_rightTrigger] = 0.0f;
            }

            _gamepadState.Buttons = HMButton.None;
            _gamepadState.Hat = HMHat.None;

            _controller.SubmitState(in _gamepadState);
        }
    }

    public void Submit(ControllerState state)
    {
        lock (_sync)
        {
            if (_controller == null || _axes == null)
            {
                return;
            }

            _axes[_leftStickX] = SignedToUnit(state.LeftStick.X);
            _axes[_leftStickY] = SignedToUnit(-state.LeftStick.Y);
            _axes[_rightStickX] = SignedToUnit(state.RightStick.X);
            _axes[_rightStickY] = SignedToUnit(-state.RightStick.Y);

            _axes[_leftTrigger] = Math.Clamp(state.LeftTrigger, 0.0f, 1.0f);
            _axes[_rightTrigger] = Math.Clamp(state.RightTrigger, 0.0f, 1.0f);

            _gamepadState.Buttons = ConvertButtons(state.Buttons);
            _gamepadState.Hat = ConvertDPad(state.DPad);

            _controller.SubmitState(in _gamepadState);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_controller == null)
            {
                return;
            }

            try
            {
                if (_axes != null)
                {
                    _axes[_leftStickX] = 0.5f;
                    _axes[_leftStickY] = 0.5f;
                    _axes[_rightStickX] = 0.5f;
                    _axes[_rightStickY] = 0.5f;
                    _axes[_leftTrigger] = 0.0f;
                    _axes[_rightTrigger] = 0.0f;
                }

                _gamepadState.Buttons = HMButton.None;
                _gamepadState.Hat = HMHat.None;

                _controller.SubmitState(in _gamepadState);
            }
            catch
            {
                // Best effort neutral submit before disposal
            }

            _controller.Dispose();
            _controller = null;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            Stop();

            _context?.Dispose();
            _context = null;
            _profile = null;
            _axes = null;
            _isInitialized = false;
        }
    }

    private static float SignedToUnit(float value)
    {
        value = Math.Clamp(value, -1.0f, 1.0f);
        return (value + 1.0f) * 0.5f;
    }

    private static HMButton ConvertButtons(ControllerButtons buttons)
    {
        HMButton result = HMButton.None;

        if ((buttons & ControllerButtons.A) != 0) result |= HMButton.A;
        if ((buttons & ControllerButtons.B) != 0) result |= HMButton.B;
        if ((buttons & ControllerButtons.X) != 0) result |= HMButton.X;
        if ((buttons & ControllerButtons.Y) != 0) result |= HMButton.Y;

        if ((buttons & ControllerButtons.LeftBumper) != 0) result |= HMButton.LeftBumper;
        if ((buttons & ControllerButtons.RightBumper) != 0) result |= HMButton.RightBumper;

        if ((buttons & ControllerButtons.View) != 0) result |= HMButton.Back;
        if ((buttons & ControllerButtons.Menu) != 0) result |= HMButton.Start;

        if ((buttons & ControllerButtons.LeftStick) != 0) result |= HMButton.LeftStick;
        if ((buttons & ControllerButtons.RightStick) != 0) result |= HMButton.RightStick;

        if ((buttons & ControllerButtons.Guide) != 0) result |= HMButton.Guide;
        if ((buttons & ControllerButtons.Share) != 0) result |= HMButton.Share;

        return result;
    }

    private static HMHat ConvertDPad(DPadDirection dpad) => dpad switch
    {
        DPadDirection.None => HMHat.None,
        DPadDirection.North => HMHat.North,
        DPadDirection.NorthEast => HMHat.NorthEast,
        DPadDirection.East => HMHat.East,
        DPadDirection.SouthEast => HMHat.SouthEast,
        DPadDirection.South => HMHat.South,
        DPadDirection.SouthWest => HMHat.SouthWest,
        DPadDirection.West => HMHat.West,
        DPadDirection.NorthWest => HMHat.NorthWest,
        _ => HMHat.None
    };
}
