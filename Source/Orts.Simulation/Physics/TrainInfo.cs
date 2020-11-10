using System.Collections.Generic;

using Orts.Common;

using static Orts.Simulation.Physics.Train;

namespace Orts.Simulation.Physics
{
    //================================================================================================//
    /// <summary>
    /// Class for info to TrackMonitor display
    /// <\summary>
    public class TrainInfo
    {
        public TrainControlMode ControlMode { get; }            // present control mode 
        public float Speed { get; }                             // present speed
        public float ProjectedSpeed { get; }                    // projected speed
        public float AllowedSpeed { get; }                      // max allowed speed
        public float Gradient { get; }                          // Gradient %
        public Direction Direction { get; }                     // present direction (0=forward, 1=backward)
        public Direction CabOrientation { get; }                // present cab orientation (0=forward, 1=backward)
        public bool PathDefined { get; }                        // train is on defined path (valid in Manual mode only)
        public List<TrainPathItem> ObjectInfoForward { get; } // forward objects
        public List<TrainPathItem> ObjectInfoBackward { get; }// backward objects

        //================================================================================================//
        /// <summary>
        /// Constructor - creates empty objects, data is filled by GetInfo routine from Train
        /// <\summary>

        public TrainInfo()
        {
            ObjectInfoForward = new List<TrainPathItem>();
            ObjectInfoBackward = new List<TrainPathItem>();
        }

        public TrainInfo(TrainControlMode controlMode, Direction direction, float speed):
            this()
        {
            ControlMode = controlMode;
            Direction = direction;
            Speed = speed;
        }

        public TrainInfo(TrainControlMode controlMode, Direction direction, float speed, float projectedSpeed, 
            float allowedSpeedMpS, float gradient, Direction cabOrientation, bool onPath) :
            this()
        {
            ControlMode = controlMode;
            Direction = direction;
            Speed = speed;
            ProjectedSpeed = projectedSpeed;
            AllowedSpeed = allowedSpeedMpS;
            Gradient = gradient;
            CabOrientation = cabOrientation;
            PathDefined = onPath;
        }
    }
}
