namespace FreeTrainSimulator.Online
{
    public interface IMultiplayerClient
    {
        void OnReceiveMessage(MultiplayerMessage message);
    }
}
