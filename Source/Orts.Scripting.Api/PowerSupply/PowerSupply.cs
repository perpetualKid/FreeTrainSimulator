using System;

using FreeTrainSimulator.Common;

namespace Orts.Scripting.Api.PowerSupply
{
    public abstract class PowerSupply : TrainScriptBase
    {
        /// <summary>
        /// Current state of the electric train supply
        /// ETS is used by the systems of the cars (such as air conditionning)
        /// </summary>
        public Func<PowerSupplyState> CurrentElectricTrainSupplyState { get; set; }
        /// <summary>
        /// Current state of the low voltage power supply
        /// Low voltage power is used by safety systems (such as TCS) or lights
        /// </summary>
        public Func<PowerSupplyState> CurrentLowVoltagePowerSupplyState { get; set; }
        /// <summary>
        /// Current state of the battery
        /// </summary>
        public Func<PowerSupplyState> CurrentBatteryState { get; set; }
        /// <summary>
        /// True if the battery is switched on
        /// </summary>
        public Func<bool> BatterySwitchOn { get; set; }

        /// <summary>
        /// Sets the current state of the low voltage power supply
        /// Low voltage power is used by safety systems (such as TCS) or lights
        /// </summary>
        public Action<PowerSupplyState> SetCurrentLowVoltagePowerSupplyState { get; set; }
        /// <summary>
        /// Sets the current state of the battery
        /// </summary>
        public Action<PowerSupplyState> SetCurrentBatteryState { get; set; }
        /// <summary>
        /// Sends an event to the battery switch
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToBatterySwitch { get; set; }
        /// <summary>
        /// Sends an event to all pantographs
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToPantographs { get; set; }
        /// <summary>
        /// Sends an event to one pantograph
        /// </summary>
        public Action<PowerSupplyEvent, int> SignalEventToPantograph { get; set; }

        /// <summary>
        /// Called once at initialization time.
        /// </summary>
        public abstract void Initialize();
        /// <summary>
        /// Called once at initialization time if the train speed is greater than 0.
        /// Set as virtual to keep compatibility with scripts not providing this method.
        /// </summary>
        public virtual void InitializeMoving() { }
        /// <summary>
        /// Called regularly at every simulator update cycle.
        /// </summary>
        public abstract void Update(double elapsedClockSeconds);
        /// <summary>
        /// Called when the driver (or the train's systems) want something to happen on the power supply system
        /// </summary>
        /// <param name="evt">The event</param>
        public virtual void HandleEvent(PowerSupplyEvent evt)
        {
            // By default, send the event to every component
            SignalEventToBatterySwitch(evt);
        }

        public virtual void HandleEvent(PowerSupplyEvent evt, int id)
        {
            // By default, send the event to every component
            SignalEventToPantograph(evt, id);
        }
        public virtual void HandleEventFromLeadLocomotive(PowerSupplyEvent evt)
        {
            // By default, send the event to every component
            SignalEventToBatterySwitch(evt);
        }

        public virtual void HandleEventFromLeadLocomotive(PowerSupplyEvent evt, int id)
        {
            // By default, send the event to every component
            SignalEventToPantograph(evt, id);
        }
    }
}
