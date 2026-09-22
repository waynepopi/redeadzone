using System;

namespace Deadzone.App.Input;

public sealed record ControllerDescriptor(
    string Key,
    string DisplayName,
    uint InstanceId,
    bool IsReliableIdentity,
    bool IsConnected,
    string ConnectionLabel = "Connected");
