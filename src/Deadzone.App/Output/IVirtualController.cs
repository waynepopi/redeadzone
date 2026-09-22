using System;
using Deadzone.Core.Models;

namespace Deadzone.App.Output;

public interface IVirtualController : IDisposable
{
    bool IsInitialized { get; }

    bool IsStarted { get; }

    string ProfileName { get; }

    void Initialize();

    void Start();

    void Submit(ControllerState state);

    void Stop();
}
