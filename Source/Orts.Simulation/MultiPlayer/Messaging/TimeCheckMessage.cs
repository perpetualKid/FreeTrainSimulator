using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class TimeCheckMessage : MultiPlayerMessageContent
    {
        public double DispatcherTime { get; set; }

        public override void HandleMessage()
        {
            multiPlayerManager.ServerTimeDifference = DispatcherTime - Simulator.Instance.ClockTime;
        }
    }
}
