using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Signalling;

namespace Orts.Simulation.Signalling
{
    /// <summary>
    /// Extension methods for <see cref="SignalType"/> model providing runtime query logic
    /// equivalent to the former MSTS SignalType helper methods.
    /// </summary>
    internal static class SignalTypeExtensions
    {
        /// <summary>
        /// Returns the default draw state index for the specified aspect, or -1 if none.
        /// </summary>
        public static int GetDefaultDrawState(this SignalType signalType, SignalAspectState state)
        {
            foreach (SignalAspect aspect in signalType.SignalAspects)
            {
                if (state == aspect.Aspect &&
                    signalType.DrawStates.TryGetValue(aspect.DrawStateName, out SignalDrawState drawState))
                {
                    return drawState.Index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns the most restrictive aspect defined for this signal type.
        /// </summary>
        public static SignalAspectState GetMostRestrictiveAspect(this SignalType signalType)
        {
            SignalAspectState targetAspect = SignalAspectState.Unknown;
            foreach (SignalAspect aspect in signalType.SignalAspects)
            {
                if (aspect.Aspect < targetAspect)
                    targetAspect = aspect.Aspect;
            }
            return targetAspect == SignalAspectState.Unknown ? SignalAspectState.Stop : targetAspect;
        }

        /// <summary>
        /// Returns the least restrictive aspect defined for this signal type.
        /// </summary>
        public static SignalAspectState GetLeastRestrictiveAspect(this SignalType signalType)
        {
            SignalAspectState targetAspect = SignalAspectState.Stop;
            foreach (SignalAspect aspect in signalType.SignalAspects)
            {
                if (aspect.Aspect > targetAspect)
                    targetAspect = aspect.Aspect;
            }
            return targetAspect > SignalAspectState.Clear2 ? SignalAspectState.Clear2 : targetAspect;
        }
    }
}
