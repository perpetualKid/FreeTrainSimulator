using System;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using MagicOnion.Server.Hubs;

using MultiPlayer.Shared;

namespace MultiPlayer.Hub
{
    public sealed class MultiPlayerHub : StreamingHubBase<IMultiPlayerHub, IMultiPlayerClient>, IMultiPlayerHub
    {
        private sealed class SessionData
        {
            public Guid SessionId { get; set; }
            public string UserName { get; set; }
            public string RouteName { get; set; }
            public DateTime TimeJoined { get; set; }
            public bool Dispatcher { get; set; }
        }

        private IGroup session;
        private IInMemoryStorage<SessionData> sessionStorage;
        private SessionData currentSession;

        public ValueTask SendMessageAsync(MultiPlayerMessage message)
        {
            BroadcastExceptSelf(session).OnReceiveMessage(message);
            return ValueTask.CompletedTask;
        }

        public async ValueTask JoinGameAsync(string userName, string route, string accessCode)
        {
            currentSession = new SessionData()
            {
                SessionId = Context.ContextId,
                UserName = userName,
                RouteName = route,
                TimeJoined = DateTime.UtcNow,
            };
            string sessionName = Convert.ToBase64String(XxHash64.Hash(MemoryMarshal.AsBytes(string.Join('|', route, accessCode).AsSpan())));
            (session, sessionStorage) = await Group.AddAsync(sessionName, currentSession).ConfigureAwait(false);
            AppointDispatcher(false);
            Console.WriteLine($"{DateTime.UtcNow} Player {userName} joined on route {route}");
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
            BroadcastExceptSelf(session).OnReceiveMessage(new MultiPlayerMessage() { MessageType = MessageType.Lost, PayloadAsString = currentSession.UserName });
            if (currentSession.Dispatcher)
            {
                AppointDispatcher(true);
            }
            Console.WriteLine($"{DateTime.UtcNow} Player {currentSession.UserName} left on route {currentSession.RouteName}");
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
            }
            if (reappoint)
            {
                Broadcast(session).OnReceiveMessage(new MultiPlayerMessage() { MessageType = MessageType.Server, PayloadAsString = dispatcher.UserName });
            }
            else
            {
                BroadcastToSelf(session).OnReceiveMessage(new MultiPlayerMessage() { MessageType = MessageType.Server, PayloadAsString = dispatcher.UserName });
            }
        }
        #endregion
    }
}
