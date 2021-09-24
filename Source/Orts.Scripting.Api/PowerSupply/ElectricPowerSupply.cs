using System;

using Orts.Common;

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
        public Func<PantographState> CurrentPantographState;
        /// <summary>
        /// Current state of the circuit breaker
        /// </summary>
        public Func<CircuitBreakerState> CurrentCircuitBreakerState;
        /// <summary>
        /// Driver's closing order of the circuit breaker
        /// </summary>
        public Func<bool> CircuitBreakerDriverClosingOrder;
        /// <summary>
        /// Driver's opening order of the circuit breaker
        /// </summary>
        public Func<bool> CircuitBreakerDriverOpeningOrder;
        /// <summary>
        /// Driver's closing authorization of the circuit breaker
        /// </summary>
        public Func<bool> CircuitBreakerDriverClosingAuthorization;
        /// <summary>
        /// Voltage of the pantograph
        /// </summary>
        public Func<float> PantographVoltageV;
        /// <summary>
        /// Voltage of the filter
        /// </summary>
        public Func<float> FilterVoltageV;
        /// <summary>
        /// Line voltage
        /// </summary>
        public Func<float> LineVoltageV;

        /// <summary>
        /// Sets the voltage of the pantograph
        /// </summary>
        public Action<float> SetPantographVoltageV;
        /// <summary>
        /// Sets the voltage of the filter
        /// </summary>
        public Action<float> SetFilterVoltageV;
        /// <summary>
        /// Sends an event to the circuit breaker
        /// </summary>
        public Action<PowerSupplyEvent> SignalEventToCircuitBreaker;

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            base.HandleEvent(evt);

            // By default, send the event to every component
            SignalEventToCircuitBreaker(evt);
        }
    }
}
