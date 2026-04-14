using System;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Distinguishes between the rail and road track databases in a route.
    /// </summary>
    public enum TrackDataBaseType
    {
        /// <summary>Rail track database (<c>.tdb</c>).</summary>
        Rail,
        /// <summary>Road track database (<c>.rdb</c>).</summary>
        Road,
    }

    /// <summary>
    /// Direction of a <see cref="TrackNodeConnector"/> relative to its owning node.
    /// </summary>
    public enum ConnectorType
    {
        /// <summary>Inbound pin (traffic arriving at this node).</summary>
        InPin,
        /// <summary>Outbound pin (traffic departing this node).</summary>
        OutPin,
    }

    /// <summary>
    /// Classification of a <see cref="TrackShape"/>'s visual/environmental type.
    /// </summary>
    public enum ShapeType
    {
        /// <summary>Normal open-air track.</summary>
        None,
        /// <summary>Track inside a tunnel.</summary>
        Tunnel,
        /// <summary>Road surface.</summary>
        Road,
    }

    /// <summary>
    /// Flags describing the behavior and display mode of a <see cref="SpeedpostTrackItem"/>.
    /// </summary>
    [Flags]
    public enum SpeedpostType
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>Advance warning of upcoming speed restriction.</summary>
        Warning = 1 << 0,
        /// <summary>Actual speed limit enforcement point.</summary>
        Limit = 1 << 1,
        /// <summary>End-of-restriction marker (resume normal speed).</summary>
        Resume = 1 << 2,
        /// <summary>Applies to passenger trains.</summary>
        Passenger = 1 << 3,
        /// <summary>Applies to freight trains.</summary>
        Freight = 1 << 4,
        /// <summary>Speed value is in km/h (metric) rather than mph.</summary>
        Metric = 1 << 5,
        /// <summary>Display the <see cref="SpeedpostTrackItem.AlternativeSpeedValue"/> number instead of speed.</summary>
        ShowNumber = 1 << 6,
        /// <summary>Display a decimal dot in the speed value.</summary>
        ShowDot = 1 << 7,
    }
}
