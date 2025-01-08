using System;

namespace FreeTrainSimulator.Models.Imported.Track
{
    [Flags]
    public enum PathNodeInvalidReasons
    {
        None = 0,
        NoJunctionNode = 0x1,
        NotOnTrack = 0x2,
        NoConnectionPossible = 0x4,
        Invalid = 0x8,
    }

    public enum TrackElementType
    {
        RailTrack,
        RoadTrack,
    }
}
