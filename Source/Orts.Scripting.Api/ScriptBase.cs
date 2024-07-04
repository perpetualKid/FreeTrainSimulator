using System;

using FreeTrainSimulator.Common;

namespace Orts.Scripting.Api
{
    /// <summary>
    /// Base class for all scripts. Contains information about the simulation.
    /// </summary>
    public abstract class ScriptBase
    {
        /// <summary>
        /// Clock value (in seconds) for the simulation. Starts with a value = session start time.
        /// </summary>
        public Func<double> ClockTime { get; set; }
        /// <summary>
        /// Clock value (in seconds) for the simulation. Starts with a value = 0.
        /// </summary>
        public Func<double> GameTime { get; set; }
        /// <summary>
        /// Simulator is in pre-update mode (update during loading screen).
        /// </summary>
        public Func<bool> PreUpdate { get; set; }
    }

    /// <summary>
    /// Base class for scripts related to train subsystems.
    /// Provides train specific features such as speed and travelled distance.
    /// </summary>
    public abstract class TrainScriptBase: ScriptBase
    { 
        /// <summary>
        /// Running total of distance travelled - always positive, updated by train physics.
        /// </summary>
        public Func<double> DistanceM { get; set; }
        /// Train's actual absolute speed.
        /// </summary>
        public Func<float> SpeedMpS { get; set; }
        /// <summary>
        /// <summary>
        /// Confirms a command done by the player with a pre-set message on the screen.
        /// </summary>
        public Action<CabControl, CabSetting> Confirm { get; set; }
        /// <summary>
        /// Displays a message on the screen.
        /// </summary>
        public Action<ConfirmLevel, string> Message { get; set; }
        /// <summary>
        /// Sends an event to the locomotive.
        /// </summary>
        public Action<TrainEvent> SignalEvent { get; set; }
        /// <summary>
        /// Sends an event to the train.
        /// </summary>
        public Action<TrainEvent> SignalEventToTrain { get; set; }
    }
}
