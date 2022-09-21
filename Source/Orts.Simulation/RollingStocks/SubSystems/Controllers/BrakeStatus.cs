using Orts.Common;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct BrakeStatus
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public ControllerState ControllerState { get; }

        public int PercentageValue { get; }

        public BrakeStatus(ControllerState state, float fraction)
        { 
            ControllerState = state;
            PercentageValue = float.IsNaN(fraction) ? -1 : (int)(fraction * 100);
        }

        public static BrakeStatus Empty { get; } = new BrakeStatus(ControllerState.Dummy, float.NaN);

        public string ToShortString(int textLength = int.MaxValue)
        {
            return ControllerState == ControllerState.Dummy && PercentageValue < 0
                ? string.Empty
                : PercentageValue < 0
                ? ControllerState.GetLocalizedDescription()
                : ControllerState == ControllerState.Dummy ? $"{PercentageValue:N0}%" : $"{ControllerState.GetLocalizedDescription().Max(textLength)} {PercentageValue:N0}%";
        }
}
}
