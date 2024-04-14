using MemoryPack;

using Orts.Common;

namespace Orts.Simulation.MultiPlayer.Messaging
{
    [MemoryPackable]
    public partial class LostMessage : MultiPlayerMessageContent
    {
        public string User { get; set; }

        public override void HandleMessage()
        {
            if (User == multiPlayerManager.UserName)
                return; //avoid myself

            if (!multiPlayerManager.IsDispatcher)
            {
                return; //only server will handle this
            }
            if (!MultiPlayerManager.OnlineTrains.Players.TryGetValue(User, out OnlinePlayer player))
                return;
            Simulator.Instance.Confirmer?.Information(MultiPlayerManager.Catalog.GetString("{0} lost.", User));
            if (player.Protected == true)
            {
                player.Protected = false;
                return;
            }
            MultiPlayerManager.BroadCast((new MSGQuit(User)).ToString()); //if the server, will broadcast a quit to every one
            //if the one quit controls my train, I will gain back the control
            if (player.Train == Simulator.Instance.PlayerLocomotive.Train)
                Simulator.Instance.PlayerLocomotive.Train.TrainType = TrainType.Player;
            multiPlayerManager.AddRemovedPlayer(player);
            //the client may quit because of lost connection, will remember it so it may recover in the future when the player log in again
            if (player.Train != null && player.Status != OnlinePlayerStatus.Removed) //if this player has train and is not removed by the dispatcher
            {
                multiPlayerManager.lostPlayer.TryAdd(player.Username, player);
                player.QuitTime = Simulator.Instance.GameTime;
                player.Train.SpeedMpS = 0.0f;
                player.Status = OnlinePlayerStatus.Quit;
            }
            MultiPlayerManager.BroadCast(new MSGQuit(User).ToString()); //broadcast twice
        }
    }
}
