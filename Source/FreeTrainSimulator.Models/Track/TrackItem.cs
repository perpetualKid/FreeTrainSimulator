using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    public abstract partial record TrackItemModel
    {
        private readonly WorldLocation location;
        public ref readonly WorldLocation Location => ref location;

        public ref readonly Tile WorldTile => ref location.Tile;

        public int TrackItemIndex { get; init; }
        public int NodeIndex { get; init; }
        public float SectionDistance { get; init; }
        public uint Flags { get; init; }

        protected TrackItemModel(in WorldLocation location)
        {
            this.location = location;
        }
    }
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SidingTrackItem : TrackItemModel
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
    public sealed partial record PlatformTrackItem : TrackItemModel
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
    public sealed partial record SpeedpostTrackItem : TrackItemModel
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
    public sealed partial record MilepostTrackItem : TrackItemModel
    {
        public float DistanceValue { get; init; }

        [MemoryPackConstructor]
        public MilepostTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record HazardTrackItem : TrackItemModel
    {
        [MemoryPackConstructor]
        public HazardTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PickupTrackItem : TrackItemModel
    {
        [MemoryPackConstructor]
        public PickupTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record LevelCrossingTrackItem : TrackItemModel
    {
        [MemoryPackConstructor]
        public LevelCrossingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RoadLevelCrossingTrackItem : TrackItemModel
    {
        [MemoryPackConstructor]
        public RoadLevelCrossingTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record SoundRegionTrackItem : TrackItemModel
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
    public sealed partial record SignalTrackItem : TrackItemModel
    {        
        public uint SignalFlags { get; init; } // Set to  00000001 if junction link set
        public TrackDirection Direction { get; init; }
        public float SignalData { get; init; }
        public string SignalType { get; init; }
        public SignalDirection SignalDirection { get; init; }

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
    public sealed partial record CrossoverTrackItem : TrackItemModel
    {
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public CrossoverTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record CarSpawnerTrackItem : TrackItemModel
    {
        [MemoryPackConstructor]
        public CarSpawnerTrackItem(in WorldLocation location) : base(location)
        {
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EmptyTrackItem : TrackItemModel
    {
        [MemoryPackConstructor]
        public EmptyTrackItem(in WorldLocation location) : base(location)
        {
        }
    }
}
