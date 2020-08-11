using System;

using Orts.Common;

namespace Orts.Scripting.Api.PowerSupply
{
    /// <summary>
    /// Circuit breaker for electric and dual mode locomotives
    /// </summary>
    public abstract class CircuitBreaker : TractionCutOffSubsystem
    {
        /// <summary>
        /// Current state of the circuit breaker
        /// </summary>
        public Func<CircuitBreakerState> CurrentState;
        /// <summary>
        /// TCS' circuit breaker closing order
        /// </summary>
        public Func<bool> TCSClosingOrder;
        /// <summary>
        /// TCS' circuit breaker opening order
        /// </summary>
        public Func<bool> TCSOpeningOrder;

        /// <summary>
        /// Sets the current state of the circuit breaker
        /// </summary>
        public Action<CircuitBreakerState> SetCurrentState;
    }
}
