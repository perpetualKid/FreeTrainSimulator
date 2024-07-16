using System;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using FreeTrainSimulator.Online;

using MagicOnion.Server.Hubs;

namespace Multiplayer.Hub
{
    public sealed class MultiplayerHub : StreamingHubBase<IMultiplayerHub, IMultiplayerClient>, IMultiplayerHub
    {
        private sealed class SessionData
        {
            public Guid SessionId { get; set; }
            public string UserName { get; set; }
            public string RouteName { get; set; }
            public string RoomName { get; set; }
            public DateTime TimeJoined { get; set; }
            public bool Dispatcher { get; set; }
        }

        private IGroup session;
        private IInMemoryStorage<SessionData> sessionStorage;
        private SessionData currentSession;

        public ValueTask SendMessageAsync(MultiplayerMessage message)
        {
            BroadcastExceptSelf(session).OnReceiveMessage(message);
            return ValueTask.CompletedTask;
        }

        public async ValueTask JoinGameAsync(string userName, string route, string room)
        {
            currentSession = new SessionData()
            {
                SessionId = Context.ContextId,
                UserName = userName,
                RouteName = route,
                RoomName = room,
                TimeJoined = DateTime.UtcNow,
            };
            string sessionName = Convert.ToBase64String(XxHash64.Hash(MemoryMarshal.AsBytes(string.Join('|', route, room).AsSpan())));
            (session, sessionStorage) = await Group.AddAsync(sessionName, currentSession).ConfigureAwait(false);
            Console.WriteLine($"{DateTime.UtcNow} Player {userName} joined room {room} for route {route}");
            AppointDispatcher(false);
        }

        protected override ValueTask OnConnecting()
        {
            return base.OnConnecting();
        }

        protected override ValueTask OnConnected()
        {
            return base.OnConnected();
        }

        protected override async ValueTask OnDisconnected()
        {
            BroadcastExceptSelf(session).OnReceiveMessage(new MultiplayerMessage() { MessageType = MessageType.Lost, PayloadAsString = currentSession.UserName });
            if (currentSession.Dispatcher)
            {
                AppointDispatcher(true);
            }
            Console.WriteLine($"{DateTime.UtcNow} Player {currentSession.UserName} left room {currentSession.RoomName} on route {currentSession.RouteName}");
            await base.OnDisconnected().ConfigureAwait(false);
        }

        #region dispatcher election
        private void AppointDispatcher(bool reappoint)
        {
            SessionData dispatcher = reappoint
                ? sessionStorage.AllValues.Where(session => session.SessionId != currentSession.SessionId).FirstOrDefault()
                : sessionStorage.AllValues.Where(m => m.Dispatcher == true).SingleOrDefault();
            if (dispatcher == null)
            {
                dispatcher = currentSession;
                dispatcher.Dispatcher = true;
                Console.WriteLine($"{DateTime.UtcNow} Player {dispatcher.UserName} is now dispatcher for {currentSession.RouteName}");
            }
            if (reappoint)
            {
                Console.WriteLine($"{DateTime.UtcNow} Player {dispatcher.UserName} is now dispatcher for {currentSession.RouteName}");
                Broadcast(session).OnReceiveMessage(new MultiplayerMessage() { MessageType = MessageType.Server, PayloadAsString = dispatcher.UserName });
            }
            else
            {
                BroadcastToSelf(session).OnReceiveMessage(new MultiplayerMessage() { MessageType = MessageType.Server, PayloadAsString = dispatcher.UserName });
            }
        }
        #endregion
    }
}
