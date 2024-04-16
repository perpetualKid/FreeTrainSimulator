namespace Orts.Simulation.Multiplayer.Messaging
{
    public abstract class MultiPlayerMessageContent
    {
        protected static MultiPlayerManager multiPlayerManager;

        public abstract void HandleMessage();

        public string User {  get; set; } = multiPlayerManager?.UserName;

        internal static void SetMultiPlayerManager(MultiPlayerManager multiPlayerManager)
        {
            MultiPlayerMessageContent.multiPlayerManager = multiPlayerManager;
        }
    }
}
