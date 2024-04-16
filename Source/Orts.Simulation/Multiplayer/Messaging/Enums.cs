namespace Orts.Simulation.Multiplayer.Messaging
{
    public enum ControlMessageType
    {
        None = 0,
        Error,
        Warning,
        Information,
        Message,
        SameNameError,
        SwitchWarning,
        SwitchOK,
        OverspeedOK,
        NoOverspeed,
    }
}
