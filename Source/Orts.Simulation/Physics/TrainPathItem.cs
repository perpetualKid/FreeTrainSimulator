using System;

using Orts.Common;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Physics
{
    //================================================================================================//
    /// <summary>
    /// Class TrainObjectItem : info on objects etc. in train path
    /// Used as interface for TrackMonitorWindow as part of TrainInfo class
    /// <\summary>

#pragma warning disable CA1036 // Override methods on comparable types
    public class TrainPathItem : IComparable<TrainPathItem>
#pragma warning restore CA1036 // Override methods on comparable types
    {
        public TrainPathItemType ItemType { get; }
        public OutOfControlReason OutOfControlReason { get; }
        public EndAuthorityType AuthorityType { get; }
        public TrackMonitorSignalAspect SignalState { get; }
        public float AllowedSpeedMpS { get; }
        public bool IsWarning { get; }
        public float DistanceToTrainM { get; }
        public bool Enabled { get; }
        public int StationPlatformLength { get; }
        public SpeedItemType SpeedObjectType { get; }
        public bool Valid { get; }
        public float Miles { get; }
        public bool SwitchDivertsRight { get; }
        public Signal Signal { get; }

        // field validity :
        // if ItemType == SIGNAL :
        //      SignalState
        //      AllowedSpeedMpS if value > 0
        //      DistanceToTrainM
        //      Signal
        //
        // if ItemType == SPEEDPOST :
        //      AllowedSpeedMpS
        //      IsWarning
        //      DistanceToTrainM
        //
        // if ItemType == STATION :
        //      DistanceToTrainM
        //
        // if ItemType == AUTHORITY :
        //      AuthorityType
        //      DistanceToTrainM
        //
        // if ItemType == REVERSAL :
        //      DistanceToTrainM
        //
        // if ItemType == OUTOFCONTROL :
        //      OutOfControlReason
        //
        // if ItemType == GENERIC_SIGNAL :
        //      DistanceToTrainM
        //      Signal


        public static TrainPathItem Undefined { get; } = new TrainPathItem(EndAuthorityType.NoPathReserved, 0.0f);

        //================================================================================================//
        /// <summary>
        /// Constructors
        /// <\summary>

        // Constructor for Signal
        public TrainPathItem(TrackMonitorSignalAspect aspect, float speed, float distance, Signal signal)
        {
            ItemType = TrainPathItemType.Signal;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = aspect;
            AllowedSpeedMpS = speed;
            DistanceToTrainM = distance;
            Signal = signal;
        }

        // Constructor for Speedpost
        public TrainPathItem(float speed, bool warning, float distance, Signal signal, SpeedItemType speedItemType = SpeedItemType.Standard)
        {
            ItemType = TrainPathItemType.Speedpost;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = speed;
            IsWarning = warning;
            DistanceToTrainM = distance;
            SpeedObjectType = speedItemType;
            Signal = signal;
        }

        // Constructor for Station or Tunnel
        public TrainPathItem(float distance, int platformLength, TrainPathItemType itemType)
        {
            ItemType = itemType;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = -1;
            DistanceToTrainM = distance;
            StationPlatformLength = platformLength;
        }

        // Constructor for Reversal
        public TrainPathItem(bool enabled, float distance, bool valid = true)
        {
            ItemType = TrainPathItemType.Reversal;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = -1;
            DistanceToTrainM = distance;
            Enabled = enabled;
            Valid = valid;
        }

        // Constructor for Authority
        public TrainPathItem(EndAuthorityType authority, float distance)
        {
            ItemType = TrainPathItemType.Authority;
            AuthorityType = authority;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = -1;
            DistanceToTrainM = distance;
        }

        // Constructor for OutOfControl
        public TrainPathItem(OutOfControlReason reason)
        {
            ItemType = TrainPathItemType.OutOfControl;
            OutOfControlReason = reason;
        }

        // Constructor for Waiting Point
        public TrainPathItem(float distance, bool enabled)
        {
            ItemType = TrainPathItemType.WaitingPoint;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = -1;
            DistanceToTrainM = distance;
            Enabled = enabled;
        }

        // Constructor for Milepost
        public TrainPathItem(float miles, float distance)
        {
            ItemType = TrainPathItemType.Milepost;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = -1;
            DistanceToTrainM = distance;
            Miles = miles;
        }

        // Constructor for Switches
        public TrainPathItem(bool isRightSwitch, float distance, TrainPathItemType itemType)
        {
            ItemType = itemType;
            DistanceToTrainM = distance;
            SwitchDivertsRight = isRightSwitch;
        }

        /// <summary>
        /// Constructor for generic signals
        /// </summary>
        public TrainPathItem(float distance, Signal signal)
        {
            ItemType = TrainPathItemType.GenericSignal;
            AuthorityType = EndAuthorityType.NoPathReserved;
            DistanceToTrainM = distance;
            Signal = signal;
        }

        //================================================================================================//
        //
        // Compare To (to allow sort)
        //
        public int CompareTo(TrainPathItem other)
        {
            return other == null ? 1 : DistanceToTrainM < other.DistanceToTrainM ? -1 : DistanceToTrainM == other.DistanceToTrainM ? 0 : 1;
        }
    }
}
