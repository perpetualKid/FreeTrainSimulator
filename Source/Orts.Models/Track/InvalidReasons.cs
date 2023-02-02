using System;

namespace Orts.Models.Track
{
    [Flags]
    public enum InvalidReasons
    {
        None = 0,
        NoJunctionNode = 0x1,
        NotOnTrack = 0x2,
        NoConnectionPossible = 0x4,
        Invalid = 0x8,
    }
}