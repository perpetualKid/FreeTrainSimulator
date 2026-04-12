using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SidingTrackItem : TrackItemBase
    {
        public uint SidingFlags { get; init; }
        public int LinkedSidingItem { get; init; }
        public string SidingName { get; init; }

        [MemoryPackConstructor]
        public SidingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PlatformTrackItem : TrackItemBase
    {
        public uint PlatformFlags { get; init; }
        public int LinkedPlatformItem { get; init; }
        public string StationName { get; init; }
        public string PlatformName { get; init; }
        public int MinWaitingTime { get; init; }
        public int PassengersWaiting { get; init; }

        [MemoryPackConstructor]
        public PlatformTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SpeedpostTrackItem : TrackItemBase
    {
        public float SpeedValue { get; init; }
        public int AlternativeSpeedValue { get; init; }
        public SpeedpostType SpeedpostType { get; init; }
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

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record MilepostTrackItem : TrackItemBase
    {
        public float DistanceValue { get; init; }

        [MemoryPackConstructor]
        public MilepostTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record HazardTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public HazardTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PickupTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public PickupTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record LevelCrossingTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public LevelCrossingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RoadLevelCrossingTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public RoadLevelCrossingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SoundRegionTrackItem : TrackItemBase
    {
        public int SoundRegionData1 { get; init; }
        public int SoundRegionData2 { get; init; }
        public float SoundRegionData3 { get; init; }

        [MemoryPackConstructor]
        public SoundRegionTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalTrackItem : TrackItemBase
    {
        public uint SignalFlags { get; init; } // Set to  00000001 if junction link set
        public TrackDirection Direction { get; init; }
        public float SignalData { get; init; }
        public string SignalType { get; init; }
        public SignalDirection SignalDirection { get; init; }
        public bool NormalSignal { get; init; }

        [MemoryPackConstructor]
        public SignalTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SignalDirection
    {
        public int NodeIndex { get; init; }
        /// <summary>Used with junction signals, appears to be either 1 or 0</summary>
        public int JunctionPath { get; init; }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record CrossoverTrackItem : TrackItemBase
    {
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public CrossoverTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record CarSpawnerTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public CarSpawnerTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EmptyTrackItem : TrackItemBase
    {
        [MemoryPackConstructor]
        public EmptyTrackItem() : base(WorldLocation.None)
        {
        }
    }
}
