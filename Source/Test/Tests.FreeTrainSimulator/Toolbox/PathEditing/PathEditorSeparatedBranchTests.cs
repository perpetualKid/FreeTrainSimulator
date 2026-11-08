using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Common;
using Tests.FreeTrainSimulator.Graphics.MapView;
using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Toolbox.PathEditing
{
    [TestClass]
    [DoNotParallelize]
    public class PathEditorSeparatedBranchTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(-1, false)]
        [DataRow(12, false)]
        [DataRow(8, true)]
        [DataRow(5, false)]
        [DataRow(11, false)]
        public async Task WhenTwoBranchEditorRefreshesWhileIdleThenCapabilitiesCacheAndSnapshotAreReused(int selectedTrack, bool canRemove)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            AuthorBranch(editor, 8, 11, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            // Warm a valid node before testing an invalid/terminal selection, too.
            _ = editor.CanRemovePassingBranch(SeparatedPassingFixture.Index(source, 3));
            editor.SelectAuthoredNode(selectedTrack < 0 ? -1 : SeparatedPassingFixture.Index(source, selectedTrack));
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, action => action(),
                () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };
            bridge.RefreshSnapshot();
            TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
            FieldInfo cacheField = typeof(PathEditor).GetField("passingBranchCapabilities", BindingFlags.Instance | BindingFlags.NonPublic);
            object cache = cacheField.GetValue(editor);
            Assert.AreEqual((false, canRemove), (snapshot.CanBeginPassingBranch, snapshot.CanRemovePassingBranch));

            for (int refresh = 0; refresh < 20; refresh++)
                bridge.RefreshSnapshot();

            Assert.AreEqual((true, true, true), (ReferenceEquals(snapshot, bridge.CaptureTrainPathSnapshot()),
                ReferenceEquals(cache, cacheField.GetValue(editor)), ReferenceEquals(source, editor.TryCaptureCurrentPathModel())));
        }

        [TestMethod]
        public async Task WhenBranchIsRemovedAndUndoneAndRedoneThenCapabilitiesFollowExactModelIdentity()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            AuthorBranch(editor, 8, 11, true);
            PathModel both = editor.TryCaptureCurrentPathModel();
            int split = SeparatedPassingFixture.Index(both, 8);
            Assert.AreEqual((false, true), (editor.CanBeginPassingBranch(split), editor.CanRemovePassingBranch(split)));
            FieldInfo modelField = typeof(PathEditor).GetField("passingBranchCapabilityModel", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo cacheField = typeof(PathEditor).GetField("passingBranchCapabilities", BindingFlags.Instance | BindingFlags.NonPublic);
            object bothCache = cacheField.GetValue(editor);

            Assert.IsTrue(editor.RemovePassingBranchCommand(split).Success);
            PathModel one = editor.TryCaptureCurrentPathModel();
            split = SeparatedPassingFixture.Index(one, 8);
            Assert.AreEqual((true, false, true, false), (editor.CanBeginPassingBranch(split), editor.CanRemovePassingBranch(split),
                ReferenceEquals(one, modelField.GetValue(editor)), ReferenceEquals(bothCache, cacheField.GetValue(editor))));
            object oneCache = cacheField.GetValue(editor);
            Assert.IsTrue(editor.Undo());
            PathModel undone = editor.TryCaptureCurrentPathModel();
            split = SeparatedPassingFixture.Index(undone, 8);
            Assert.AreEqual((false, true, true, false), (editor.CanBeginPassingBranch(split), editor.CanRemovePassingBranch(split),
                ReferenceEquals(undone, modelField.GetValue(editor)), ReferenceEquals(oneCache, cacheField.GetValue(editor))));
            Assert.IsTrue(editor.Redo());
            PathModel redone = editor.TryCaptureCurrentPathModel();
            split = SeparatedPassingFixture.Index(redone, 8);

            Assert.AreEqual((true, false, true), (editor.CanBeginPassingBranch(split), editor.CanRemovePassingBranch(split),
                ReferenceEquals(redone, modelField.GetValue(editor))));
        }

        [TestMethod]
        public async Task WhenPendingBranchInteractionUsesWarmCacheThenLiveGatesRefuseCommandsAndCancellationRestoresCapabilities()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            int first = SeparatedPassingFixture.Index(source, 3);
            int second = SeparatedPassingFixture.Index(source, 8);
            Assert.AreEqual((true, true), (editor.CanBeginPassingBranch(second), editor.CanRemovePassingBranch(first)));
            FieldInfo cacheField = typeof(PathEditor).GetField("passingBranchCapabilities", BindingFlags.Instance | BindingFlags.NonPublic);
            object cache = cacheField.GetValue(editor);
            Assert.IsTrue(editor.BeginPassingBranchCommand(second).Success);
            Assert.AreEqual((false, false, false, false), (editor.CanBeginPassingBranch(second), editor.CanRemovePassingBranch(first),
                editor.BeginPassingBranchCommand(second).Success, editor.RemovePassingBranchCommand(first).Success));

            Assert.IsTrue(editor.CancelPassingBranchCommand().Success);

            Assert.AreEqual((true, true, true, true), (editor.CanBeginPassingBranch(second), editor.CanRemovePassingBranch(first),
                ReferenceEquals(cache, cacheField.GetValue(editor)), ReferenceEquals(source, editor.TryCaptureCurrentPathModel())));
        }

        [TestMethod]
        public async Task WhenPlacementUsesWarmCacheThenLiveGatesRefuseCommandsAndCancellationRestoresCapabilities()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            int first = SeparatedPassingFixture.Index(source, 3);
            int second = SeparatedPassingFixture.Index(source, 8);
            Assert.AreEqual((true, true), (editor.CanBeginPassingBranch(second), editor.CanRemovePassingBranch(first)));
            Assert.IsTrue(editor.BeginMoveNodeCommand(SeparatedPassingFixture.Index(source, 5)).Success);
            Assert.AreEqual((false, false, false, false), (editor.CanBeginPassingBranch(second), editor.CanRemovePassingBranch(first),
                editor.BeginPassingBranchCommand(second).Success, editor.RemovePassingBranchCommand(first).Success));

            Assert.IsTrue(editor.CancelMoveNodeCommand().Success);

            Assert.AreEqual((true, true, true), (editor.CanBeginPassingBranch(second), editor.CanRemovePassingBranch(first),
                ReferenceEquals(source, editor.TryCaptureCurrentPathModel())));
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        [DataRow(false, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, false, true)]
        [DataRow(true, true, true)]
        public async Task WhenTwoBranchesAreAuthoredThenBothOrdersAndWorkflowsPreserveOwnersAndHistory(bool firstGuided, bool secondGuided, bool reverseOrder)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            PathModel source = editor.TryCaptureCurrentPathModel();
            int firstSplit = reverseOrder ? 8 : 3;
            int firstRejoin = reverseOrder ? 11 : 6;
            int secondSplit = reverseOrder ? 3 : 8;
            int secondRejoin = reverseOrder ? 6 : 11;
            AuthorBranch(editor, firstSplit, firstRejoin, firstGuided);
            PathModel first = editor.TryCaptureCurrentPathModel();
            PathRouteResolution firstResolution = PathRouteResolver.Resolve(first, fixture.World, TestContext.CancellationToken);

            AuthorBranch(editor, secondSplit, secondRejoin, secondGuided);
            PathModel both = editor.TryCaptureCurrentPathModel();
            PathRouteResolution bothResolution = PathRouteResolver.Resolve(both, fixture.World, TestContext.CancellationToken);
            Assert.IsTrue(bothResolution.IsValid);
            Assert.HasCount(2, bothResolution.PassingRoutes);
            Assert.IsTrue(PathRouteResolver.Resolve(source, fixture.World, TestContext.CancellationToken).MainRoute.HasSamePhysicalTraversal(bothResolution.MainRoute));
            Assert.IsTrue(firstResolution.PassingRoutes.Single().HasSamePhysicalTraversal(Owner(bothResolution, both, firstSplit)));
            Assert.IsTrue(Owner(bothResolution, both, secondSplit).Spans.SelectMany(span => span.TrackVectorNodeIndexes).Contains(secondSplit == 3 ? 5 : 10));
            Assert.AreEqual(Owner(bothResolution, both, secondSplit).StartNodeIndex, editor.SelectedPathNodeIndex);
            Assert.IsFalse(editor.CanBeginPassingBranch(SeparatedPassingFixture.Index(both, firstSplit)));
            Assert.IsTrue(editor.Undo());
            Assert.AreSequenceEqual(first.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsTrue(editor.Redo());
            Assert.AreSequenceEqual(both.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenSecondGuidedBranchIsCanceledThenFirstOwnerAndUndoRedoRemainUntouched(bool preview)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, false);
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathModel[] undo = History(editor, "undoHistory");
            PathModel[] redo = History(editor, "redoHistory");
            BeginGuided(editor, 8, 11, preview);

            PathEditorCommandResult result = editor.CancelPassingBranchCommand();

            Assert.AreEqual((true, true, PassingBranchAuthoringPhase.Idle), (result.Success, ReferenceEquals(source, editor.TryCaptureCurrentPathModel()), editor.PassingBranchPhase));
            Assert.AreSequenceEqual(undo, History(editor, "undoHistory"));
            Assert.AreSequenceEqual(redo, History(editor, "redoHistory"));
        }

        [TestMethod]
        [DataRow(3)]
        [DataRow(6)]
        [DataRow(5)]
        public async Task WhenNewStartCannotHaveASeparatedRejoinThenCapabilityAndCommandRefuseWithoutMutation(int startTrack)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathModel[] undo = History(editor, "undoHistory");
            int start = SeparatedPassingFixture.Index(source, startTrack);

            bool capability = editor.CanBeginPassingBranch(start);
            PathEditorCommandResult result = editor.BeginPassingBranchCommand(start);

            Assert.AreEqual((false, false, true, PassingBranchAuthoringPhase.Idle), (capability, result.Success, ReferenceEquals(source, editor.TryCaptureCurrentPathModel()), editor.PassingBranchPhase));
            Assert.AreSequenceEqual(undo, History(editor, "undoHistory"));
        }

        [TestMethod]
        public async Task WhenNewOwnerHasInvalidRejoinThenQuickAndGuidedCapabilitiesAgreeAndDraftSurvives()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, false);
            PathModel source = editor.TryCaptureCurrentPathModel();
            Assert.IsTrue(editor.BeginPassingBranchCommand(SeparatedPassingFixture.Index(source, 8)).Success);
            int invalidRejoin = SeparatedPassingFixture.Index(source, 6);

            bool quick = editor.CanCompletePassingBranch(invalidRejoin);
            bool guided = editor.CanRejoinPassingBranchWithVia(invalidRejoin);
            PathEditorCommandResult result = editor.RejoinPassingBranchWithViaCommand(invalidRejoin);

            Assert.AreEqual((false, false, false, true, PassingBranchAuthoringPhase.SelectingRejoin),
                (quick, guided, result.Success, ReferenceEquals(source, editor.TryCaptureCurrentPathModel()), editor.PassingBranchPhase));
        }

        [TestMethod]
        [DataRow(3, 8)]
        [DataRow(8, 3)]
        public async Task WhenOneOwnerIsRemovedThenRemappedSelectionAndOtherOwnerArePreserved(int removedSplit, int remainingSplit)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 8, 11, true);
            AuthorBranch(editor, 3, 6, true);
            PathModel both = editor.TryCaptureCurrentPathModel();
            PathRouteResolution baseline = PathRouteResolver.Resolve(both, fixture.World, TestContext.CancellationToken);

            PathEditorCommandResult result = editor.RemovePassingBranchCommand(SeparatedPassingFixture.Index(both, removedSplit));
            PathModel remaining = editor.TryCaptureCurrentPathModel();
            PathRouteResolution resolution = PathRouteResolver.Resolve(remaining, fixture.World, TestContext.CancellationToken);

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(Owner(baseline, both, remainingSplit).HasSamePhysicalTraversal(resolution.PassingRoutes.Single()));
            Assert.AreEqual(SeparatedPassingFixture.Index(remaining, removedSplit), editor.SelectedPathNodeIndex);
            Assert.IsTrue(editor.Undo());
            Assert.AreSequenceEqual(both.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public async Task WhenPassingChoicesBelongToOtherOwnerThenExactOwnerResolverRefusesThem()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            AuthorBranch(editor, 8, 11, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathRouteResolution resolution = PathRouteResolver.Resolve(source, fixture.World, TestContext.CancellationToken);
            ResolvedPathSpan foreign = Owner(resolution, source, 3).Spans[0];

            ArgumentOutOfRangeException error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PathRouteResolver.ResolveSelectedPassingRoutes(source,
                fixture.World, PathRouteResolverOptions.Default, SeparatedPassingFixture.Index(source, 8),
                ImmutableDictionary<int, ResolvedRouteCandidate>.Empty.Add(foreign.FromNodeIndex, foreign.Candidates[0]), TestContext.CancellationToken));

            Assert.AreEqual("selectedCandidates", error.ParamName);
        }

        private static ResolvedPathRoute Owner(PathRouteResolution resolution, PathModel model, int splitTrack)
        {
            return resolution.PassingRoutes.Single(route => model.PathNodes[route.StartNodeIndex].NodeIndex == splitTrack);
        }

        private static PathModel[] History(PathEditor editor, string name)
        {
            return ((System.Collections.Generic.Stack<PathModel>)typeof(PathEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor)).ToArray();
        }

        private static void AuthorBranch(PathEditor editor, int splitTrack, int rejoinTrack, bool guided)
        {
            SeparatedPassingFixture.AuthorBranch(editor, splitTrack, rejoinTrack, guided);
        }

        private static void BeginGuided(PathEditor editor, int splitTrack, int rejoinTrack, bool preview)
        {
            PathModel source = editor.TryCaptureCurrentPathModel();
            Assert.IsTrue(editor.BeginPassingBranchCommand(SeparatedPassingFixture.Index(source, splitTrack)).Success);
            Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(SeparatedPassingFixture.Index(source, rejoinTrack)).Success);
            if (preview)
                Assert.IsTrue(editor.PreviewPassingBranchViaCommand(Via(splitTrack), false).Success);
        }

        private static PathNode Via(int splitTrack)
        {
            return SyntheticPathFixture.Node(PathNodeType.Via, splitTrack == 3 ? 5 : 10, splitTrack == 3 ? 50 : 250, 100, -1, -1);
        }

        [TestMethod]
        [DataRow(5, 8)]
        [DataRow(10, 3)]
        public async Task WhenEitherInteriorIsMovedByPointerThenBothPhysicalOwnersAndUndoArePreserved(int track, int otherSplit)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 8, 11, true);
            AuthorBranch(editor, 3, 6, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathRouteResolution before = editor.ResolveCurrent(source);
            int interior = SeparatedPassingFixture.Index(source, track);
            Assert.HasCount(3, fixture.World.SegmentSections[track].SectionSegments);
            TrackSegmentBase segment = fixture.World.SegmentSections[track].SectionSegments[1];
            PointD target = segment.Location + (segment.Vector - segment.Location) * 0.3;
            Assert.IsTrue(editor.BeginMoveNodeCommand(interior).Success);

            editor.UpdatePointerLocation(target, segment);
            Assert.IsTrue(editor.CanCommitMoveNode);
            Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
            PathEditResult result = editor.CommitMoveNode();

            Assert.IsTrue(result.Success, result.Message);
            PathModel moved = editor.TryCaptureCurrentPathModel();
            PathRouteResolution after = editor.ResolveCurrent(moved);
            Assert.IsTrue(after.IsValid);
            Assert.IsTrue(before.MainRoute.HasSamePhysicalTraversal(after.MainRoute));
            Assert.IsTrue(Owner(before, source, otherSplit).HasSamePhysicalTraversal(Owner(after, moved, otherSplit)));
            Assert.IsTrue(Owner(before, source, track == 5 ? 3 : 8).HasSamePhysicalTraversal(Owner(after, moved, track == 5 ? 3 : 8)));
            Assert.IsTrue(moved.PathNodes.Any(node => node.Location == PointD.ToWorldLocation(segment.SnapToSegment(target))));
            Assert.IsTrue(editor.Undo());
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsTrue(editor.Redo());
            Assert.AreSequenceEqual(moved.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        [DataRow(5, 8)]
        [DataRow(10, 3)]
        public async Task WhenEitherInteriorIsRemovedThenOtherPhysicalOwnerAndUndoArePreserved(int track, int otherSplit)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 3, 6, true);
            AuthorBranch(editor, 8, 11, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathRouteResolution before = editor.ResolveCurrent(source);
            int interior = SeparatedPassingFixture.Index(source, track);
            Assert.IsTrue(editor.CanRemoveViaPoint(interior));

            PathEditorCommandResult result = editor.RemoveViaPointCommand(interior);

            Assert.IsTrue(result.Success, result.Message);
            PathModel removed = editor.TryCaptureCurrentPathModel();
            PathRouteResolution after = editor.ResolveCurrent(removed);
            Assert.IsTrue(after.IsValid);
            Assert.HasCount(2, after.PassingRoutes);
            Assert.IsTrue(before.MainRoute.HasSamePhysicalTraversal(after.MainRoute));
            Assert.IsTrue(Owner(before, source, otherSplit).HasSamePhysicalTraversal(Owner(after, removed, otherSplit)));
            Assert.IsTrue(editor.Undo());
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsTrue(editor.Redo());
            Assert.AreSequenceEqual(removed.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        [DataRow(3, 0)]
        [DataRow(6, 1)]
        [DataRow(8, 1)]
        [DataRow(11, 2)]
        public async Task WhenTwoOwnerPathIsTruncatedThenOnlyCompleteOwnersSurviveAndUndoRestoresBoth(int cutoff, int count)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false);
            using PathEditor editor = new PathEditor(fixture.Content);
            Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
            AuthorBranch(editor, 8, 11, true);
            AuthorBranch(editor, 3, 6, true);
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathRouteResolution before = editor.ResolveCurrent(source);
            int index = SeparatedPassingFixture.Index(source, cutoff);
            Assert.IsTrue(editor.CanRemoveRestOfPath(index));

            PathEditorCommandResult result = editor.RemoveRestOfPathCommand(index);

            Assert.IsTrue(result.Success, result.Message);
            PathModel truncated = editor.TryCaptureCurrentPathModel();
            PathRouteResolution after = editor.ResolveCurrent(truncated);
            Assert.IsTrue(after.IsValid);
            Assert.HasCount(count, after.PassingRoutes);
            Assert.IsTrue(after.PassingRoutes.All(route => Owner(before, source, truncated.PathNodes[route.StartNodeIndex].NodeIndex).HasSamePhysicalTraversal(route)));
            Assert.AreEqual(cutoff, truncated.PathNodes[editor.SelectedAuthoredNodeIndex].NodeIndex);
            Assert.IsTrue(editor.Undo());
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsTrue(editor.Redo());
            Assert.AreSequenceEqual(truncated.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }
    }

    internal sealed class SeparatedPassingFixture : IMapSessionComposer, IXnaMapShellSession
    {
        private readonly RuntimeDataResolver previousRuntime = RuntimeDataResolver.Instance;
        private readonly TrackWorld previousWorld = TrackWorld.Instance;
        internal ToolboxContent Content { get; private set; }
        internal TrackWorld World { get; private set; }
        public IMapViewport Viewport { get; } = new ToolboxInspectionFixture.InspectionViewport();
        public IMapRenderer Renderer { get; } = new ToolboxInspectionFixture.InspectionRenderer();
        public IMapHostControl HostControl => null;
        public IXnaMapShellHost ShellHost { get; }
        IMapShellHost IMapShellSession.ShellHost => ShellHost;
        public IMapSession Compose(MapSessionRequest request) => this;

        private SeparatedPassingFixture()
        {
            // Match the journey fixture: selection needs only the redraw flag, not an initialized GPU shell.
            ContentArea redrawTarget = (ContentArea)RuntimeHelpers.GetUninitializedObject(typeof(ContentArea));
#pragma warning disable CA1816
            GC.SuppressFinalize(redrawTarget);
#pragma warning restore CA1816
            ShellHost = new XnaMapShellHost(redrawTarget);
        }

        internal static async Task<SeparatedPassingFixture> CreateAsync()
        {
            TrackDatabase database = SyntheticPathFixture.CreateTwoSeparatedBranchesDatabase();
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackSectionModel sections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection { SectionIndex = 1, Length = 100, Gauge = 1.435f }),
            };
            return await CreateAsync(TrackWorld.Create(new TrackModel { TrackDatabase = database }, sections), sections).ConfigureAwait(false);
        }

        internal static async Task<SeparatedPassingFixture> CreateAsync(TrackWorld world, TrackSectionModel sections)
        {
            SeparatedPassingFixture fixture = new SeparatedPassingFixture { World = world };
            try
            {
                RuntimeDataResolver runtime = new SeparatedRuntime(fixture.World, sections);
                GameService<TrackWorld>.Set(null, fixture.World);
                GameService<RuntimeDataResolver>.Set(null, runtime);
                fixture.Content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime), fixture, null, null));
                await fixture.Content.InitializeAsync(_ => Task.CompletedTask).ConfigureAwait(false);
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        internal static PathModel CreateMain()
        {
            PathModel source = SyntheticPathFixture.CreateTwoSeparatedBranchesSource();
            source = source with { PathNodes = source.PathNodes.Take(6).Select(node => node with { NextSidingNode = -1 }).ToImmutableArray() };
            source = PathModelEditor.InsertViaPoint(source, 1, SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, -1, -1), false).PathModel;
            source = PathModelEditor.InsertViaPoint(source, Index(source, 8), SyntheticPathFixture.Node(PathNodeType.Via, 9, 250, 0, -1, -1), false).PathModel;
            int length = source.PathNodes.Length;
            return source with { PathNodes = source.PathNodes.Reverse().Select(node => node with
            {
                NextMainNode = node.NextMainNode < 0 ? -1 : length - 1 - node.NextMainNode,
                NextSidingNode = -1,
            }).ToImmutableArray() };
        }

        internal static int Index(PathModel source, int trackNode)
        {
            return Enumerable.Range(0, source.PathNodes.Length).First(index => source.PathNodes[index].NodeIndex == trackNode);
        }

        internal static PathNode Via(int splitTrack)
        {
            return SyntheticPathFixture.Node(PathNodeType.Via, splitTrack == 3 ? 5 : 10, splitTrack == 3 ? 50 : 250, 100, -1, -1);
        }

        internal static void AuthorBranch(PathEditor editor, int splitTrack, int rejoinTrack, bool guided)
        {
            PathModel source = editor.TryCaptureCurrentPathModel();
            int split = Index(source, splitTrack);
            int rejoin = Index(source, rejoinTrack);
            Assert.IsTrue(editor.CanBeginPassingBranch(split));
            Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
            if (guided)
            {
                Assert.IsTrue(editor.CanRejoinPassingBranchWithVia(rejoin));
                PathEditorCommandResult begin = editor.RejoinPassingBranchWithViaCommand(rejoin);
                Assert.IsTrue(begin.Success, begin.Message);
                PathEditorCommandResult preview = editor.PreviewPassingBranchViaCommand(Via(splitTrack), false);
                Assert.IsTrue(preview.Success, preview.Message);
                PathEditResult commit = editor.CommitPlacement();
                Assert.IsTrue(commit.Success, commit.Message);
            }
            else
            {
                Assert.IsTrue(editor.CanCompletePassingBranch(rejoin));
                PathEditorCommandResult complete = editor.CompletePassingBranchCommand(rejoin);
                Assert.IsTrue(complete.Success, complete.Message);
            }
        }

        public void Dispose()
        {
            GameService<RuntimeDataResolver>.Set(null, previousRuntime);
            GameService<TrackWorld>.Set(null, previousWorld);
        }

        private sealed class SeparatedRuntime : RuntimeDataResolver
        {
            internal SeparatedRuntime(TrackWorld world, TrackSectionModel sections)
                : base(new RouteModel(WorldLocation.None) { Name = "Separated passing fixture" }, sections, new SignalConfigurationModel(), world, true, null)
            {
            }
        }
    }
}
