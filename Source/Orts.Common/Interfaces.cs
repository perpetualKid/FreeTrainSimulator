namespace Orts.Common
{
    public interface IRuntimeReferenceResolver
    {
        ISignal SignalById(int signalId);
        ISwitch SwitchByUId(uint switchId);
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
        MainRoute,
        SideRoute,
    }

    public interface ISignal
    { 
        SignalState State { get; set; }
    }

    public interface ISwitch
    {
        SwitchState State { get; set; }
    }
}
