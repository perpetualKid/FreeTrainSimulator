using System;
using System.Diagnostics;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>Immutable chart publication and the ownership required for map navigation.</summary>
    internal sealed record PathChartSnapshot
    {
        /// <summary>Editor owning the committed source.</summary>
        public PathEditor Owner { get; init; }
        /// <summary>Committed model, never a tentative candidate.</summary>
        public PathModel Source { get; init; }
        /// <summary>Explicit route geometry owner.</summary>
        public TrackWorld World { get; init; }
        /// <summary>Map composition captured on the owner alongside the profile.</summary>
        public ITrackNodeInfoContext Context { get; init; }
        /// <summary>Monotonic edit/load witness, including edit-and-undo.</summary>
        public long Revision { get; init; }
        /// <summary>Load lifetime independent of the saved path identity.</summary>
        public long PathLifetime { get; init; }
        /// <summary>Profile extracted once for the committed source.</summary>
        public PathChartProfile Profile { get; init; }
        /// <summary>Whether presentation uses metric distance and elevation.</summary>
        public bool UseMetricUnits { get; init; }
        /// <summary>Stable navigation-result message translated at presentation.</summary>
        public string NavigationMessage { get; init; }

        public PathChartSnapshot(PathEditor owner, PathModel source, TrackWorld world, ITrackNodeInfoContext context, long revision,
            long pathLifetime, PathChartProfile profile, bool useMetricUnits, string navigationMessage)
        {
            Owner = owner;
            Source = source;
            World = world;
            Context = context;
            Revision = revision;
            PathLifetime = pathLifetime;
            Profile = profile;
            UseMetricUnits = useMetricUnits;
            NavigationMessage = navigationMessage;
        }

        /// <summary>Initial state before a route and path are available.</summary>
        public static PathChartSnapshot Empty { get; } = new PathChartSnapshot(null, null, null, null, 0, 0, null, true, null);
    }

    /// <summary>Owner-thread profile extraction with lock-free WPF publication and guarded map navigation.</summary>
    internal sealed class PathChartToolWindow
    {
        private readonly Func<PathEditor> editorAccessor;
        private readonly Func<ITrainPathToolingContext> toolingAccessor;
        private readonly Action<Action> gameThreadInvoker;
        private ITrackNodeInfoContext mapContext;
        private volatile PathChartSnapshot snapshot = PathChartSnapshot.Empty;
        private volatile bool active;

        internal PathChartToolWindow(Func<PathEditor> editorAccessor, Func<ITrainPathToolingContext> toolingAccessor,
            Action<Action> gameThreadInvoker)
        {
            this.editorAccessor = editorAccessor ?? throw new ArgumentNullException(nameof(editorAccessor));
            this.toolingAccessor = toolingAccessor ?? throw new ArgumentNullException(nameof(toolingAccessor));
            this.gameThreadInvoker = gameThreadInvoker ?? throw new ArgumentNullException(nameof(gameThreadInvoker));
        }

        /// <summary>Only visible charts perform owner-thread source checks.</summary>
        public bool Active
        {
            get => active;
            set => active = value;
        }

        /// <summary>Reads the immutable publication without consulting editor or map state on WPF.</summary>
        internal PathChartSnapshot CaptureSnapshot() => snapshot;

        /// <summary>Invalidates old route data even while the chart is hidden.</summary>
        internal void UpdateContext(ITrackNodeInfoContext context)
        {
            if (ReferenceEquals(mapContext, context))
                return;
            mapContext = context;
            snapshot = PathChartSnapshot.Empty;
        }

        /// <summary>Called on the game owner; unchanged frames never resample geometry.</summary>
        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            PathEditor editor = editorAccessor();
            if (editor?.IsStopped == true)
                editor = null;
            PathModel model = editor?.TryCaptureCurrentPathModel();
            ITrainPathToolingContext tooling = toolingAccessor();
            TrackWorld world = tooling?.TrackWorld;
            bool metric = tooling?.UseMetricUnits ?? true;
            long revision = editor?.CompositionRevision ?? 0;
            long lifetime = editor?.PathLifetime ?? 0;
            PathChartSnapshot previous = snapshot;
            if (ReferenceEquals(previous.Owner, editor) && ReferenceEquals(previous.Source, model)
                && ReferenceEquals(previous.World, world) && previous.Revision == revision
                && previous.PathLifetime == lifetime && previous.UseMetricUnits == metric)
                return;

            PathChartProfile profile = ReferenceEquals(previous.Source, model) && ReferenceEquals(previous.World, world)
                ? previous.Profile : null;
            if (profile == null && model != null && world != null && ReferenceEquals(editor.RuntimeTrackWorld, world))
                profile = PathChartBuilder.Build(model, editor.ResolveCurrent(model), world);

            snapshot = new PathChartSnapshot(editor, model, world, mapContext, revision, lifetime, profile, metric, null);
        }

        /// <summary>Centers the current route only when the displayed publication still owns its source.</summary>
        internal void ShowOnMap(PathChartSnapshot displayed, WorldLocation location)
        {
            ArgumentNullException.ThrowIfNull(displayed);
            gameThreadInvoker(() =>
            {
                PathEditor editor = editorAccessor();
                if (displayed.Profile?.IsAvailable != true || editor == null || editor.IsStopped
                    || !ReferenceEquals(displayed.Owner, editor) || !ReferenceEquals(displayed.Source, editor.TryCaptureCurrentPathModel())
                    || displayed.Revision != editor.CompositionRevision || displayed.PathLifetime != editor.PathLifetime
                    || !ReferenceEquals(displayed.World, editor.RuntimeTrackWorld)
                    || !ReferenceEquals(displayed.World, toolingAccessor()?.TrackWorld)
                    || !ReferenceEquals(displayed.Context, mapContext) || !ReferenceEquals(displayed.World, mapContext?.TrackWorld)
                    || mapContext.Viewport == null)
                {
                    Trace.TraceWarning("Cannot navigate from the path chart: the route or path has changed.");
                    snapshot = snapshot with { NavigationMessage = "The route or path has changed. Select a position on the current chart and try again." };
                    return;
                }

                mapContext.Viewport.SetTrackingPosition(location);
                snapshot = snapshot with { NavigationMessage = "Map centered on chart position." };
            });
        }
    }
}