using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Toolbox.PathEditing;

using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace Tests.FreeTrainSimulator.Toolbox.Views
{
    [STATestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public class TrainPathToolViewTests
    {
        public TestContext TestContext { get; set; }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false, 0, "replace")]
        [DataRow(true, 0, "replace")]
        [DataRow(false, 1, "replace")]
        [DataRow(true, 1, "replace")]
        [DataRow(false, 0, "reload")]
        [DataRow(true, 0, "reload")]
        [DataRow(false, 1, "reload")]
        [DataRow(true, 1, "reload")]
        [DataRow(false, 0, "edit-undo")]
        [DataRow(true, 0, "edit-undo")]
        [DataRow(false, 1, "edit-undo")]
        [DataRow(true, 1, "edit-undo")]
        [DataRow(false, 0, "placement")]
        [DataRow(true, 0, "placement")]
        [DataRow(false, 1, "placement")]
        [DataRow(true, 1, "placement")]
        public async Task WhenWpfNodeMenuOwnerChangesThenCapturedActionCannotEditCurrentPath(bool contextMenu, int changeStage, string change)
        {
            using (SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true))
            {
                using (PathEditor editor = new PathEditor(fixture.Content))
                {
                    using (PathEditor replacement = new PathEditor(fixture.Content))
                    {
                        PathModel source = SeparatedPassingFixture.CreateMain();
                        Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
                        Assert.IsTrue(replacement.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
                        PathEditor current = editor;
                        int nodeIndex = SeparatedPassingFixture.Index(source, 4);
                        editor.SelectAuthoredNode(nodeIndex);
                        replacement.SelectAuthoredNode(nodeIndex);
                        Queue<Action> commands = new Queue<Action>();
                        TrainPathToolWindow bridge = new TrainPathToolWindow(() => current, () => null, commands.Enqueue, () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };
                        bridge.RefreshSnapshot();
                        using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                        {
                            using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex))
                            {
                                viewModel.Start();
                                TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                                Window window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                                try
                                {
                                    window.Show();
                                    SettleLayout(window);
                                    DrainCommands(commands);
                                    MenuItem action = OpenNodeMenu(view, window, viewModel, contextMenu).OfType<MenuItem>()
                                        .Single(item => Equals(item.Header, global::FreeTrainSimulator.Toolbox.MainWindow.GetMapContextMenuCaption(
                                            new MapContextMenuItem(MapContextMenuAction.RemoveViaPoint, nodeIndex))));
                                    DrainCommands(commands);
                                    Action[] stages = { () => { }, () => { } };
                                    stages[changeStage] = change switch
                                    {
                                        "replace" => () => current = replacement,
                                        "reload" => () => Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult()),
                                        "edit-undo" => () =>
                                        {
                                            Assert.IsTrue(editor.SetMetadataCommand("Changed", source.Start, source.End, false).Success);
                                            Assert.IsTrue(editor.Undo());
                                        }
                                        ,
                                        "placement" => () => Assert.IsTrue(editor.BeginMoveNodeCommand(nodeIndex).Success),
                                        _ => throw new ArgumentOutOfRangeException(nameof(change)),
                                    };
                                    stages[0]();
                                    action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                    stages[1]();
                                    PathModel expected = current.TryCaptureCurrentPathModel();
                                    PathEditorPlacementMode expectedPlacement = current.PlacementMode;
                                    commands.Dequeue()();

                                    Assert.AreSame(expected, current.TryCaptureCurrentPathModel());
                                    Assert.AreEqual(expectedPlacement, current.PlacementMode);
                                    Assert.HasCount(0, commands, "Menu dispatch must not queue a second mutation callback.");
                                }
                                finally
                                {
                                    window.Close();
                                }
                            }
                        }
                    }
                }
            }
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false, 0, "restart")]
        [DataRow(true, 0, "restart")]
        [DataRow(false, 1, "restart")]
        [DataRow(true, 1, "restart")]
        [DataRow(false, 0, "via")]
        [DataRow(true, 0, "via")]
        [DataRow(false, 1, "via")]
        [DataRow(true, 1, "via")]
        [DataRow(false, 0, "phase")]
        [DataRow(true, 0, "phase")]
        [DataRow(false, 1, "phase")]
        [DataRow(true, 1, "phase")]
        public async Task WhenWpfGuidedMenuInteractionChangesThenStaleCancelCannotAffectCurrentDraft(bool contextMenu, int changeStage, string change)
        {
            using (SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true))
            {
                using (PathEditor editor = new PathEditor(fixture.Content))
                {
                    PathModel source = SeparatedPassingFixture.CreateMain();
                    Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
                    int split = SeparatedPassingFixture.Index(source, 3);
                    int rejoin = SeparatedPassingFixture.Index(source, 6);
                    BeginGuidedMenuDraft(editor, split, rejoin);
                    WithWpfNodeMenu(editor, rejoin, MapContextMenuAction.CancelPassingBranch, contextMenu, (commands, action, _) =>
                    {
                        Action[] stages = { () => { }, () => { } };
                        stages[changeStage] = change switch
                        {
                            "restart" => () =>
                            {
                                Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                                BeginGuidedMenuDraft(editor, split, rejoin);
                            }
                            ,
                            "via" => () => Assert.IsTrue(editor.PreviewPassingBranchViaCommand(new PathNode(new WorldLocation(SeparatedPassingFixture.Via(3).Location.Tile, new Vector3(60, 0, 100)))
                            { NodeType = PathNodeType.Via, NodeIndex = 5, NextMainNode = -1, NextSidingNode = -1 }, false).Success),
                            "phase" => () =>
                            {
                                Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                                Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
                            }
                            ,
                            _ => throw new ArgumentOutOfRangeException(nameof(change)),
                        };
                        stages[0]();
                        action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        stages[1]();
                        PathModel expected = editor.TryCaptureCurrentPathModel();
                        (long, PassingBranchAuthoringPhase, PathEditorPlacementMode, bool) expectedInteraction = (editor.PassingBranchInteractionRevision, editor.PassingBranchPhase, editor.PlacementMode, editor.CanCommitPlacement);
                        commands.Dequeue()();

                        Assert.AreEqual(expectedInteraction,
                            (editor.PassingBranchInteractionRevision, editor.PassingBranchPhase, editor.PlacementMode, editor.CanCommitPlacement));
                        Assert.AreSame(expected, editor.TryCaptureCurrentPathModel());
                        Assert.HasCount(0, commands);
                    });
                }
            }
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenOnlyWpfNodeSelectionChangesThenMenuStillEditsCapturedNode(bool contextMenu)
        {
            using (SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true))
            {
                using (PathEditor editor = new PathEditor(fixture.Content))
                {
                    PathModel source = SeparatedPassingFixture.CreateMain();
                    Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
                    WithWpfNodeMenu(editor, SeparatedPassingFixture.Index(source, 4), MapContextMenuAction.RemoveViaPoint, contextMenu, (commands, action, viewModel) =>
                    {
                        viewModel.UserSelectNode(viewModel.Nodes.Single(node => node.Index == SeparatedPassingFixture.Index(source, 9)));
                        commands.Dequeue()();
                        action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        commands.Dequeue()();

                        Assert.AreEqual((false, true), (editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NodeIndex == 4),
                        editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NodeIndex == 9)));
                        Assert.HasCount(0, commands);
                    });
                }
            }
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenWpfGuidedMenuOwnerRemainsCurrentThenCapturedConfirmationCommitsDraft(bool contextMenu)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            int rejoin = SeparatedPassingFixture.Index(source, 6);
            BeginGuidedMenuDraft(editor, SeparatedPassingFixture.Index(source, 3), rejoin);
            WithWpfNodeMenu(editor, rejoin, MapContextMenuAction.ConfirmPassingBranchVia, contextMenu, (commands, action, _) =>
            {
                action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                commands.Dequeue()();

                Assert.AreEqual((PassingBranchAuthoringPhase.Idle, 13),
                    (editor.PassingBranchPhase, editor.TryCaptureCurrentPathModel().PathNodes.Length));
                Assert.HasCount(0, commands);
            });
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        public async Task WhenNodeMenuIsCapturedThenCollectionOwnerIsRecheckedBeforePublication()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            int accesses = 0;
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => { accesses++; return editor; }, () => null, _ => { },
                () => { }, () => { }, _ => { }, () => { }, () => { });

            PathEditorMenuRequest request = bridge.CaptureNodeMenuRequest(SeparatedPassingFixture.Index(source, 4));

            Assert.AreEqual((2, true, true), (accesses, request.IsCurrent(editor), request.Items.Any(item => item.Action == MapContextMenuAction.RemoveViaPoint)));
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [DataRow("reload")]
        [DataRow("edit-undo")]
        [DataRow("placement")]
        public async Task WhenOwnershipChangesBeforeNodeMenuEntriesAreAttachedThenRequestIsRejected(string change)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            int nodeIndex = SeparatedPassingFixture.Index(source, 4);
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, _ => { },
                () => { }, () => { }, _ => { }, () => { }, () => { });
            PathEditorMenuRequest ownership = PathEditorMenuRequest.Capture(editor);
            ImmutableArray<MapContextMenuItem> entries = bridge.GetNodeActions(nodeIndex);
            Action changeOwnership = change switch
            {
                "reload" => () => Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult()),
                "edit-undo" => () =>
                {
                    Assert.IsTrue(editor.SetMetadataCommand("Changed", source.Start, source.End, false).Success);
                    Assert.IsTrue(editor.Undo());
                },
                "placement" => () => Assert.IsTrue(editor.BeginMoveNodeCommand(nodeIndex).Success),
                _ => throw new ArgumentOutOfRangeException(nameof(change)),
            };

            changeOwnership();
            PathEditorMenuRequest request = ownership.WithItems(entries, editor);

            Assert.AreSame(PathEditorMenuRequest.Empty, request);
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [DataRow("restart")]
        [DataRow("via")]
        [DataRow("phase")]
        public async Task WhenDraftChangesBeforeNodeMenuEntriesAreAttachedThenRequestIsRejected(string change)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            int split = SeparatedPassingFixture.Index(source, 3);
            int rejoin = SeparatedPassingFixture.Index(source, 6);
            BeginGuidedMenuDraft(editor, split, rejoin);
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, _ => { },
                () => { }, () => { }, _ => { }, () => { }, () => { });
            PathEditorMenuRequest ownership = PathEditorMenuRequest.Capture(editor);
            ImmutableArray<MapContextMenuItem> entries = bridge.GetNodeActions(rejoin);
            Action changeDraft = change switch
            {
                "restart" => () =>
                {
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    BeginGuidedMenuDraft(editor, split, rejoin);
                },
                "via" => () => Assert.IsTrue(editor.PreviewPassingBranchViaCommand(new PathNode(
                    new WorldLocation(SeparatedPassingFixture.Via(3).Location.Tile, new Vector3(60, 0, 100)))
                    { NodeType = PathNodeType.Via, NodeIndex = 5, NextMainNode = -1, NextSidingNode = -1 }, false).Success),
                "phase" => () =>
                {
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
                },
                _ => throw new ArgumentOutOfRangeException(nameof(change)),
            };

            changeDraft();
            PathEditorMenuRequest request = ownership.WithItems(entries, editor);

            Assert.AreSame(PathEditorMenuRequest.Empty, request);
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        public async Task WhenOnlySelectionChangesBeforeEntriesAreAttachedThenFrozenRequestRemainsUsable()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, _ => { },
                () => { }, () => { }, _ => { }, () => { }, () => { });
            PathEditorMenuRequest ownership = PathEditorMenuRequest.Capture(editor);
            ImmutableArray<MapContextMenuItem> entries = bridge.GetNodeActions(SeparatedPassingFixture.Index(source, 4));

            editor.SelectAuthoredNode(SeparatedPassingFixture.Index(source, 9));
            PathEditorMenuRequest request = ownership.WithItems(entries, editor);

            Assert.AreEqual((true, true, true), (ownership.Items.IsEmpty, request.Items == entries, request.IsCurrent(editor)));
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenCurrentEditorChangesBeforeNodeMenuPublicationThenEntriesAreRejected(bool unavailable)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            using PathEditor replacement = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            Assert.IsTrue(replacement.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            int accesses = 0;
            PathEditor next = unavailable ? null : replacement;
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => ++accesses == 1 ? editor : next, () => null, _ => { },
                () => { }, () => { }, _ => { }, () => { }, () => { });

            PathEditorMenuRequest request = bridge.CaptureNodeMenuRequest(SeparatedPassingFixture.Index(source, 4));

            Assert.AreSame(PathEditorMenuRequest.Empty, request);
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false, "reload")]
        [DataRow(true, "reload")]
        [DataRow(false, "restart")]
        [DataRow(true, "restart")]
        public async Task WhenOwnershipChangesDuringWpfMenuConstructionThenNoActionIsPublished(bool contextMenu, string change)
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            int split = SeparatedPassingFixture.Index(source, 3);
            int rejoin = SeparatedPassingFixture.Index(source, 6);
            Dictionary<string, Action> prepare = new Dictionary<string, Action>
            {
                ["reload"] = () => { },
                ["restart"] = () => BeginGuidedMenuDraft(editor, split, rejoin),
            };
            prepare[change]();
            editor.SelectAuthoredNode(rejoin);
            Action changeOwnership = change switch
            {
                "reload" => () => Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult()),
                "restart" => () =>
                {
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    BeginGuidedMenuDraft(editor, split, rejoin);
                },
                _ => throw new ArgumentOutOfRangeException(nameof(change)),
            };
            int accesses = 0;
            bool constructingMenu = false;
            Queue<Action> commands = new Queue<Action>();
            TrainPathToolWindow bridge = new TrainPathToolWindow(() =>
            {
                if (constructingMenu && ++accesses == 2)
                    changeOwnership();
                return editor;
            }, () => null, commands.Enqueue, () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };
            bridge.RefreshSnapshot();
            using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex);
            viewModel.Start();
            TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
            Window window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
            try
            {
                window.Show();
                SettleLayout(window);
                DrainCommands(commands);

                // Menu layout pumps the dispatcher; exclude polling reads from the construction witness.
                viewModel.Stop();
                constructingMenu = true;
                object[] entries = OpenNodeMenu(view, window, viewModel, contextMenu).ToArray();
                constructingMenu = false;

                Assert.AreEqual((2, 0), (accesses, entries.OfType<MenuItem>().Count()));
            }
            finally
            {
                window.Close();
            }
        }

        [TestMethod]
        public void WhenNoEditorExistsThenNodeMenuCaptureAndDispatchAreEmptyAndSafe()
        {
            Queue<Action> commands = new Queue<Action>();
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, commands.Enqueue,
                () => { }, () => { }, _ => { }, () => { }, () => { });
            PathEditorMenuRequest request = bridge.CaptureNodeMenuRequest(0);

            bridge.ExecuteNodeAction(request, new MapContextMenuItem(MapContextMenuAction.RemoveViaPoint, 0));
            commands.Dequeue()();

            Assert.AreEqual((true, false, true, 0), (request.Items.IsEmpty, request.IsCurrent(null), bridge.GetNodeActions(0).IsEmpty, commands.Count));
        }

        [STATestMethod(UseSTASynchronizationContext = true)]
        public async Task WhenEntryDoesNotBelongToCapturedNodeMenuThenCurrentOwnerIsNotMutated()
        {
            using SeparatedPassingFixture fixture = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(true);
            using PathEditor editor = new PathEditor(fixture.Content);
            PathModel source = SeparatedPassingFixture.CreateMain();
            Assert.IsTrue(editor.InitializePathAsync(source, TestContext.CancellationToken).GetAwaiter().GetResult());
            Queue<Action> commands = new Queue<Action>();
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, commands.Enqueue,
                () => { }, () => { }, _ => { }, () => { }, () => { });
            PathEditorMenuRequest request = bridge.CaptureNodeMenuRequest(SeparatedPassingFixture.Index(source, 4));
            PathModel expected = editor.TryCaptureCurrentPathModel();

            bridge.ExecuteNodeAction(request, new MapContextMenuItem(MapContextMenuAction.RemoveViaPoint, SeparatedPassingFixture.Index(source, 9)));
            commands.Dequeue()();

            Assert.AreSame(expected, editor.TryCaptureCurrentPathModel());
        }

        private static void BeginGuidedMenuDraft(PathEditor editor, int split, int rejoin)
        {
            Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
            Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(rejoin).Success);
            Assert.IsTrue(editor.PreviewPassingBranchViaCommand(SeparatedPassingFixture.Via(3), false).Success);
        }

        private static void WithWpfNodeMenu(PathEditor editor, int nodeIndex, MapContextMenuAction actionKind, bool contextMenu,
            Action<Queue<Action>, MenuItem, TrainPathToolWindowViewModel> verify)
        {
            editor.SelectAuthoredNode(nodeIndex);
            Queue<Action> commands = new Queue<Action>();
            TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, commands.Enqueue,
                () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };
            bridge.RefreshSnapshot();
            using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex);
            viewModel.Start();
            TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
            Window window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
            try
            {
                window.Show();
                SettleLayout(window);
                DrainCommands(commands);
                MenuItem action = OpenNodeMenu(view, window, viewModel, contextMenu).OfType<MenuItem>()
                    .Single(item => Equals(item.Header, global::FreeTrainSimulator.Toolbox.MainWindow.GetMapContextMenuCaption(new MapContextMenuItem(actionKind, nodeIndex))));
                DrainCommands(commands);
                verify(commands, action, viewModel);
            }
            finally
            {
                window.Close();
            }
        }

        private static object[] OpenNodeMenu(TrainPathToolView view, Window window, TrainPathToolWindowViewModel viewModel, bool contextMenu)
        {
            if (!contextMenu)
            {
                MenuItem menu = Descendants(view).OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == "Node actions");
                menu.IsSubmenuOpen = true;
                SettleLayout(window);
                return menu.Items.Cast<object>().ToArray();
            }

            ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Nodes));
            list.ScrollIntoView(viewModel.SelectedNode);
            SettleLayout(window);
            ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(viewModel.SelectedNode);
            ContextMenuEventArgs opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { row, true }, null);
            row.RaiseEvent(opening);
            return list.ContextMenu.Items.Cast<object>().ToArray();
        }

        private static void DrainCommands(Queue<Action> commands)
        {
            while (commands.TryDequeue(out Action command))
                command();
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow((int)PassingBranchAuthoringPhase.SelectingRejoin, "Rejoin with one passing via")]
        [DataRow((int)PassingBranchAuthoringPhase.ConfirmingVia, "Confirm passing via")]
        [DataRow((int)PassingBranchAuthoringPhase.PlacingVia, "Cancel")]
        public void WhenCurrentGuidedActionIsInvokedThenWpfCommandReachesBridge(int phase, string actionName)
        {
            Window window = null;
            try
            {
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, commands.Enqueue,
                    () => { }, () => { }, _ => { }, () => { }, () => { });
                typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge,
                    CreateGuidedSnapshot((PassingBranchAuthoringPhase)phase, true));
                using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex);
                viewModel.Start();
                TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);
                Button button = Descendants(view).OfType<Button>().Single(control => control.IsVisible && Equals(control.Content, actionName));
                Dictionary<string, ICommand> expected = new Dictionary<string, ICommand>
                {
                    ["Rejoin with one passing via"] = viewModel.RejoinPassingBranchWithViaCommand,
                    ["Confirm passing via"] = viewModel.CommitPlacementCommand,
                    ["Cancel"] = viewModel.CancelPassingBranchCommand,
                };
                Assert.AreSame(expected[actionName], button.Command);
                Assert.IsTrue(button.IsEnabled);
                Assert.IsFalse(viewModel.IsPlacementPanelVisible);
                Assert.IsFalse(viewModel.IsBuildingPanelVisible);

                ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
                SettleLayout(window);

                Assert.HasCount(1, commands);
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenGuidedViaHasNoPreviewThenWpfConfirmationIsDisabledUntilAuthoritativePreviewArrives()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { },
                    () => { }, () => { }, _ => { }, () => { }, () => { });
                FieldInfo snapshotField = typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                snapshotField.SetValue(bridge, CreateGuidedSnapshot(PassingBranchAuthoringPhase.PlacingVia, false));
                using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex);
                viewModel.Start();
                TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);
                Button button = Descendants(view).OfType<Button>().Single(control => control.IsVisible && Equals(control.Content, "Confirm passing via"));
                Assert.IsFalse(button.IsEnabled);

                snapshotField.SetValue(bridge, CreateGuidedSnapshot(PassingBranchAuthoringPhase.ConfirmingVia, true));
                viewModel.Start();
                SettleLayout(window);

                Assert.IsTrue(button.IsEnabled);
            }
            finally
            {
                window?.Close();
            }
        }

        private static TrainPathSnapshot CreateGuidedSnapshot(PassingBranchAuthoringPhase phase, bool canConfirm)
        {
            return TrainPathSnapshot.Empty with
            {
                PassingBranchPhase = phase,
                PlacementMode = PathEditorPlacementMode.PassingBranchVia,
                Nodes = [new TrainPathNodeRow(2, PathNodeType.Via, true, 1, 3, -1, null, null)],
                SelectedNodeIndex = 2,
                CanRejoinPassingBranchWithVia = true,
                CanCancelPassingBranch = true,
                CanCancelPathInteraction = true,
                CanCommitPlacement = canConfirm,
            };
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(0, false)]
        [DataRow(1, false)]
        [DataRow(2, false)]
        [DataRow(3, false)]
        [DataRow(6, false)]
        [DataRow(2, true)]
        public void WhenSavedCheckboxesAreSelectedThenCombineMenuRequiresExactlyTwoLoadedBaselinesWithoutAnActivePath(int count, bool loading)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, action => action(),
                    () => { }, () => { }, _ => Assert.Fail("Checkbox selection must not load an editor path."),
                    () => Assert.Fail("Checkbox selection must not unload an editor path."), () => { }, null,
                    (_, _) => Task.CompletedTask, null, _ => Task.CompletedTask) { Active = true };
                bridge.UpdateComparisons(Enumerable.Range(0, 6).Select(index => new TrainPathComparisonRow(index.ToString(System.Globalization.CultureInfo.InvariantCulture), "Saved " + index, 1,
                    index < count, index < count && loading, null, System.Drawing.Color.Empty)).ToImmutableArray());
                bridge.RefreshSnapshot();
                TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                bridge.RefreshSnapshot();
                Assert.AreSame(snapshot, bridge.CaptureTrainPathSnapshot(), "An unchanged paths-only snapshot must be reused.");
                using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler);
                viewModel.Start();
                TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);
                MenuItem combine = (MenuItem)view.FindName("CombinePathsMenuItem");
                MenuItem actions = (MenuItem)combine.Parent;
                actions.IsSubmenuOpen = true;
                SettleLayout(window);

                Assert.AreEqual(count == 2 && !loading, combine.IsEnabled);
                Assert.IsTrue(combine.IsVisible);
                Assert.AreEqual("Path actions", AutomationProperties.GetName(actions));
                Assert.AreSame(viewModel.CombineSelectedPathsCommand, combine.Command);
                Assert.IsNull(viewModel.SelectedPath);
                Assert.IsNull(view.FindName("CompositionSourcePicker"));
                Assert.IsNull(view.FindName("AppendSavedPathButton"));
                Assert.IsFalse(view.Resources.Contains("TrainPathCombineCellTemplate"));
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenExistingCheckboxesAreUsedThenCombineAndImmediateCancelDispatchFromPathActions()
        {
            Window window = null;
            try
            {
                List<bool> requests = new List<bool>();
                TrainPathComparisonRow[] rows = Enumerable.Range(0, 3).Select(index => new TrainPathComparisonRow(index.ToString(System.Globalization.CultureInfo.InvariantCulture), "Saved " + index, 1,
                    false, false, null, System.Drawing.Color.Empty)).ToArray();
                TrainPathToolWindow bridge = null;
                bridge = new TrainPathToolWindow(() => null, () => null, action => action(),
                    () => { }, () => { }, _ => Assert.Fail("Checkbox selection must not load an editor path."),
                    () => Assert.Fail("Checkbox selection must not unload an editor path."), () => { }, null,
                    (id, selected) =>
                    {
                        int index = Array.FindIndex(rows, row => row.Id == id);
                        rows[index] = rows[index] with { IsSelected = selected };
                        bridge.UpdateComparisons(rows.ToImmutableArray());
                        bridge.RefreshSnapshot();
                        return Task.CompletedTask;
                    }, null, combine =>
                    {
                        requests.Add(combine);
                        bridge.UpdateCombineState(combine);
                        bridge.RefreshSnapshot();
                        return Task.CompletedTask;
                    })
                { Active = true };
                bridge.UpdatePaths(rows.Select(row => new global::FreeTrainSimulator.Models.Content.PathModelHeader { Id = row.Id, Name = row.Name }).ToImmutableArray());
                bridge.UpdateComparisons(rows.ToImmutableArray());
                bridge.RefreshSnapshot();
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        viewModel.Start();
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Paths));
                        Assert.HasCount(3, ((GridView)list.View).Columns);
                        CheckBox[] checkboxes = Descendants(list).OfType<CheckBox>().Where(toggle => toggle.Name == "SavedPathComparisonToggle").ToArray();
                        MethodInfo click = typeof(CheckBox).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                        foreach (CheckBox checkbox in checkboxes.Take(2))
                        {
                            click.Invoke(checkbox, null);
                            viewModel.Start();
                            SettleLayout(window);
                        }
                        Assert.IsTrue(viewModel.CombineSelectedPathsCommand.CanExecute(null));
                        click.Invoke(checkboxes[2], null);
                        viewModel.Start();
                        Assert.IsFalse(viewModel.CombineSelectedPathsCommand.CanExecute(null));
                        click.Invoke(checkboxes[2], null);
                        viewModel.Start();
                        Assert.IsTrue(viewModel.CombineSelectedPathsCommand.CanExecute(null));
                        MenuItem combine = (MenuItem)view.FindName("CombinePathsMenuItem");
                        ((MenuItem)combine.Parent).IsSubmenuOpen = true;
                        SettleLayout(window);
                        ((IInvokeProvider)new MenuItemAutomationPeer(combine).GetPattern(PatternInterface.Invoke)).Invoke();
                        SettleLayout(window);
                        MenuItem cancel = (MenuItem)view.FindName("CancelPathCombineMenuItem");
                        Assert.IsTrue(viewModel.CancelPathCombineCommand.CanExecute(null));
                        ((MenuItem)cancel.Parent).IsSubmenuOpen = true;
                        SettleLayout(window);
                        ((IInvokeProvider)new MenuItemAutomationPeer(cancel).GetPattern(PatternInterface.Invoke)).Invoke();
                        SettleLayout(window);

                        Assert.AreSequenceEqual(expectedtruefalse, requests);
                        Assert.IsNull(viewModel.SelectedPath);
                        Assert.IsNull(list.SelectedItem);
                        Assert.AreEqual(2, viewModel.Paths.Count(path => path.IsCompared));
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenWholePathReverseMenuItemIsInvokedThenItDispatchesTheBoundCommandAndTracksAvailability()
        {
            Window window = null;
            try
            {
                int invocations = 0;
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => invocations++,
                    () => { }, () => { }, _ => { }, () => { }, () => { });
                FieldInfo snapshotField = typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                snapshotField.SetValue(bridge, TrainPathSnapshot.Empty with { CanReversePath = true });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex))
                    {
                        viewModel.Start();
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        MenuItem reverse = (MenuItem)view.FindName("ReversePathMenuItem");
                        MenuItem pathActions = (MenuItem)reverse.Parent;
                        bool correctMenu = ReferenceEquals(pathActions, ((MenuItem)view.FindName("ReResolvePathMenuItem")).Parent)
                            && AutomationProperties.GetName(pathActions) == "Path actions";
                        bool duplicateButton = Descendants(view).OfType<Button>().Any(button => ReferenceEquals(button.Command, viewModel.ReversePathCommand));
                        pathActions.IsSubmenuOpen = true;
                        SettleLayout(window);
                        bool enabled = reverse.IsEnabled && reverse.IsVisible && ReferenceEquals(reverse.Command, viewModel.ReversePathCommand);

                        IInvokeProvider provider = (IInvokeProvider)new MenuItemAutomationPeer(reverse).GetPattern(PatternInterface.Invoke);
                        provider.Invoke();
                        SettleLayout(window);
                        snapshotField.SetValue(bridge, TrainPathSnapshot.Empty);
                        viewModel.Start();
                        pathActions.IsSubmenuOpen = true;
                        SettleLayout(window);

                        Assert.AreEqual((true, true, false, 1, true, false), (correctMenu, enabled, duplicateButton, invocations, reverse.IsVisible, reverse.IsEnabled));
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenOnlyWaitClearIsAvailableThenInspectorDisablesPositiveEditingAndOffersClear(bool repairMode)
        {
            Window window = null;
            try
            {
                int invocations = 0;
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => invocations++,
                    () => { }, () => { }, _ => { }, () => { }, () => { });
                typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge,
                    TrainPathSnapshot.Empty with
                    {
                        Nodes = [new TrainPathNodeRow(0, global::FreeTrainSimulator.Common.PathNodeType.Via, false, 1, -1, -1, 30, null)],
                        SelectedNodeIndex = 0,
                        CanClearSelectedWaitPoint = true,
                        IsRepairMode = repairMode,
                    });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex))
                    {
                        viewModel.Start();
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        window = new Window { Content = view, Width = 320, Height = 700, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        TextBox editor = (TextBox)view.FindName("WaitTimeEditor");
                        Button clear = Descendants(view).OfType<Button>().Single(button => ReferenceEquals(button.Command, viewModel.ClearWaitPointCommand));

                        InvokeButton(clear, window);

                        Assert.AreEqual((false, true, 1), (editor.IsEnabled, clear.IsEnabled, invocations));
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false, Visibility.Collapsed)]
        [DataRow(true, Visibility.Visible)]
        public void WhenRepairModeBindingIsUnavailableThenTheBannerDoesNotFlash(bool repairMode, Visibility expected)
        {
            Window window = null;
            try
            {
                TrainPathToolView view = new TrainPathToolView();
                window = new Window { Content = view, Width = 520, Height = 520, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);
                TextBlock banner = Descendants(view).OfType<TextBlock>().Single(element => element.Text.StartsWith("Repair mode:", StringComparison.Ordinal));
                Assert.AreEqual(Visibility.Collapsed, banner.Visibility);
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge,
                    TrainPathSnapshot.Empty with { IsRepairMode = repairMode });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        viewModel.Start();
                        view.DataContext = viewModel;
                        SettleLayout(window);

                        Assert.AreEqual(expected, banner.Visibility);

                        view.DataContext = null;
                        SettleLayout(window);

                        Assert.AreEqual(Visibility.Collapsed, banner.Visibility);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenMetadataIsEditedAgainAfterEnterThenLeavingTheEditorCommitsTheLatestText()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        TrainPathToolView view = new TrainPathToolView() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        window.UpdateLayout();
                        TextBox editor = Descendants(view).OfType<TextBox>().Single(element => AutomationProperties.GetName(element) == "Path name");
                        TextBox nextEditor = Descendants(view).OfType<TextBox>().Single(element => AutomationProperties.GetName(element) == "Path start");
                        Assert.IsTrue(editor.Focus());
                        editor.Text = "First name";
                        editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), Environment.TickCount, Key.Enter)
                        {
                            RoutedEvent = Keyboard.PreviewKeyDownEvent,
                        });
                        Assert.AreEqual("First name", viewModel.PathName);
                        Assert.IsTrue(editor.IsKeyboardFocusWithin);

                        editor.Text = "Latest name";
                        Assert.IsTrue(nextEditor.Focus());

                        Assert.AreEqual("Latest name", viewModel.PathName);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(220)]
        [DataRow(520)]
        public void WhenPathHeaderFilterIsUsedThenTheLowerRowRetainsSearchUntilClearedAndToggled(int width)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge,
                            TrainPathSnapshot.Empty with
                            {
                                Paths = [
                                    new TrainPathListRow("path", "Morning service", global::FreeTrainSimulator.Models.Content.PathValidationState.NotValidated),
                                    new TrainPathListRow("other", "Evening service", global::FreeTrainSimulator.Models.Content.PathValidationState.NotValidated)],
                                SelectedPathId = "path",
                            });
                        viewModel.Start();
                        TrainPathListItemViewModel path = viewModel.Paths.Single(item => item.Id == "path");
                        TrainPathListItemViewModel otherPath = viewModel.Paths.Single(item => item.Id == "other");
                        TrainPathToolView view = new TrainPathToolView() { DataContext = viewModel };
                        window = new Window { Content = view, Width = width, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ColumnFilterHeader header = Descendants(view).OfType<ColumnFilterHeader>().Single();
                        TextBox filter = Descendants(header).OfType<TextBox>().Single();
                        Button lens = Descendants(header).OfType<Button>().Single(element => AutomationProperties.GetName(element) == "Toggle column filter");
                        Button clear = Descendants(header).OfType<Button>().Single(element => AutomationProperties.GetName(element) == "Clear column filter");
                        TextBlock caption = Descendants(header).OfType<TextBlock>().Single(element => element.Text == "Name");
                        Assert.IsFalse(filter.IsVisible);
                        InvokeButton(lens, window);
                        Assert.IsTrue(filter.IsVisible);
                        Assert.IsTrue(filter.IsKeyboardFocusWithin);
                        Assert.IsGreaterThanOrEqualTo(caption.TranslatePoint(new Point(0, caption.ActualHeight), header).Y, filter.TranslatePoint(new Point(), header).Y);
                        Assert.IsTrue(caption.IsVisible);

                        filter.Text = "mOrNiNg";
                        SettleLayout(window);
                        Assert.AreEqual("mOrNiNg", viewModel.SearchText);
                        Assert.IsTrue(path.IsVisible);
                        Assert.IsFalse(otherPath.IsVisible);
                        TextBox metadata = Descendants(view).OfType<TextBox>().Single(element => AutomationProperties.GetName(element) == "Path name");
                        Assert.IsTrue(metadata.Focus());
                        Assert.IsTrue(filter.IsVisible);
                        Assert.AreEqual("Name", caption.Text);
                        InvokeButton(lens, window);
                        Assert.IsTrue(filter.IsVisible);
                        viewModel.Start();
                        view.DataContext = null;
                        SettleLayout(window);
                        view.DataContext = viewModel;
                        window.Width = 180;
                        SettleLayout(window);
                        window.Width = width;
                        SettleLayout(window);

                        Assert.AreEqual("mOrNiNg", filter.Text);
                        Assert.IsTrue(filter.IsVisible);
                        Assert.AreEqual("Name", caption.Text);
                        Assert.AreSame(path, viewModel.SelectedPath);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Paths));
                        Assert.AreSame(path, list.SelectedItem);
                        ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(path);
                        Assert.AreEqual(FontWeights.Bold, row.FontWeight);
                        ListViewItem otherRow = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(otherPath);
                        Assert.AreEqual(Visibility.Collapsed, otherRow.Visibility);

                        InvokeButton(clear, window);
                        Assert.AreEqual(string.Empty, viewModel.SearchText);
                        Assert.IsTrue(otherPath.IsVisible);
                        Assert.AreEqual(Visibility.Visible, otherRow.Visibility);
                        Assert.IsTrue(filter.IsVisible);
                        InvokeButton(lens, window);
                        Assert.IsFalse(filter.IsVisible);
                        Assert.AreEqual("Name", caption.Text);
                        Assert.IsTrue(caption.IsVisible);
                        Assert.AreSame(path, viewModel.SelectedPath);
                        Assert.AreSame(path, list.SelectedItem);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenAColumnIsNotSearchableThenItReservesTheSharedFilterRowWithoutAnEditor()
        {
            Window window = null;
            try
            {
                ColumnFilterHeader searchable = new ColumnFilterHeader() { Caption = "Name", IsSearchable = true, IsFilterRowVisible = true };
                ColumnFilterHeader reserved = new ColumnFilterHeader() { Caption = "Status", IsSearchable = false, IsFilterRowVisible = true };
                StackPanel panel = new StackPanel();
                panel.Children.Add(searchable);
                panel.Children.Add(reserved);
                window = new Window { Content = panel, Width = 220, Height = 160, ShowInTaskbar = false };
                window.Show();
                SettleLayout(window);

                Assert.AreEqual(searchable.ActualHeight, reserved.ActualHeight);
                Assert.IsFalse(Descendants(reserved).OfType<TextBox>().Single().IsVisible);
                Assert.DoesNotContain(button => button.IsVisible, Descendants(reserved).OfType<Button>());
                double expandedHeight = reserved.ActualHeight;
                reserved.IsFilterRowVisible = false;
                SettleLayout(window);
                Assert.IsLessThan(expandedHeight, reserved.ActualHeight);
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenAnUnselectedPathHasUnsavedChangesThenOnlyTheAsteriskMarksIt()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        TrainPathListItemViewModel path = new TrainPathListItemViewModel("dirty", "Unsaved path", global::FreeTrainSimulator.Models.Content.PathValidationState.NotValidated, true);
                        viewModel.Paths.Add(path);
                        TrainPathToolView view = new TrainPathToolView() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Paths));
                        ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(path);
                        TextBlock name = Descendants(row).OfType<TextBlock>().Single(element => element.Text == "Unsaved path");
                        TextBlock marker = Descendants(row).OfType<TextBlock>().Single(element => element.Text == " *");

                        Assert.AreEqual(FontWeights.Normal, name.FontWeight);
                        Assert.IsTrue(marker.IsVisible);

                        viewModel.UserSelectPath(path);
                        SettleLayout(window);

                        Assert.AreEqual(FontWeights.Bold, name.FontWeight);
                        Assert.IsTrue(marker.IsVisible);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(true, "✓", "Valid path node")]
        [DataRow(false, "⚠", "Invalid path node")]
        public void WhenPathNodesAreDisplayedThenValidityHasItsOwnColumn(bool valid, string expectedSymbol, string expectedName)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        viewModel.Nodes.Add(new TrainPathNodeItemViewModel(new TrainPathNodeRow(0, global::FreeTrainSimulator.Common.PathNodeType.Via, valid, 1, -1, -1, null, valid ? null : "Off track")));
                        viewModel.SelectedTabIndex = TrainPathToolWindowViewModel.EditorTabIndex;
                        TrainPathToolView view = new TrainPathToolView() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 320, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Nodes));
                        GridView grid = (GridView)list.View;
                        ListViewItem row = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                        TextBlock status = Descendants(row).OfType<TextBlock>().Single(element => AutomationProperties.GetName(element) == expectedName);

                        Assert.HasCount(3, grid.Columns);
                        Assert.AreEqual("Type", grid.Columns[2].Header);
                        Assert.AreEqual(expectedSymbol, status.Text);
                        Assert.IsTrue(status.IsVisible);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(320)]
        [DataRow(520)]
        public void WhenIssuesAreExpandedThenTheListAndSelectedMessageRemainVisible(int width)
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        TrainPathDiagnosticItemViewModel issue = new TrainPathDiagnosticItemViewModel(new TrainPathDiagnosticRow(
                            global::FreeTrainSimulator.Runtime.Track.PathRouteDiagnosticSeverity.Error,
                            global::FreeTrainSimulator.Runtime.Track.PathRouteDiagnosticCode.AnchorNotOnTrack,
                            "The selected node is off track.", 0, -1, -1, "Move the node onto track.", true));
                        viewModel.Diagnostics.Add(issue);
                        viewModel.SelectedTabIndex = TrainPathToolWindowViewModel.EditorTabIndex;
                        viewModel.IsIssuesExpanded = true;
                        TrainPathToolView view = new TrainPathToolView() { DataContext = viewModel };
                        window = new Window { Content = view, Width = width, Height = 900, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        Expander details = Descendants(view).OfType<Expander>().Single(element => Equals(element.Header, "Details"));
                        details.IsExpanded = false;
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.VisibleDiagnostics));
                        TextBlock message = Descendants(view).OfType<TextBlock>().Single(element => element.Text == "Message");
                        Assert.IsFalse(message.IsVisible);

                        list.SelectedItem = issue;
                        SettleLayout(window);

                        Assert.AreSame(issue, viewModel.SelectedDiagnostic);
                        Assert.IsGreaterThanOrEqualTo(40, list.ActualHeight);
                        Assert.IsTrue(message.IsVisible);
                        Assert.IsGreaterThanOrEqualTo(list.TranslatePoint(new Point(0, list.ActualHeight), view).Y, message.TranslatePoint(new Point(), view).Y);
                        Assert.Contains(element => element.Text == issue.Message && element.IsVisible, Descendants(view).OfType<TextBlock>());
                        Assert.Contains(element => Equals(element.Content, "Repair node") && element.IsVisible, Descendants(view).OfType<Button>());
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenNodeRowsAreSelectedScrolledAndRefreshedThenRenderedIndexesMatchTheirPositions()
        {
            Window window = null;
            try
            {
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, _ => { }, () => { }, () => { }, _ => { }, () => { }, () => { });
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex))
                    {
                        TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
                        {
                            Nodes = System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(0, 112).Select(index =>
                                new TrainPathNodeRow(index, global::FreeTrainSimulator.Common.PathNodeType.Via, true, 10, index + 1, -1, null, null))),
                        };
                        typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, snapshot);
                        viewModel.Start();
                        TrainPathToolView view = new TrainPathToolView() { DataContext = viewModel };
                        window = new Window { Content = view, Width = 320, Height = 520, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Nodes));

                        foreach (int selectedIndex in new[] { 10, 100, 101, 109, 110, 111, 51, 0 })
                        {
                            list.ScrollIntoView(viewModel.Nodes[selectedIndex]);
                            SettleLayout(window);
                            list.SelectedItem = viewModel.Nodes[selectedIndex];
                            snapshot = snapshot with { SelectedNodeIndex = selectedIndex };
                            typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, snapshot);
                            viewModel.Start();
                            window.Width = selectedIndex % 2 == 0 ? 520 : 320;
                            SettleLayout(window);

                            Assert.AreSequenceEqual(Enumerable.Range(0, 112).ToArray(), viewModel.Nodes.Select(node => node.Index).ToArray());
                            foreach (ListViewItem row in Descendants(list).OfType<ListViewItem>())
                            {
                                TrainPathNodeItemViewModel node = (TrainPathNodeItemViewModel)row.Content;
                                TextBlock indexCell = Descendants(row).OfType<TextBlock>().Single(text => text.Name == "NodeIndexText");
                                Assert.AreEqual(node.Index.ToString(System.Globalization.CultureInfo.InvariantCulture), indexCell.Text, $"Rendered row {node.Index} after selecting {selectedIndex}");
                                Assert.AreEqual(TextTrimming.CharacterEllipsis, indexCell.TextTrimming);
                                Assert.AreEqual(node.Index, indexCell.ToolTip);
                                TextBlock measured = new TextBlock()
                                {
                                    Text = indexCell.Text,
                                    FontFamily = indexCell.FontFamily,
                                    FontSize = indexCell.FontSize,
                                    FontWeight = indexCell.FontWeight,
                                };
                                measured.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                Assert.IsGreaterThanOrEqualTo(measured.DesiredSize.Width, indexCell.ActualWidth + 0.5, $"Index {node.Index} needs {measured.DesiredSize.Width} units but has {indexCell.ActualWidth}.");
                            }
                        }
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenSavedComparisonCheckboxIsClickedThenPointerPreviewDoesNotSelectOrUnloadEditor(bool selected)
        {
            Window window = null;
            try
            {
                int selections = 0;
                int comparisons = 0;
                TrainPathComparisonRow comparison = new TrainPathComparisonRow("saved", "Saved path", 3, false, false, null, System.Drawing.Color.Empty);
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, action => action(),
                    () => { }, () => { }, _ => selections++, () => selections++, () => { }, null, (id, value) =>
                    {
                        Assert.AreEqual(("saved", true), (id, value));
                        comparisons++;
                        return System.Threading.Tasks.Task.CompletedTask;
                    });
                bridge.UpdateComparisons([comparison]);
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        TrainPathListItemViewModel path = new TrainPathListItemViewModel("saved", "Saved path", global::FreeTrainSimulator.Models.Content.PathValidationState.Valid, true);
                        path.UpdateComparison(comparison);
                        viewModel.Paths.Add(path);
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        ListView list = Descendants(view).OfType<ListView>().Single(element => ReferenceEquals(element.ItemsSource, viewModel.Paths));
                        if (selected)
                            viewModel.UserSelectPath(path);
                        SettleLayout(window);
                        selections = 0;
                        CheckBox toggle = Descendants(list).OfType<CheckBox>().Single(element => element.Name == "SavedPathComparisonToggle");
                        TextBlock caption = Descendants(toggle).OfType<TextBlock>().Single();

                        caption.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                        typeof(CheckBox).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(toggle, null);
                        SettleLayout(window);

                        Assert.AreEqual((0, 1, selected), (selections, comparisons, ReferenceEquals(path, viewModel.SelectedPath)));
                        Assert.AreEqual(selected, ReferenceEquals(path, list.SelectedItem));
                        Assert.IsTrue(path.HasUnsavedChanges);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenComparisonClickAwaitsOwnerThenSelectionAndSwatchChangeOnlyWithAuthoritativeSnapshot()
        {
            Window window = null;
            System.Threading.Tasks.TaskCompletionSource completion = new System.Threading.Tasks.TaskCompletionSource(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                int comparisons = 0;
                TrainPathComparisonRow initial = new TrainPathComparisonRow("saved", "Saved path", 4, false, false, null, System.Drawing.Color.Empty);
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, action => action(),
                    () => { }, () => { }, _ => Assert.Fail("Comparison must not load the editor."), () => Assert.Fail("Comparison must not unload the editor."), () => { }, null,
                    (_, _) => { comparisons++; return completion.Task; });
                bridge.UpdateComparisons([initial]);
                FieldInfo snapshotField = typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
                {
                    Paths = [new TrainPathListRow("saved", "Saved path", global::FreeTrainSimulator.Models.Content.PathValidationState.Valid, true)],
                    Comparisons = [initial],
                };
                snapshotField.SetValue(bridge, snapshot);
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        viewModel.Start();
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        TrainPathListItemViewModel path = viewModel.Paths.Single();
                        CheckBox toggle = Descendants(view).OfType<CheckBox>().Single(element => element.Name == "SavedPathComparisonToggle");

                        typeof(CheckBox).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(toggle, null);
                        SettleLayout(window);

                        Assert.AreEqual((1, false, true), (comparisons, path.IsCompared, path.HasUnsavedChanges));
                        Assert.AreSame(Brushes.Transparent, path.ComparisonBrush);
                        Assert.IsNull(viewModel.SelectedPath);
                        System.Drawing.Color color = System.Drawing.Color.FromArgb(230, 159, 0);
                        TrainPathComparisonRow authoritative = new TrainPathComparisonRow("saved", "Saved path", 4, true, false, null, color);
                        snapshotField.SetValue(bridge, snapshot with { Comparisons = [authoritative] });
                        completion.SetResult();
                        viewModel.Start();
                        SettleLayout(window);

                        Assert.AreSame(path, viewModel.Paths.Single());
                        Assert.AreEqual((true, true, true), (path.IsCompared, toggle.IsChecked == true, path.HasUnsavedChanges));
                        SolidColorBrush brush = (SolidColorBrush)path.ComparisonBrush;
                        Assert.AreEqual(Color.FromArgb(color.A, color.R, color.G, color.B), brush.Color);
                        Assert.IsTrue(brush.IsFrozen);
                        Assert.IsNull(viewModel.SelectedPath);
                    }
                }
            }
            finally
            {
                completion.TrySetResult();
                window?.Close();
            }
        }

        private static readonly bool[] expectedtruefalse = new[] { true, false };

        [TestMethod]
        [Timeout(15000, CooperativeCancellation = true)]
        public void WhenSavedComparisonCheckboxIsRapidlyCheckedAndUncheckedThenBothActualStatesAreQueuedBeforeSnapshotRefresh()
        {
            Window window = null;
            try
            {
                Queue<Action> commands = new Queue<Action>();
                List<bool> requested = new List<bool>();
                TrainPathComparisonRow comparison = new TrainPathComparisonRow("saved", "Saved path", 4, false, false, null, System.Drawing.Color.Empty);
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => null, () => null, commands.Enqueue,
                    () => { }, () => { }, _ => Assert.Fail("Comparison must not load the editor."),
                    () => Assert.Fail("Comparison must not unload the editor."), () => { }, null,
                    (_, selected) => { requested.Add(selected); return System.Threading.Tasks.Task.CompletedTask; });
                bridge.UpdateComparisons([comparison]);
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        TrainPathListItemViewModel path = new TrainPathListItemViewModel("saved", "Saved path", global::FreeTrainSimulator.Models.Content.PathValidationState.Valid, true);
                        path.UpdateComparison(comparison);
                        viewModel.Paths.Add(path);
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        window = new Window { Content = view, Width = 520, Height = 700, ShowInTaskbar = false };
                        window.Show();
                        SettleLayout(window);
                        CheckBox toggle = Descendants(view).OfType<CheckBox>().Single(element => element.Name == "SavedPathComparisonToggle");
                        MethodInfo click = typeof(CheckBox).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);

                        click.Invoke(toggle, null);
                        bool firstState = toggle.IsChecked == true;
                        click.Invoke(toggle, null);
                        bool secondState = toggle.IsChecked == true;
                        commands.Dequeue()();
                        commands.Dequeue()();

                        Assert.AreSequenceEqual(expectedtruefalse, requested);
                        Assert.AreEqual((true, false, false, true, null), (firstState, secondState, path.IsCompared, path.HasUnsavedChanges, viewModel.SelectedPath));
                        Assert.AreSame(Brushes.Transparent, path.ComparisonBrush);
                    }
                }
            }
            finally
            {
                window?.Close();
            }
        }

        private static void InvokeButton(Button button, Window window)
        {
            IInvokeProvider provider = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
            provider.Invoke();
            SettleLayout(window);
        }

        private static void SettleLayout(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (DependencyObject descendant in Descendants(child))
                    yield return descendant;
            }
        }
    }
}
