using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Sound
{
    /////////////////////////////////////////////////////////
    // SOUND TRIGGERS
    /////////////////////////////////////////////////////////

    /// <summary>
    /// Trigger is defined in the SMS file as members of a SoundStream.
    /// They are activated by various events.
    /// When triggered, executes a SoundCommand
    /// </summary>
    public class ORTSTrigger
    {
        /// <summary>
        /// Set by the DisableTrigger, EnableTrigger sound commands
        /// </summary>
        public bool Enabled = true;
        /// <summary>
        /// True if trigger activation conditions are met
        /// </summary>
        public bool Signaled;
        /// <summary>
        /// Represents a sound command to be executed, when trigger is activated
        /// </summary>
        public ORTSSoundCommand SoundCommand;

        /// <summary>
        /// Check in every update loop whether to activate the trigger
        /// </summary>
        public virtual void TryTrigger() { }
        /// <summary>
        /// Executed in constructors, or when sound source gets into scope, or for InitialTrigger when other VariableTriggers stop working
        /// </summary>
        public virtual void Initialize() { }
    }


    /// <summary>
    /// Play this sound when a discrete TrainCar event occurs in the simulator
    /// </summary>
    public class ORTSDiscreteTrigger : ORTSTrigger
    {
        /// <summary>
        /// Event this trigger listens to
        /// </summary>
        public TrainEvent TriggerID;
        /// <summary>
        /// Store the owning SoundStream
        /// </summary>
        private SoundStream SoundStream;
        /// <summary>
        /// This flag is set by Updater process, and is used by Sound process to activate the trigger
        /// </summary>
        private bool Triggered;

        public ORTSDiscreteTrigger(SoundStream soundStream, SoundEventSource eventSound, DiscreteTrigger smsData)
        {
            TriggerID = SoundEvent.From(eventSound, smsData.TriggerId);
            SoundCommand = ORTSSoundCommand.FromMSTS(smsData.SoundCommand, soundStream);
            SoundStream = soundStream;
        }

        /// <summary>
        /// Construct a discrete sound trigger with an arbitrary event trigger and sound command.
        /// </summary>
        /// <param name="soundStream">The parent sound stream.</param>
        /// <param name="triggerID">The trigger to activate this event.</param>
        /// <param name="soundCommand">The command to run when activated.</param>
        public ORTSDiscreteTrigger(SoundStream soundStream, TrainEvent triggerID, ORTSSoundCommand soundCommand)
        {
            TriggerID = triggerID;
            SoundCommand = soundCommand;
            SoundStream = soundStream;
        }

        /// <summary>
        /// Check if this trigger listens to an event, and if also belongs to the object
        /// </summary>
        /// <param name="trainEvent">Occured event</param>
        /// <param name="viewer">Object the event belongs to</param>
        internal void OnCarSoundEvent(object sender, SoundSourceEventArgs e)
        {
            if (e.SoundEvent == TriggerID)
            {
                Triggered = e.Owner == null || Program.Viewer.SoundProcess.IsSoundSourceOwnedBy(e.Owner, SoundStream.SoundSource);
            }
        }

        public override void TryTrigger()
        {
            Triggered &= Enabled;
            if (Triggered)
            {
                Triggered = false;
                SoundStream.RepeatedTrigger = this == SoundStream.LastTriggered;
                SoundCommand.Run();
                SoundStream.LastTriggered = this;
                Signaled = true;
#if DEBUGSCR
                if (SoundCommand is ORTSSoundPlayCommand && !string.IsNullOrEmpty((SoundCommand as ORTSSoundPlayCommand).Files[(SoundCommand as ORTSSoundPlayCommand).iFile]))
                    Trace.WriteLine("({0})DiscreteTrigger: {1}:{2}", SoundStream.ALSoundSource.SoundSourceID, TriggerID, (SoundCommand as ORTSSoundPlayCommand).Files[(SoundCommand as ORTSSoundPlayCommand).iFile]);
                else
                    Trace.WriteLine("({0})DiscreteTrigger: {1}", SoundStream.ALSoundSource.SoundSourceID, TriggerID);
#endif
            }
            // If the SoundSource is not active, should deactivate the SoundStream also
            //   preventing the hearing when not should be audible
            if (!SoundStream.SoundSource.Active)
                SoundStream.Deactivate();
        }

    } // class ORTSDiscreteTrigger

    /// <summary>
    /// Play this sound controlled by the distance a TrainCar has travelled
    /// </summary>
    public sealed class ORTSDistanceTravelledTrigger : ORTSTrigger
    {
        private DistanceTravelledTrigger SMS;
        private float triggerDistance;
        private TrainCar car;
        private SoundStream SoundStream;

        public ORTSDistanceTravelledTrigger(SoundStream soundStream, DistanceTravelledTrigger smsData)
        {
            SoundStream = soundStream;
            car = soundStream.SoundSource.Car;
            SMS = smsData;
            SoundCommand = ORTSSoundCommand.FromMSTS(SMS.SoundCommand, soundStream);
            Initialize();
        }

        public override void Initialize()
        {
            UpdateTriggerDistance();
        }

        public override void TryTrigger()
        {
            if (car.DistanceTravelled > triggerDistance)
            {
                Signaled = true;
                if (Enabled)
                {
                    SoundStream.RepeatedTrigger = this == SoundStream.LastTriggered;
                    SoundCommand.Run();
                    float volume = (float)StaticRandom.NextDouble() * (SMS.MaximumVolume - SMS.MinimumVolume) + SMS.MinimumVolume;
                    SoundStream.Volume = volume;
                    SoundStream.LastTriggered = this;
                }
                UpdateTriggerDistance();
#if DEBUGSCR
                Trace.WriteLine("({0})DistanceTravelledTrigger: Current:{1}, Next:{2}", SoundStream.ALSoundSource.SoundSourceID, car.DistanceM, triggerDistance);
#endif

            }
            else
            {
                Signaled = false;
            }
        }

        /// <summary>
        /// Calculate a new random distance to travel till the next trigger action
        /// </summary>
        private void UpdateTriggerDistance()
        {
            if (SMS.MaximumDistance != SMS.MinimumDistance)
            {
                triggerDistance = car.DistanceTravelled + ((float)StaticRandom.NextDouble() * (SMS.MaximumDistance - SMS.MinimumDistance) + SMS.MinimumDistance);
            }
            else
            {
                triggerDistance = car.DistanceTravelled + ((float)StaticRandom.NextDouble() * SMS.MinimumDistance + SMS.MinimumDistance);
            }
        }

    } // class ORTSDistanceTravelledTrigger

    /// <summary>
    /// Play this sound immediately when this SoundSource becomes active, or in case no other VariableTriggers are active
    /// </summary>
    public class ORTSInitialTrigger : ORTSTrigger
    {
        private SoundStream SoundStream;

        public ORTSInitialTrigger(SoundStream soundStream, InitialTrigger smsData)
        {
            SoundCommand = ORTSSoundCommand.FromMSTS(smsData.SoundCommand, soundStream);
            SoundStream = soundStream;
        }

        // For pre-compiled activity sound
        public ORTSInitialTrigger(SoundStream soundStream, string wavFileName)
        {
            SoundCommand = ORTSSoundCommand.Precompiled(wavFileName, soundStream);
            SoundStream = soundStream;
        }

        public override void Initialize()
        {
            if (Enabled)
            {
                SoundStream.RepeatedTrigger = this == SoundStream.LastTriggered;
                SoundCommand.Run();
                SoundStream.LastTriggered = this;
#if DEBUGSCR
                if (SoundCommand is ORTSSoundPlayCommand && !string.IsNullOrEmpty((SoundCommand as ORTSSoundPlayCommand).Files[(SoundCommand as ORTSSoundPlayCommand).iFile]))
                    Trace.WriteLine("({0})InitialTrigger: {1}", SoundStream.ALSoundSource.SoundSourceID, (SoundCommand as ORTSSoundPlayCommand).Files[(SoundCommand as ORTSSoundPlayCommand).iFile]);
#endif
            }

            Signaled = true;
        }

    }

    /// <summary>
    /// Play the sound at random times
    /// </summary>
    public sealed class ORTSRandomTrigger : ORTSTrigger
    {
        private RandomTrigger SMS;
        private double triggerAtSeconds;
        private SoundStream SoundStream;

        public ORTSRandomTrigger(SoundStream soundStream, RandomTrigger smsData)
        {
            SoundStream = soundStream;
            SMS = smsData;
            SoundCommand = ORTSSoundCommand.FromMSTS(smsData.SoundCommand, soundStream);
            Initialize();
        }

        public override void Initialize()
        {
            UpdateTriggerAtSeconds();
        }

        public override void TryTrigger()
        {
            if (Simulator.Instance.ClockTime > triggerAtSeconds)
            {
                Signaled = true;
                if (Enabled)
                {
                    SoundStream.RepeatedTrigger = this == SoundStream.LastTriggered;
                    SoundCommand.Run();
                    float volume = (float)StaticRandom.NextDouble() * (SMS.MaximumVolume - SMS.MinimumVolume) + SMS.MinimumVolume;
                    SoundStream.Volume = volume;
                    SoundStream.LastTriggered = this;
                }
                UpdateTriggerAtSeconds();
            }
            else
            {
                Signaled = false;
            }
        }

        /// <summary>
        /// Calculate new random time till the next triggering action
        /// </summary>
        private void UpdateTriggerAtSeconds()
        {
            double interval = StaticRandom.NextDouble() * (SMS.MaximumDelay - SMS.MinimumDelay) + SMS.MinimumDelay;
            triggerAtSeconds = Simulator.Instance.ClockTime + interval;
        }

    }  // class RandomTrigger

    /// <summary>
    /// Control sounds based on TrainCar variables in the simulator 
    /// </summary>
    public sealed class ORTSVariableTrigger : ORTSTrigger
    {
        private VariableTrigger SMS;
        private MSTSWagon car;
        private SoundStream SoundStream;
        private float StartValue;
        public bool IsBellow;

        public ORTSVariableTrigger(SoundStream soundStream, VariableTrigger smsData)
        {
            SMS = smsData;
            car = soundStream.SoundSource.Car ?? (MSTSWagon)Program.Viewer.Camera.AttachedCar;
            SoundStream = soundStream;
            SoundCommand = ORTSSoundCommand.FromMSTS(smsData.SoundCommand, soundStream);
            Initialize();
        }

        public override void Initialize()
        {
            StartValue = SMS.Event == VariableTrigger.TriggerEvent.DistanceDecrease ? float.MaxValue : 0;

            /*if ((new Variable_Trigger.Events[] { Variable_Trigger.Events.Variable1_Dec_Past,
                Variable_Trigger.Events.Variable1_Inc_Past, Variable_Trigger.Events.Variable2_Dec_Past, 
                Variable_Trigger.Events.Variable2_Inc_Past, Variable_Trigger.Events.Variable3_Dec_Past,
                Variable_Trigger.Events.Variable3_Inc_Past}).Contains(SMS.Event) && SMS.Threshold >= 1)
            {
                SMS.Threshold /= 100f;
            }*/
            IsBellow = StartValue < SMS.Threshold;
        }

        public override void TryTrigger()
        {
            float newValue = ReadValue();
            bool triggered = false;
            Signaled = false;

            switch (SMS.Event)
            {
                case VariableTrigger.TriggerEvent.DistanceDecrease:
                case VariableTrigger.TriggerEvent.SpeedDecrease:
                case VariableTrigger.TriggerEvent.Variable1Decrease:
                case VariableTrigger.TriggerEvent.Variable2Decrease:
                case VariableTrigger.TriggerEvent.Variable3Decrease:
                case VariableTrigger.TriggerEvent.BrakeCylinderDecrease:
                case VariableTrigger.TriggerEvent.CurveForceDecrease:
                    if (newValue < SMS.Threshold)
                    {
                        Signaled = true;
                        if (SMS.Threshold <= StartValue)
                            triggered = true;
                    }
                    break;
                case VariableTrigger.TriggerEvent.DistanceIncrease:
                case VariableTrigger.TriggerEvent.SpeedIncrease:
                case VariableTrigger.TriggerEvent.Variable1Increase:
                case VariableTrigger.TriggerEvent.Variable2Increase:
                case VariableTrigger.TriggerEvent.Variable3Increase:
                case VariableTrigger.TriggerEvent.BrakeCylinderIncrease:
                case VariableTrigger.TriggerEvent.CurveForceIncrease:
                    if (newValue > SMS.Threshold)
                    {
                        Signaled = true;
                        if (SMS.Threshold >= StartValue)
                            triggered = true;
                    }
                    break;
            }

            //Signaled = triggered;

            StartValue = newValue;
            IsBellow = newValue < SMS.Threshold;

            if (triggered && Enabled)
            {
                SoundStream.RepeatedTrigger = this == SoundStream.LastTriggered;
                SoundCommand.Run();
                SoundStream.LastTriggered = this;

#if DEBUGSCR
                ORTSStartLoop sl = SoundCommand as ORTSStartLoop;
                if (sl != null)
                {
                    Trace.WriteLine("({0})StartLoop ({1} {2}): {3} ", SoundStream.ALSoundSource.SoundSourceID, SMS.Event.ToString(), SMS.Threshold.ToString(), sl.Files[sl.iFile]);
                }
                ORTSStartLoopRelease slr = SoundCommand as ORTSStartLoopRelease;
                if (slr != null)
                {
                    Trace.WriteLine("({0})StartLoopRelease ({1} {2}): {3} ", SoundStream.ALSoundSource.SoundSourceID, SMS.Event.ToString(), SMS.Threshold.ToString(), slr.Files[slr.iFile]);
                }
                ORTSReleaseLoopRelease rlr = SoundCommand as ORTSReleaseLoopRelease;
                if (rlr != null)
                {
                    Trace.WriteLine("({0})ReleaseLoopRelease ({1} {2}) ", SoundStream.ALSoundSource.SoundSourceID, SMS.Event.ToString(), SMS.Threshold.ToString());
                }
                ORTSReleaseLoopReleaseWithJump rlrwj = SoundCommand as ORTSReleaseLoopReleaseWithJump;
                if (rlrwj != null)
                {
                    Trace.WriteLine("({0})ReleaseLoopReleaseWithJump ({1} {2}) ", SoundStream.ALSoundSource.SoundSourceID, SMS.Event.ToString(), SMS.Threshold.ToString());
                }
#endif
            }
        }

        /// <summary>
        /// Read the desired variable either from the attached TrainCar, or the distance to sound source
        /// </summary>
        /// <returns></returns>
        private float ReadValue()
        {
            switch (SMS.Event)
            {
                case VariableTrigger.TriggerEvent.DistanceDecrease:
                case VariableTrigger.TriggerEvent.DistanceIncrease:
                    return SoundStream.SoundSource.DistanceSquared;
                case VariableTrigger.TriggerEvent.SpeedDecrease:
                case VariableTrigger.TriggerEvent.SpeedIncrease:
                    return car.AbsSpeedMpS;
                case VariableTrigger.TriggerEvent.Variable1Decrease:
                case VariableTrigger.TriggerEvent.Variable1Increase:
                    return car.SoundValues.X;
                case VariableTrigger.TriggerEvent.Variable2Decrease:
                case VariableTrigger.TriggerEvent.Variable2Increase:
                    return car.SoundValues.Y;
                case VariableTrigger.TriggerEvent.Variable3Decrease:
                case VariableTrigger.TriggerEvent.Variable3Increase:
                    return car.SoundValues.Z;
                case VariableTrigger.TriggerEvent.BrakeCylinderDecrease:
                case VariableTrigger.TriggerEvent.BrakeCylinderIncrease:
                    return car.BrakeSystem.GetCylPressurePSI();
                case VariableTrigger.TriggerEvent.CurveForceDecrease:
                case VariableTrigger.TriggerEvent.CurveForceIncrease:
                    return (float)car.CurveForceFiltered;
                default:
                    return 0;
            }
        }

    }  // class VariableTrigger

}
