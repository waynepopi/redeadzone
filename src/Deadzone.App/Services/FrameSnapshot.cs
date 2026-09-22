using Deadzone.Core.Models;

namespace Deadzone.App.Services;

public sealed record FrameSnapshot(
    ControllerState Raw,
    ControllerState Processed);
