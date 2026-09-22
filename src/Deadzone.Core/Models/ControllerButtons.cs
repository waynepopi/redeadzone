using System;

namespace Deadzone.Core.Models;

[Flags]
public enum ControllerButtons : ushort
{
    None         = 0,

    A            = 1 << 0,
    B            = 1 << 1,
    X            = 1 << 2,
    Y            = 1 << 3,

    LeftBumper   = 1 << 4,
    RightBumper  = 1 << 5,

    View         = 1 << 6,
    Menu         = 1 << 7,

    LeftStick    = 1 << 8,
    RightStick   = 1 << 9,

    Guide        = 1 << 10,
    Share        = 1 << 11
}
