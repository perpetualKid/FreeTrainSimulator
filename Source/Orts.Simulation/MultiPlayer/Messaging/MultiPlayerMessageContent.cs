namespace Orts.Simulation.MultiPlayer.Messaging
{
    public abstract class MultiPlayerMessageContent
    {
        protected static MultiPlayerManager multiPlayerManager;

        public abstract void HandleMessage();

        internal static void SetMultiPlayerManager(MultiPlayerManager multiPlayerManager)
        {
            MultiPlayerMessageContent.multiPlayerManager = multiPlayerManager;
        }
    }
}
