using System;

using FreeTrainSimulator.Common;

namespace Orts.Scripting.Api.PowerSupply
{
    /// <summary>
    /// Power supply for electric locomotives
    /// </summary>
    public abstract class ElectricPowerSupply : LocomotivePowerSupply
    {
        /// <summary>
        /// Current state of the pantograph
        /// </summary>
        public Func<PantographState> CurrentPantographState { get; set; }
        /// <summary>
        /// Current state of the circuit breaker
        /// </summary>
        public Func<CircuitBreakerState> CurrentCircuitBreakerState { get; set; }
        /// <summary>
        /// Driver's closing order of the circuit breaker
        /// </summary>
        public Func<bool> CircuitBreakerDriverClosingOrder { get; set; }
        /// <summary>
        /// Driver's opening order of the circuit breaker
        /// </summary>
        public Func<bool> CircuitBreakerDriverOpeningOrder { get; set; }
        /// <summary>
        /// Driver's closing authorization of the circuit breaker
        /// </summary>
        public Func<bool> CircuitBreakerDriverClosingAuthorization { get; set; }
        /// <summary>
        /// Voltage of the pantograph
        /// </summary>
        public Func<float> PantographVoltageV { get; set; }
        /// <summary>
        /// Voltage of the filter
        /// </summary>
        public Func<float> FilterVoltageV { get; set; }
        /// <summary>
        /// Line voltage
        /// </summary>
        public Func<float> LineVoltageV { get; set; }

        /// <summary>
        /// Sets the voltage of the pantograph
        /// </summary>
        public Action<float> SetPantographVoltageV { get; set; }
        /// <summary>
        /// Sets the voltage of the filter
        /// </summary>
        public Action<float> SetFilterVoltageV { get; set; }
        /// <summary>
        /// Sends an event to the circuit breaker
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToCircuitBreaker { get; set; }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            base.HandleEvent(evt);

            // By default, send the event to every component
            SignalEventToCircuitBreaker(evt);
        }

        public override void HandleEventFromLeadLocomotive(PowerSupplyEvent evt)
        {
            base.HandleEventFromLeadLocomotive(evt);

            // By default, send the event to every component
            SignalEventToCircuitBreaker(evt);
        }
    }
}
