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
}
