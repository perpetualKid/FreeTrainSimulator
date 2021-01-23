using Orts.Simulation.Signalling;

namespace Orts.Simulation.Physics
{
    //================================================================================================//
    /// <summary>
    /// Generation of Train data useful for TCS and TrackMonitor; player train only
    /// <\summary>
    public class TrainSignal
    {
        public float DistanceToTrain { get; }
        public Signal SignalObjectDetails { get; }

        public TrainSignal(float distanceToTrainM, Signal objectDetails)
        {
            DistanceToTrain = distanceToTrainM;
            SignalObjectDetails = objectDetails;
        }
    }
}
