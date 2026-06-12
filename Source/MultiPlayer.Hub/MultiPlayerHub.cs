using System;
using System.Collections.Concurrent;
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

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, SessionData>> Rooms = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, SessionData>>();

        private IGroup<IMultiplayerClient> session;
        private ConcurrentDictionary<Guid, SessionData> sessionStorage;
        private SessionData currentSession;
        private string sessionName;

        public ValueTask SendMessageAsync(MultiplayerMessage message)
        {
            if (session != null && currentSession != null)
                session.Except(new[] { currentSession.SessionId }).OnReceiveMessage(message);

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
            sessionName = Convert.ToBase64String(XxHash64.Hash(MemoryMarshal.AsBytes(string.Join('|', route, room).AsSpan())));
            session = await Group.AddAsync(sessionName).ConfigureAwait(false);
            sessionStorage = Rooms.GetOrAdd(sessionName, static _ => new ConcurrentDictionary<Guid, SessionData>());
            sessionStorage[currentSession.SessionId] = currentSession;

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
            if (session != null && currentSession != null)
            {
                session.Except(new[] { currentSession.SessionId }).OnReceiveMessage(new MultiplayerMessage() { MessageType = MessageType.Lost, PayloadAsString = currentSession.UserName });

                if (sessionStorage != null)
                {
                    _ = sessionStorage.TryRemove(currentSession.SessionId, out _);
                    if (currentSession.Dispatcher)
                        AppointDispatcher(true);

                    if (sessionStorage.IsEmpty && !string.IsNullOrEmpty(sessionName))
                        _ = Rooms.TryRemove(sessionName, out _);
                }

                Console.WriteLine($"{DateTime.UtcNow} Player {currentSession.UserName} left room {currentSession.RoomName} on route {currentSession.RouteName}");
            }

            await base.OnDisconnected().ConfigureAwait(false);
        }

        #region dispatcher election
        private void AppointDispatcher(bool reappoint)
        {
            if (sessionStorage == null || currentSession == null || session == null)
                return;

            SessionData dispatcher = reappoint
                ? sessionStorage.Values.OrderBy(sessionData => sessionData.TimeJoined).FirstOrDefault()
                : sessionStorage.Values.SingleOrDefault(sessionData => sessionData.Dispatcher);

            if (dispatcher == null)
                dispatcher = reappoint
                    ? sessionStorage.Values.OrderBy(sessionData => sessionData.TimeJoined).FirstOrDefault()
                    : currentSession;

            if (dispatcher == null)
                return;

            foreach (SessionData sessionData in sessionStorage.Values)
                sessionData.Dispatcher = sessionData.SessionId == dispatcher.SessionId;

            Console.WriteLine($"{DateTime.UtcNow} Player {dispatcher.UserName} is now dispatcher for {currentSession.RouteName}");

            MultiplayerMessage dispatcherMessage = new MultiplayerMessage() { MessageType = MessageType.Server, PayloadAsString = dispatcher.UserName };
            if (reappoint)
                session.All.OnReceiveMessage(dispatcherMessage);
            else
                session.Single(currentSession.SessionId).OnReceiveMessage(dispatcherMessage);
        }
        #endregion
    }
}
