using System;

namespace FreeTrainSimulator.Models.Track
{
    public enum TrackDataBaseType
    {
        Track,
        Road,
    }

    public enum ConnectorType
    {
        InPin,
        OutPin,
    }

    public enum ShapeType
    {
        None,
        Tunnel,
        Road,
    }

    [Flags]
    public enum SpeedpostType
    {
        None = 0,
        Warning = 1 << 0,
        Limit = 1 << 1,
        Resume = 1 << 2,
        Passenger = 1 << 3,
        Freight = 1 << 4,
        Metric = 1 << 5,
        ShowNumber = 1 << 6,
        ShowDot = 1 << 7,
    }
}
