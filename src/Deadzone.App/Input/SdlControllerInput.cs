using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Deadzone.Core.Models;
using SDL;
using static SDL.SDL3;

namespace Deadzone.App.Input;

public sealed unsafe class SdlControllerInput : IControllerInput
{
    private readonly object _syncLock = new();
    private IntPtr _gamepad = IntPtr.Zero;
    private bool _isInitialized;
    private string _controllerName = string.Empty;
    private ControllerDescriptor? _currentDescriptor;
    private IReadOnlyList<ControllerDescriptor> _availableControllers = Array.Empty<ControllerDescriptor>();

    public string ControllerName
    {
        get
        {
            lock (_syncLock)
            {
                return _controllerName;
            }
        }
    }

    public bool IsInitialized
    {
        get
        {
            lock (_syncLock)
            {
                return _isInitialized;
            }
        }
    }

    public bool IsOpen
    {
        get
        {
            lock (_syncLock)
            {
                return _isInitialized && _gamepad != IntPtr.Zero;
            }
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_syncLock)
            {
                if (_gamepad == IntPtr.Zero)
                    return false;

                return SDL_GamepadConnected((SDL_Gamepad*)_gamepad);
            }
        }
    }

    public ControllerDescriptor? CurrentDescriptor
    {
        get
        {
            lock (_syncLock)
            {
                return _currentDescriptor;
            }
        }
    }

    public IReadOnlyList<ControllerDescriptor> AvailableControllers
    {
        get
        {
            lock (_syncLock)
            {
                return _availableControllers;
            }
        }
    }

    public void Initialize()
    {
        lock (_syncLock)
        {
            if (_isInitialized)
                return;

            SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

            if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                throw new InvalidOperationException($"SDL gamepad initialization failed: {GetSdlError()}");
            }

            _isInitialized = true;
            // Immediate first enumeration on startup so pre-connected controllers appear without replugging
            EnumerateControllersInternal();
        }
    }

    public IReadOnlyList<ControllerDescriptor> EnumerateControllers()
    {
        lock (_syncLock)
        {
            if (!_isInitialized)
                return Array.Empty<ControllerDescriptor>();

            return EnumerateControllersInternal();
        }
    }

    private IReadOnlyList<ControllerDescriptor> EnumerateControllersInternal()
    {
        SDL_PumpEvents();
        SDL_UpdateGamepads();

        int count = 0;
        SDL_JoystickID* gamepads = SDL_GetGamepads(&count);

        if (gamepads == null || count == 0)
        {
            if (gamepads != null)
                SDL_free(gamepads);

            _availableControllers = Array.Empty<ControllerDescriptor>();
            return _availableControllers;
        }

        try
        {
            var rawList = new List<(uint id, string rawName, ushort vendor, ushort product, ushort version, string guidHex)>(count);

            for (int i = 0; i < count; i++)
            {
                SDL_JoystickID id = gamepads[i];
                string? name = SDL_GetGamepadNameForID(id);
                string rawName = string.IsNullOrWhiteSpace(name) ? "Gamepad" : name.Trim();
                ushort vendor = SDL_GetGamepadVendorForID(id);
                ushort product = SDL_GetGamepadProductForID(id);
                ushort version = SDL_GetGamepadProductVersionForID(id);
                SDL_GUID guid = SDL_GetGamepadGUIDForID(id);
                string guidHex = GuidToHex(guid);

                rawList.Add(((uint)id, rawName, vendor, product, version, guidHex));
            }

            // Differentiate duplicate names within this session (e.g. "Name", "Name (2)")
            var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var keyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in rawList)
            {
                nameCounts[item.rawName] = nameCounts.GetValueOrDefault(item.rawName) + 1;
                string baseKey = $"pad:{item.guidHex}_{item.vendor:X4}_{item.product:X4}_{item.version:X4}_{item.rawName}";
                keyCounts[baseKey] = keyCounts.GetValueOrDefault(baseKey) + 1;
            }

            var nameRunningIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var descriptors = new List<ControllerDescriptor>(rawList.Count);

            foreach (var item in rawList)
            {
                string displayName = item.rawName;
                string baseKey = $"pad:{item.guidHex}_{item.vendor:X4}_{item.product:X4}_{item.version:X4}_{item.rawName}";
                string key = baseKey;

                if (nameCounts[item.rawName] > 1)
                {
                    int index = nameRunningIndex.GetValueOrDefault(item.rawName) + 1;
                    nameRunningIndex[item.rawName] = index;
                    if (index > 1)
                    {
                        displayName = $"{item.rawName} ({index})";
                    }
                    key = $"{baseKey}#id{item.id}";
                }

                // If multiple identical controllers exist simultaneously without a distinguishing serial,
                // mark identity as unreliable for cross-session guessing
                bool isReliable = keyCounts[baseKey] <= 1;

                string connectionLabel = "Connected";
                if (_gamepad != IntPtr.Zero && _currentDescriptor != null && _currentDescriptor.InstanceId == item.id)
                {
                    SDL_JoystickConnectionState conn = SDL_GetGamepadConnectionState((SDL_Gamepad*)_gamepad);
                    connectionLabel = conn switch
                    {
                        SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRED => "USB",
                        SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRELESS => "Wireless",
                        _ => "Connected"
                    };
                }

                descriptors.Add(new ControllerDescriptor(
                    Key: key,
                    DisplayName: displayName,
                    InstanceId: item.id,
                    IsReliableIdentity: isReliable,
                    IsConnected: true,
                    ConnectionLabel: connectionLabel));
            }

            _availableControllers = descriptors;
            return _availableControllers;
        }
        finally
        {
            SDL_free(gamepads);
        }
    }

    public bool TryOpen(string? targetKey, out string? errorMessage)
    {
        errorMessage = null;

        lock (_syncLock)
        {
            if (!_isInitialized)
            {
                errorMessage = "SDL is not initialized.";
                return false;
            }

            // Refresh available controllers
            var candidates = EnumerateControllersInternal();
            if (candidates.Count == 0)
            {
                CloseInternal();
                errorMessage = "No controller detected.";
                return false;
            }

            ControllerDescriptor? target = null;
            if (!string.IsNullOrWhiteSpace(targetKey))
            {
                target = candidates.FirstOrDefault(c =>
                    string.Equals(c.Key, targetKey, StringComparison.OrdinalIgnoreCase)
                    || c.Key.StartsWith(targetKey, StringComparison.OrdinalIgnoreCase)
                    || targetKey.StartsWith(c.Key, StringComparison.OrdinalIgnoreCase)
                    || targetKey.EndsWith(c.Key, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                {
                    // Target preferred controller is not in the candidate list
                    errorMessage = "Preferred controller not found.";
                    return false;
                }
            }
            else
            {
                target = candidates[0];
            }

            // If already open to this device and connected, no need to re-open
            if (_gamepad != IntPtr.Zero && _currentDescriptor?.InstanceId == target.InstanceId && SDL_GamepadConnected((SDL_Gamepad*)_gamepad))
            {
                return true;
            }

            CloseInternal();

            SDL_Gamepad* pad = SDL_OpenGamepad((SDL_JoystickID)target.InstanceId);
            if (pad == null)
            {
                errorMessage = $"Failed to open controller: {GetSdlError()}";
                return false;
            }

            _gamepad = (IntPtr)pad;
            _controllerName = target.DisplayName;

            SDL_JoystickConnectionState conn = SDL_GetGamepadConnectionState(pad);
            string connectionLabel = conn switch
            {
                SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRED => "USB",
                SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRELESS => "Wireless",
                _ => "Connected"
            };

            _currentDescriptor = target with
            {
                ConnectionLabel = connectionLabel
            };

            return true;
        }
    }

    public bool TryRead(out ControllerState state)
    {
        state = default;

        lock (_syncLock)
        {
            if (_gamepad == IntPtr.Zero)
                return false;

            SDL_Gamepad* pad = (SDL_Gamepad*)_gamepad;
            if (!SDL_GamepadConnected(pad))
                return false;

            SDL_UpdateGamepads();

            short rawLeftX = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX);
            short rawLeftY = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY);
            short rawRightX = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX);
            short rawRightY = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY);

            short rawLT = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER);
            short rawRT = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER);

            float leftX = NormalizeStick(rawLeftX);
            float leftY = -NormalizeStick(rawLeftY);
            float rightX = NormalizeStick(rawRightX);
            float rightY = -NormalizeStick(rawRightY);

            float leftTrigger = NormalizeTrigger(rawLT);
            float rightTrigger = NormalizeTrigger(rawRT);

            ControllerButtons buttons = ControllerButtons.None;

            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH))
                buttons |= ControllerButtons.A;
            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST))
                buttons |= ControllerButtons.B;
            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST))
                buttons |= ControllerButtons.X;
            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH))
                buttons |= ControllerButtons.Y;

            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER))
                buttons |= ControllerButtons.LeftBumper;
            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER))
                buttons |= ControllerButtons.RightBumper;

            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK))
                buttons |= ControllerButtons.View;
            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START))
                buttons |= ControllerButtons.Menu;

            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK))
                buttons |= ControllerButtons.LeftStick;
            if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK))
                buttons |= ControllerButtons.RightStick;

            if (SDL_GamepadHasButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE))
            {
                if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE))
                    buttons |= ControllerButtons.Guide;
            }

            if (SDL_GamepadHasButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1))
            {
                if (SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1))
                    buttons |= ControllerButtons.Share;
            }

            bool dpadUp = SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP);
            bool dpadDown = SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN);
            bool dpadLeft = SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT);
            bool dpadRight = SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT);

            DPadDirection dpad = ResolveDPad(dpadUp, dpadDown, dpadLeft, dpadRight);

            state = new ControllerState(
                LeftStick: new StickVector(leftX, leftY),
                RightStick: new StickVector(rightX, rightY),
                LeftTrigger: leftTrigger,
                RightTrigger: rightTrigger,
                Buttons: buttons,
                DPad: dpad);

            return true;
        }
    }

    public void Close()
    {
        lock (_syncLock)
        {
            CloseInternal();
        }
    }

    private void CloseInternal()
    {
        if (_gamepad != IntPtr.Zero)
        {
            SDL_CloseGamepad((SDL_Gamepad*)_gamepad);
            _gamepad = IntPtr.Zero;
        }

        _controllerName = string.Empty;
        _currentDescriptor = null;
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            CloseInternal();

            if (_isInitialized)
            {
                SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_GAMEPAD);
                _isInitialized = false;
            }
        }
    }

    private static string GuidToHex(SDL_GUID guid)
    {
        byte[] bytes = new byte[sizeof(SDL_GUID)];
        fixed (byte* p = bytes)
        {
            *(SDL_GUID*)p = guid;
        }
        return Convert.ToHexString(bytes);
    }

    private static float NormalizeStick(short value)
    {
        float normalized =
            value < 0
                ? value / 32768f
                : value / 32767f;

        return Math.Clamp(
            normalized,
            -1f,
            1f);
    }

    private static float NormalizeTrigger(short value)
    {
        return Math.Clamp(
            value / 32767f,
            0f,
            1f);
    }

    private static DPadDirection ResolveDPad(bool up, bool down, bool left, bool right)
    {
        if (up && down)
            return DPadDirection.None;
        if (left && right)
            return DPadDirection.None;

        if (up && right)
            return DPadDirection.NorthEast;
        if (up && left)
            return DPadDirection.NorthWest;
        if (down && right)
            return DPadDirection.SouthEast;
        if (down && left)
            return DPadDirection.SouthWest;

        if (up)
            return DPadDirection.North;
        if (down)
            return DPadDirection.South;
        if (right)
            return DPadDirection.East;
        if (left)
            return DPadDirection.West;

        return DPadDirection.None;
    }

    private static string GetSdlError()
    {
        return SDL_GetError() ?? "Unknown SDL error";
    }
}
