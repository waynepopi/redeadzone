using System;
using System.Collections.Generic;
using Deadzone.Core.Models;

namespace Deadzone.App.Input;

public interface IControllerInput : IDisposable
{
    string ControllerName { get; }

    bool IsInitialized { get; }

    bool IsOpen { get; }

    bool IsConnected { get; }

    ControllerDescriptor? CurrentDescriptor { get; }

    IReadOnlyList<ControllerDescriptor> AvailableControllers { get; }

    void Initialize();

    IReadOnlyList<ControllerDescriptor> EnumerateControllers();

    bool TryOpen(string? targetKey, out string? errorMessage);

    bool TryRead(out ControllerState state);

    void Close();
}

