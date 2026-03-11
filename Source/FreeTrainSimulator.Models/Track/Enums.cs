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
        Warning,
        Limit,
        Resume,
        Passenger,
        Freight,
        Metric,
        ShowNumber,
        ShowDot,
    }
}
