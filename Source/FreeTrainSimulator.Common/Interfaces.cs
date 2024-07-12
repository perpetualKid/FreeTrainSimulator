using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common
{
    public interface IRuntimeReferenceResolver
    {
        ISignal SignalById(int signalId);
        IJunction SwitchById(int switchId);
    }

    public enum SignalState
    {
        Clear,
        Lock,
        Approach,
        Manual,
        CallOn,
    }

    public enum SwitchState
    {
        Invalid = -1,
        MainRoute,
        SideRoute,
    }

    public interface ISignal
    {
        SignalState State { get; set; }
        public bool CallOnEnabled { get; }
    }

    public interface IJunction
    {
        SwitchState State { get; set; }
    }

    public interface ITrain
    {
        string Name { get; }
        int Number { get; }
        TrainType TrainType { get; }
    }

    /// <summary>
    /// Interface for mouse controllable CabViewControls
    /// </summary>
    public interface ICabViewMouseControlRenderer
    {
        bool IsMouseWithin(Point mousePoint);
        void HandleUserInput(GenericButtonEventType buttonEventType, Point position, Vector2 delta);
        string GetControlName(Point mousePoint);
    }
}
