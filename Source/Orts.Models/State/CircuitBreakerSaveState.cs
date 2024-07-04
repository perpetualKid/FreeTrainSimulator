using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class CircuitBreakerSaveState : SaveStateBase
    {
        public string ScriptName { get; set; }
        public double DelayTimer { get; set; }
        public CircuitBreakerState CircuitBreakerState { get; set; }
        public TractionCutOffRelayState TractionCutOffRelayState { get; set; }
        public bool DriverClosingOrder { get; set; }
        public bool DriverOpeningOrder { get; set; }
        public bool DriverClosingAuthorization { get; set; }
        public bool ClosingAuthorization { get; set; }
    }
}
