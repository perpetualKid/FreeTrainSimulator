using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;

using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

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
    public class SoundTrigger
    {
        /// <summary>
        /// Set by the DisableTrigger, EnableTrigger sound commands
        /// </summary>
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// True if trigger activation conditions are met
        /// </summary>
        public bool Signaled { get; set; }
        /// <summary>
        /// Represents a sound command to be executed, when trigger is activated
        /// </summary>
        public SoundCommand SoundCommand { get; }

        /// <summary>
        /// Check in every update loop whether to activate the trigger
        /// </summary>
        public virtual void CheckTrigger() { }
        /// <summary>
        /// Executed in constructors, or when sound source gets into scope, or for InitialTrigger when other VariableTriggers stop working
        /// </summary>
        public virtual void Initialize() { }

        public SoundTrigger() { }

        protected SoundTrigger(SoundCommand soundCommand)
        {
            SoundCommand = soundCommand;
        }
    }

    /// <summary>
    /// Play this sound when a discrete TrainCar event occurs in the simulator
    /// </summary>
    public class DiscreteSoundTrigger : SoundTrigger
    {
        /// <summary>
        /// Store the owning SoundStream
        /// </summary>
        private readonly SoundStream soundStream;
        /// <summary>
        /// This flag is set by Updater process, and is used by Sound process to activate the trigger
        /// </summary>
        private bool Triggered;

        /// <summary>
        /// Event this trigger listens to
        /// </summary>
        public TrainEvent TriggerId { get; set; }

        public DiscreteSoundTrigger(SoundStream soundStream, SoundEventSource eventSound, DiscreteTrigger smsData) :
            base(Sound.SoundCommand.FromMsts(smsData?.SoundCommand, soundStream))
        {
            TriggerId = SoundEvent.From(eventSound, smsData.TriggerId);
            this.soundStream = soundStream;
        }

        /// <summary>
        /// Construct a discrete sound trigger with an arbitrary event trigger and sound command.
        /// </summary>
        /// <param name="soundStream">The parent sound stream.</param>
        /// <param name="triggerID">The trigger to activate this event.</param>
        /// <param name="soundCommand">The command to run when activated.</param>
        public DiscreteSoundTrigger(SoundStream soundStream, TrainEvent triggerID, SoundCommand soundCommand) :
            base((soundCommand))
        {
            TriggerId = triggerID;
            this.soundStream = soundStream;
        }

        /// <summary>
        /// Check if this trigger listens to an event, and if also belongs to the object
        /// </summary>
        /// <param name="trainEvent">Occured event</param>
        /// <param name="viewer">Object the event belongs to</param>
        internal void OnCarSoundEvent(object sender, SoundSourceEventArgs e)
        {
            if (e.SoundEvent == TriggerId)
            {
                Triggered = e.Owner == null || Program.Viewer.SoundProcess.IsSoundSourceOwnedBy(e.Owner, soundStream.SoundSource);
            }
        }

        public override void CheckTrigger()
        {
            Triggered &= Enabled;
            if (Triggered)
            {
                Triggered = false;
                soundStream.RepeatedTrigger = this == soundStream.LastTriggered;
                SoundCommand.Run();
                soundStream.LastTriggered = this;
                Signaled = true;
            }
            // If the SoundSource is not active, should deactivate the SoundStream also
            //   preventing the hearing when not should be audible
            if (!soundStream.SoundSource.Active)
                soundStream.Deactivate();
        }
    }

    /// <summary>
    /// Play this sound controlled by the distance a TrainCar has travelled
    /// </summary>
    public sealed class DistanceTravelledSoundTrigger : SoundTrigger
    {
        private readonly DistanceTravelledTrigger soundStreamData;
        private float triggerDistance;
        private readonly TrainCar car;
        private readonly SoundStream soundStream;

        public DistanceTravelledSoundTrigger(SoundStream soundStream, DistanceTravelledTrigger smsData) :
            base(Sound.SoundCommand.FromMsts(smsData?.SoundCommand, soundStream))
        {
            this.soundStream = soundStream ?? throw new ArgumentNullException(nameof(soundStream));
            car = soundStream.SoundSource.Car;
            soundStreamData = smsData;
            Initialize();
        }

        public override void Initialize()
        {
            UpdateTriggerDistance();
        }

        public override void CheckTrigger()
        {
            if (car.DistanceTravelled > triggerDistance)
            {
                Signaled = true;
                if (Enabled)
                {
                    soundStream.RepeatedTrigger = this == soundStream.LastTriggered;
                    SoundCommand.Run();
                    float volume = (float)StaticRandom.NextDouble() * (soundStreamData.MaximumVolume - soundStreamData.MinimumVolume) + soundStreamData.MinimumVolume;
                    soundStream.Volume = volume;
                    soundStream.LastTriggered = this;
                }
                UpdateTriggerDistance();
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
            if (soundStreamData.MaximumDistance != soundStreamData.MinimumDistance)
            {
                triggerDistance = car.DistanceTravelled + ((float)StaticRandom.NextDouble() * (soundStreamData.MaximumDistance - soundStreamData.MinimumDistance) + soundStreamData.MinimumDistance);
            }
            else
            {
                triggerDistance = car.DistanceTravelled + ((float)StaticRandom.NextDouble() * soundStreamData.MinimumDistance + soundStreamData.MinimumDistance);
            }
        }

    } // class ORTSDistanceTravelledTrigger

    /// <summary>
    /// Play this sound immediately when this SoundSource becomes active, or in case no other VariableTriggers are active
    /// </summary>
    public class InitialSoundTrigger : SoundTrigger
    {
        private readonly SoundStream soundStream;

        public InitialSoundTrigger(SoundStream soundStream, InitialTrigger smsData) :
            base(Sound.SoundCommand.FromMsts(smsData?.SoundCommand, soundStream))
        {
            this.soundStream = soundStream;
        }

        // For pre-compiled activity sound
        public InitialSoundTrigger(SoundStream soundStream, string wavFileName) : base(SoundCommand.Precompiled(wavFileName, soundStream))
        {
            this.soundStream = soundStream;
        }

        public override void Initialize()
        {
            if (Enabled)
            {
                soundStream.RepeatedTrigger = this == soundStream.LastTriggered;
                SoundCommand.Run();
                soundStream.LastTriggered = this;
            }

            Signaled = true;
        }
    }

    /// <summary>
    /// Play the sound at random times
    /// </summary>
    public sealed class RandomSoundTrigger : SoundTrigger
    {
        private readonly RandomTrigger soundstreamData;
        private double triggerAtSeconds;
        private readonly SoundStream soundStream;

        public RandomSoundTrigger(SoundStream soundStream, RandomTrigger smsData) :
            base(Sound.SoundCommand.FromMsts(smsData?.SoundCommand, soundStream))
        {
            this.soundStream = soundStream;
            soundstreamData = smsData;
            Initialize();
        }

        public override void Initialize()
        {
            UpdateTriggerAtSeconds();
        }

        public override void CheckTrigger()
        {
            if (Simulator.Instance.ClockTime > triggerAtSeconds)
            {
                Signaled = true;
                if (Enabled)
                {
                    soundStream.RepeatedTrigger = this == soundStream.LastTriggered;
                    SoundCommand.Run();
                    float volume = (float)StaticRandom.NextDouble() * (soundstreamData.MaximumVolume - soundstreamData.MinimumVolume) + soundstreamData.MinimumVolume;
                    soundStream.Volume = volume;
                    soundStream.LastTriggered = this;
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
            double interval = StaticRandom.NextDouble() * (soundstreamData.MaximumDelay - soundstreamData.MinimumDelay) + soundstreamData.MinimumDelay;
            triggerAtSeconds = Simulator.Instance.ClockTime + interval;
        }

    }  // class RandomTrigger

    /// <summary>
    /// Control sounds based on TrainCar variables in the simulator 
    /// </summary>
    public sealed class VariableSoundTrigger : SoundTrigger
    {
        private VariableTrigger soundStreamData;
        private readonly MSTSWagon car;
        private readonly SoundStream soundStream;
        private float startValue;
        public bool BelowThreshold { get; private set; }

        public VariableSoundTrigger(SoundStream soundStream, VariableTrigger smsData) :
            base(Sound.SoundCommand.FromMsts(smsData?.SoundCommand, soundStream))
        {
            soundStreamData = smsData;
            this.soundStream = soundStream ?? throw new ArgumentNullException(nameof(soundStream));
            car = soundStream.SoundSource.Car ?? (MSTSWagon)Program.Viewer.Camera.AttachedCar;
            Initialize();
        }

        public override void Initialize()
        {
            startValue = soundStreamData.Event == VariableTrigger.TriggerEvent.DistanceDecrease ? float.MaxValue : 0;

            /*if ((new Variable_Trigger.Events[] { Variable_Trigger.Events.Variable1_Dec_Past,
                Variable_Trigger.Events.Variable1_Inc_Past, Variable_Trigger.Events.Variable2_Dec_Past, 
                Variable_Trigger.Events.Variable2_Inc_Past, Variable_Trigger.Events.Variable3_Dec_Past,
                Variable_Trigger.Events.Variable3_Inc_Past}).Contains(SMS.Event) && SMS.Threshold >= 1)
            {
                SMS.Threshold /= 100f;
            }*/
            BelowThreshold = startValue < soundStreamData.Threshold;
        }

        public override void CheckTrigger()
        {
            float newValue = ReadValue();
            bool triggered = false;
            Signaled = false;

            switch (soundStreamData.Event)
            {
                case VariableTrigger.TriggerEvent.DistanceDecrease:
                case VariableTrigger.TriggerEvent.SpeedDecrease:
                case VariableTrigger.TriggerEvent.Variable1Decrease:
                case VariableTrigger.TriggerEvent.Variable2Decrease:
                case VariableTrigger.TriggerEvent.Variable3Decrease:
                case VariableTrigger.TriggerEvent.BrakeCylinderDecrease:
                case VariableTrigger.TriggerEvent.CurveForceDecrease:
                    if (newValue < soundStreamData.Threshold)
                    {
                        Signaled = true;
                        if (soundStreamData.Threshold <= startValue)
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
                    if (newValue > soundStreamData.Threshold)
                    {
                        Signaled = true;
                        if (soundStreamData.Threshold >= startValue)
                            triggered = true;
                    }
                    break;
            }

            //Signaled = triggered;

            startValue = newValue;
            BelowThreshold = newValue < soundStreamData.Threshold;

            if (triggered && Enabled)
            {
                soundStream.RepeatedTrigger = this == soundStream.LastTriggered;
                SoundCommand.Run();
                soundStream.LastTriggered = this;
            }
        }

        /// <summary>
        /// Read the desired variable either from the attached TrainCar, or the distance to sound source
        /// </summary>
        /// <returns></returns>
        private float ReadValue()
        {
            return soundStreamData.Event switch
            {
                VariableTrigger.TriggerEvent.DistanceDecrease or VariableTrigger.TriggerEvent.DistanceIncrease => soundStream.SoundSource.DistanceSquared,
                VariableTrigger.TriggerEvent.SpeedDecrease or VariableTrigger.TriggerEvent.SpeedIncrease => car.AbsSpeedMpS,
                VariableTrigger.TriggerEvent.Variable1Decrease or VariableTrigger.TriggerEvent.Variable1Increase => car.SoundValues.X,
                VariableTrigger.TriggerEvent.Variable2Decrease or VariableTrigger.TriggerEvent.Variable2Increase => car.SoundValues.Y,
                VariableTrigger.TriggerEvent.Variable3Decrease or VariableTrigger.TriggerEvent.Variable3Increase => car.SoundValues.Z,
                VariableTrigger.TriggerEvent.BrakeCylinderDecrease or VariableTrigger.TriggerEvent.BrakeCylinderIncrease => car.BrakeSystem.GetCylPressurePSI(),
                VariableTrigger.TriggerEvent.CurveForceDecrease or VariableTrigger.TriggerEvent.CurveForceIncrease => (float)car.CurveForceFiltered,
                _ => 0,
            };
        }
    }
}
