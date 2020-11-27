using System;

using Orts.Common;

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
        public float DistanceToTrainM { get; }
        public bool Enabled { get; }
        public int StationPlatformLength { get; }
        public SpeedItemType SpeedObjectType { get; }
        public bool Valid { get; }
        public string Miles { get; }
        public bool SwitchDivertsRight { get; }

        // field validity :
        // if ItemType == SIGNAL :
        //      SignalState
        //      AllowedSpeedMpS if value > 0
        //      DistanceToTrainM
        //
        // if ItemType == SPEEDPOST :
        //      AllowedSpeedMpS
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


        //================================================================================================//
        /// <summary>
        /// Constructors
        /// <\summary>

        // Constructor for Signal
        public TrainPathItem(TrackMonitorSignalAspect aspect, float speed, float distance)
        {
            ItemType = TrainPathItemType.Signal;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = aspect;
            AllowedSpeedMpS = speed;
            DistanceToTrainM = distance;
        }

        // Constructor for Speedpost
        public TrainPathItem(float speed, float distance, SpeedItemType speedItemType = SpeedItemType.Standard)
        {
            ItemType = TrainPathItemType.Speedpost;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = speed;
            DistanceToTrainM = distance;
            SpeedObjectType = speedItemType;
        }

        // Constructor for Station
        public TrainPathItem(float distance, int platformLength)
        {
            ItemType = TrainPathItemType.Station;
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
        public TrainPathItem(string miles, float distance)
        {
            ItemType = TrainPathItemType.Milepost;
            AuthorityType = EndAuthorityType.NoPathReserved;
            SignalState = TrackMonitorSignalAspect.Clear2;
            AllowedSpeedMpS = -1;
            DistanceToTrainM = distance;
            Miles = miles;
        }

        // Constructor for facing Switch
        public TrainPathItem(bool isRightSwitch, float distance)
        {
            ItemType = TrainPathItemType.FacingSwitch;
            DistanceToTrainM = distance;
            SwitchDivertsRight = isRightSwitch;
        }


        /// no need for Restore or Save items as info is not kept in permanent variables

        //================================================================================================//
        //
        // Compare To (to allow sort)
        //
        public int CompareTo(TrainPathItem other)
        {
            if (other == null)
                return 1;
            if (DistanceToTrainM < other.DistanceToTrainM)
                return (-1);
            if (DistanceToTrainM == other.DistanceToTrainM)
                return (0);
            return 1;
        }
    }
}
