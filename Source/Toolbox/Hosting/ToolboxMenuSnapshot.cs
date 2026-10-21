using System;
using System.Collections.Immutable;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>A menu identity in its content namespace, independent of native model equality and lazy caches.</summary>
    internal sealed class ToolboxMenuKey : IEquatable<ToolboxMenuKey>
    {
        /// <summary>The persisted folder ID, not its display name.</summary>
        public string FolderId { get; }
        /// <summary>The route ID within the folder, or null for a folder entry.</summary>
        public string RouteId { get; }
        /// <summary>The path ID within the route, or null for a route entry.</summary>
        public string PathId { get; }

        public ToolboxMenuKey(string folderId, string routeId, string pathId)
        {
            FolderId = folderId;
            RouteId = routeId;
            PathId = pathId;
        }

        public bool Equals(ToolboxMenuKey other) => other != null
            && StringComparer.OrdinalIgnoreCase.Equals(FolderId, other.FolderId)
            && StringComparer.OrdinalIgnoreCase.Equals(RouteId, other.RouteId)
            && StringComparer.OrdinalIgnoreCase.Equals(PathId, other.PathId);
        public override bool Equals(object obj) => obj is ToolboxMenuKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(
            FolderId == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(FolderId),
            RouteId == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(RouteId),
            PathId == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(PathId));
    }

    /// <summary>An immutable selector value. Only the qualified key participates in equality.</summary>
    internal sealed class ToolboxMenuEntry : IEquatable<ToolboxMenuEntry>
    {
        /// <summary>The entry's content-qualified identity.</summary>
        public ToolboxMenuKey Key { get; }
        /// <summary>Display text captured on the owner, never read from a mutable model by WPF.</summary>
        public string Name { get; }
        /// <summary>Captured route description, or null for entries without one.</summary>
        public string Description { get; }

        public ToolboxMenuEntry(ToolboxMenuKey key, string name, string description)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Name = name;
            Description = description;
        }

        public bool Equals(ToolboxMenuEntry other) => other != null && Key.Equals(other.Key);
        public override bool Equals(object obj) => obj is ToolboxMenuEntry other && Equals(other);
        public override int GetHashCode() => Key.GetHashCode();
    }

    /// <summary>The menu's current owner-side transition, separate from route request generation.</summary>
    internal enum ToolboxMenuStatus
    {
        Ready,
        Confirming,
        DiscoveringRoutes,
        LoadingRoute,
        Failed,
        Stopped,
    }

    /// <summary>A captured selection command. List namespace epochs, not UI revisions, govern freshness.</summary>
    internal sealed class ToolboxMenuCommand
    {
        /// <summary>The producing session.</summary>
        public Guid SessionId { get; }
        /// <summary>The accepted folder namespace epoch.</summary>
        public long FolderContext { get; }
        /// <summary>The committed route lifetime epoch.</summary>
        public long RouteContext { get; }
        /// <summary>The selected key, resolved against owner-side models.</summary>
        public ToolboxMenuKey Key { get; }

        public ToolboxMenuCommand(Guid sessionId, long folderContext, long routeContext, ToolboxMenuKey key)
        {
            SessionId = sessionId;
            FolderContext = folderContext;
            RouteContext = routeContext;
            Key = key;
        }
    }

    /// <summary>One coherent immutable publication captured by the session on the game owner.</summary>
    internal sealed class ToolboxMenuSnapshot : EventArgs
    {
        /// <summary>The owner session; a new session uses a new bridge.</summary>
        public Guid SessionId { get; }
        /// <summary>Monotonic presentation revision, independent of model versions and route generation.</summary>
        public long Revision { get; }
        /// <summary>The accepted folder namespace epoch.</summary>
        public long FolderContext { get; }
        /// <summary>The route lifetime epoch.</summary>
        public long RouteContext { get; }
        /// <summary>Available folders.</summary>
        public ImmutableArray<ToolboxMenuEntry> Folders { get; }
        /// <summary>Routes belonging to the accepted folder.</summary>
        public ImmutableArray<ToolboxMenuEntry> Routes { get; }
        /// <summary>Persisted paths belonging to the committed route.</summary>
        public ImmutableArray<ToolboxMenuEntry> Paths { get; }
        /// <summary>The accepted folder.</summary>
        public ToolboxMenuKey AcceptedFolder { get; }
        /// <summary>Folder intent awaiting confirmation, if any.</summary>
        public ToolboxMenuKey PendingFolder { get; }
        /// <summary>Route intent awaiting confirmation or load, if any.</summary>
        public ToolboxMenuKey PendingRoute { get; }
        /// <summary>The committed route, never an uncommitted load.</summary>
        public ToolboxMenuKey CommittedRoute { get; }
        /// <summary>The current editor path, which may not yet be persisted.</summary>
        public ToolboxMenuEntry SelectedPath { get; }
        /// <summary>The current transition.</summary>
        public ToolboxMenuStatus Status { get; }
        /// <summary>A load/discovery failure message, cleared by the next accepted transition.</summary>
        public string Error { get; }
        /// <summary>False while modal UI or session shutdown prevents menu commands.</summary>
        public bool Enabled { get; }
        /// <summary>Whether route-scoped path commands are available.</summary>
        public bool CanEditPath => Enabled && CommittedRoute != null && Status != ToolboxMenuStatus.Confirming;

        public ToolboxMenuSnapshot(Guid sessionId, long revision, long folderContext, long routeContext,
            ImmutableArray<ToolboxMenuEntry> folders, ImmutableArray<ToolboxMenuEntry> routes, ImmutableArray<ToolboxMenuEntry> paths,
            ToolboxMenuKey acceptedFolder, ToolboxMenuKey pendingFolder, ToolboxMenuKey pendingRoute,
            ToolboxMenuKey committedRoute, ToolboxMenuEntry selectedPath, ToolboxMenuStatus status, string error, bool enabled)
        {
            SessionId = sessionId;
            Revision = revision;
            FolderContext = folderContext;
            RouteContext = routeContext;
            Folders = folders;
            Routes = routes;
            Paths = paths;
            AcceptedFolder = acceptedFolder;
            PendingFolder = pendingFolder;
            PendingRoute = pendingRoute;
            CommittedRoute = committedRoute;
            SelectedPath = selectedPath;
            Status = status;
            Error = error;
            Enabled = enabled;
        }

        /// <summary>Captures command scope without coupling intent to unrelated presentation changes.</summary>
        public ToolboxMenuCommand Command(ToolboxMenuKey key) => new ToolboxMenuCommand(SessionId, FolderContext, RouteContext, key);
    }
}
