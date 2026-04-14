using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A siding marker placed along a track node, representing one end of a named siding.
    /// Two siding items with matching <see cref="LinkedSidingItem"/> define the extent of a siding.
    /// </summary>
    /// <remarks>Derived from <c>SidingItem</c> in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SidingTrackItem : TrackItemBase
    {
        /// <summary>MSTS flags specific to siding items.</summary>
        public uint SidingFlags { get; init; }

        /// <summary>Track item index of the other end of this siding.</summary>
        public int LinkedSidingItem { get; init; }

        /// <summary>Display name of this siding.</summary>
        public string SidingName { get; init; }

        [MemoryPackConstructor]
        public SidingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A platform marker placed along a track node, representing one end of a station platform.
    /// Two platform items with matching <see cref="LinkedPlatformItem"/> define the platform extent.
    /// </summary>
    /// <remarks>Derived from <c>PlatformItem</c> in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PlatformTrackItem : TrackItemBase
    {
        /// <summary>MSTS flags specific to platform items (e.g. disabled).</summary>
        public uint PlatformFlags { get; init; }

        /// <summary>Track item index of the other end of this platform.</summary>
        public int LinkedPlatformItem { get; init; }

        /// <summary>Name of the station this platform belongs to.</summary>
        public string StationName { get; init; }

        /// <summary>Display name of this platform within the station.</summary>
        public string PlatformName { get; init; }

        /// <summary>Minimum waiting time at this platform in seconds.</summary>
        public int MinWaitingTime { get; init; }

        /// <summary>Number of passengers waiting at this platform.</summary>
        public int PassengersWaiting { get; init; }

        [MemoryPackConstructor]
        public PlatformTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A speed restriction sign placed along a track node.
    /// Defines a speed limit, warning, or resume marker for passenger and/or freight traffic.
    /// </summary>
    /// <remarks>Derived from <c>SpeedPostItem</c> in the MSTS <c>.tdb</c> file (non-milepost variant).</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SpeedpostTrackItem : TrackItemBase
    {
        /// <summary>Speed value in meters per second.</summary>
        public float SpeedValue { get; init; }

        /// <summary>Alternative numeric value displayed on German-style speed boards.</summary>
        public int AlternativeSpeedValue { get; init; }

        /// <summary>Flags describing the speedpost behavior (limit/warning/resume, passenger/freight, metric, display options).</summary>
        public SpeedpostType SpeedpostType { get; init; }

        /// <summary>Orientation angle of the speedpost sign relative to the track, in radians.</summary>
        public float Angle { get; init; }

        [MemoryPackConstructor]
        public SpeedpostTrackItem(in WorldLocation location) : base(location)
        {
        }

        public override string ToString()
        {
            string result = string.Empty;
            //determine what to show: speed or number used in German routes
            if (SpeedpostType.HasFlag(SpeedpostType.ShowNumber))
            {
                result += AlternativeSpeedValue;
            }
            else
            {
                //determine if the speed is for passenger or freight
                if (SpeedpostType.HasFlag(SpeedpostType.Freight) && !SpeedpostType.HasFlag(SpeedpostType.Passenger))
                    result += "F";
                else if (!SpeedpostType.HasFlag(SpeedpostType.Freight) && SpeedpostType.HasFlag(SpeedpostType.Passenger))
                    result += "P";
                result += SpeedValue;
            }
            if (!SpeedpostType.HasFlag(SpeedpostType.ShowDot))
                result = result.Replace(".", "", StringComparison.OrdinalIgnoreCase);
            return result;
        }
    }

    /// <summary>
    /// A milepost (distance marker) placed along a track node.
    /// </summary>
    /// <remarks>Derived from <c>SpeedPostItem</c> (milepost variant) in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record MilepostTrackItem : TrackItemBase
    {
        /// <summary>Distance value displayed on the milepost, in the route's distance unit.</summary>
        public float DistanceValue { get; init; }

        [MemoryPackConstructor]
        public MilepostTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A hazard location marker (e.g. animals crossing) placed along a track node.
    /// </summary>
    /// <remarks>Derived from <c>HazardItem</c> in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record HazardTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public HazardTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A pickup location (fuel, water, or container) placed along a track node.
    /// </summary>
    /// <remarks>Derived from <c>PickupItem</c> in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PickupTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public PickupTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A rail-road level crossing marker placed along a track node.
    /// </summary>
    /// <remarks>Derived from <c>LevelCrItem</c> in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record LevelCrossingTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public LevelCrossingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A road-side level crossing marker placed along a road track node.
    /// </summary>
    /// <remarks>Derived from <c>RoadLevelCrItem</c> in the MSTS <c>.rdb</c> road database file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RoadLevelCrossingTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public RoadLevelCrossingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A sound region marker placed along a track node, defining an area where a specific ambient sound applies.
    /// </summary>
    /// <remarks>Derived from <c>SoundRegionItem</c> in the MSTS <c>.tdb</c> file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SoundRegionTrackItem : TrackItemBase
    {
        /// <summary>Sound region type identifier.</summary>
        public int SoundRegionData1 { get; init; }

        /// <summary>Sound region parameter (interpretation depends on the sound system).</summary>
        public int SoundRegionData2 { get; init; }

        /// <summary>Sound region rotation angle or extent value.</summary>
        public float SoundRegionData3 { get; init; }

        [MemoryPackConstructor]
        public SoundRegionTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A signal head marker placed along a track node, referencing a signal shape and type.
    /// </summary>
    /// <remarks>Derived from <c>SignalItem</c> in the MSTS <c>.tdb</c> file.
    /// Multiple signal track items may share the same physical signal when they represent
    /// different heads on a multi-head signal mast.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalTrackItem : TrackItemBase
    {
        /// <summary>MSTS signal flags. Bit 0 is set if a junction link is associated with this signal.</summary>
        public uint SignalFlags { get; init; }

        /// <summary>Direction the signal faces along the track node.</summary>
        public TrackDirection Direction { get; init; }

        /// <summary>MSTS signal data value (e.g. sub-object reference). Interpretation varies by signal type.</summary>
        public float SignalData { get; init; }

        /// <summary>Name of the <see cref="Signalling.SignalType"/> that defines this signal head's behavior.</summary>
        public string SignalType { get; init; }

        /// <summary>Junction direction linkage for junction-linked signals.</summary>
        public SignalDirection SignalDirection { get; init; }

        /// <summary>Whether this item represents a normal (main-line) signal as opposed to a shunting or other type.</summary>
        public bool NormalSignal { get; init; }

        [MemoryPackConstructor]
        public SignalTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// Junction linkage information for a signal, specifying which track node and diverging path
    /// the signal controls.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalDirection
    {
        /// <summary>Index of the junction track node this signal is linked to.</summary>
        public int NodeIndex { get; init; }
        /// <summary>Which diverging path (0 or 1) at the junction this signal controls.</summary>
        public int JunctionPath { get; init; }
    }

    /// <summary>
    /// A crossover item connecting two parallel tracks, allowing trains to switch between them.
    /// </summary>
    /// <remarks>Derived from <c>CrossoverItem</c> in the MSTS <c>.tdb</c> file.
    /// Two crossover items with matching shape indices form a complete crossover.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record CrossoverTrackItem : TrackItemBase
    {
        /// <summary>Index of the 3D shape used for this crossover, referencing <see cref="TrackSectionModel.TrackShapes"/>.</summary>
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public CrossoverTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// A car spawner marker placed along a road track node, defining a location where road traffic is spawned.
    /// </summary>
    /// <remarks>Derived from <c>CarSpawnerItem</c> in the MSTS road database file.</remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record CarSpawnerTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public CarSpawnerTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// Placeholder for track item indices that exist in the database but have no meaningful data.
    /// Used to maintain correct indexing in the <see cref="TrackDatabase.TrackItems"/> array.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EmptyTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public EmptyTrackItem() : base(WorldLocation.None)
        {
        }
    }
}
