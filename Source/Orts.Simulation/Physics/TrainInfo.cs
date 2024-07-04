using System.Collections.Generic;

using FreeTrainSimulator.Common;

using Orts.Common;

namespace Orts.Simulation.Physics
{
    //================================================================================================//
    /// <summary>
    /// Class for info to TrackMonitor display
    /// <\summary>
    public class TrainInfo
    {
        public TrainControlMode ControlMode { get; private set; }            // present control mode 
        public float Speed { get; private set; }                             // present speed
        public float ProjectedSpeed { get; private set; }                    // projected speed
        public float AllowedSpeed { get; private set; }                      // max allowed speed
        public float Gradient { get; private set; }                          // Gradient %
        public Direction Direction { get; private set; }                     // present direction (0=forward, 1=backward)
        public Direction CabOrientation { get; private set; }                // present cab orientation (0=forward, 1=backward)
        public bool PathDefined { get; private set; }                        // train is on defined path (valid in Manual mode only)
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

        public void Clear()
        {
            ControlMode = TrainControlMode.Undefined;
            Speed = 0;
            ProjectedSpeed = 0;
            AllowedSpeed = 0;
            Gradient = 0;
            Direction = 0;
            CabOrientation = 0;
            PathDefined = false;
            ObjectInfoForward.Clear();
            ObjectInfoBackward.Clear();
        }
    }
}
