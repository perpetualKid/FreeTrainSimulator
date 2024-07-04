using FreeTrainSimulator.Common;

using Orts.Common;

namespace Orts.Scripting.Api
{
    public interface IControllerNotch
    {
        float Value { get; set; }
        bool Smooth { get; set; }
        ControllerState NotchStateType { get; set; }
    }
}
