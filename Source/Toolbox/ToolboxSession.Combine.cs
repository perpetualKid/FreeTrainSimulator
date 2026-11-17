using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox
{
    internal sealed partial class ToolboxSession
    {
        private CancellationTokenSource combineRequest;
        private long combineRequestVersion;
        private PendingPathCombination pendingPathCombination;

        /// <summary>Combines the two checked saved baselines, or cancels their outstanding combination.</summary>
        internal Task CombineSelectedPathsAsync(bool combine) => RunAsync(() => CombineSelectedPathsCoreAsync(combine));

        private async Task CombineSelectedPathsCoreAsync(bool combine)
        {
            game.VerifySessionOwner();
            if (!combine)
            {
                CancelPathCombine();
                ReportCombineResult(PathEditorCommandResult.Succeeded(game.Catalog.GetString("Path combination canceled."), pathEditor?.TryCaptureCurrentPathModel()));
                return;
            }
            RouteModelHeader route = selectedRoute;
            ToolboxContent content = toolboxContent;
            long version = routeContentVersion;
            KeyValuePair<string, ComparisonRequest>[] selected = comparisons.Where(pair => pair.Value.IsSelected).ToArray();
            if (selected.Length != 2 || selected.Any(pair => pair.Value.IsLoading || pair.Value.Error != null)
                || route == null || content == null || compositionRequest != null || pathEditor?.CanCombinePaths == false)
            {
                ReportCombineResult(PathEditorCommandResult.Failed(game.Catalog.GetString("Combine Paths requires exactly two checked, loaded saved paths and an idle editor."), pathEditor?.TryCaptureCurrentPathModel()));
                return;
            }
            PathEditor editor = GetPathEditor();
            TrackWorld world = editor.RuntimeTrackWorld;
            PathModel source = editor.TryCaptureCurrentPathModel();
            long revision = editor.CompositionRevision;
            long selectionVersion = comparisonRequestVersion;
            string firstId = selected[0].Key;
            string secondId = selected[1].Key;
            CancelPathCombine();
            using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            combineRequest = request;
            long requestVersion = ++combineRequestVersion;
            PublishPathCombineState();
            try
            {
                await Task.Run(async () =>
                {
                    PathModel first = await route.PathModelUncached(firstId, request.Token).ConfigureAwait(false);
                    PathModel second = await route.PathModelUncached(secondId, request.Token).ConfigureAwait(false);
                    if (first == null || !string.Equals(first.Id, firstId, StringComparison.OrdinalIgnoreCase)
                        || second == null || !string.Equals(second.Id, secondId, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("A selected saved path is missing or has a different identity.");
                    PathCombinationResult prepared = PathModelEditor.PreparePathCombination(first, second, world, request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    await game.InvokeOnGameThreadAsync(() =>
                    {
                        if (IsCurrentCombine(request, route, content, version, editor, world))
                        {
                            combineRequest = null;
                            PathEditorCommandResult result;
                            if (IsUnchangedCombineSource(selected, selectionVersion, editor, source, revision))
                            {
                                if (prepared.Candidates.Length > 1 && editor.BeginPathCombinationPreview())
                                {
                                    ImmutableArray<TrainPathCombinationRow> rows = prepared.Candidates.Select((candidate, index) =>
                                        new TrainPathCombinationRow(requestVersion, index, first.Name ?? first.Id, second.Name ?? second.Id,
                                            candidate.FirstReversed, candidate.SecondReversed, candidate.Combination.PathModel.Start, candidate.Combination.PathModel.End)).ToImmutableArray();
                                    pendingPathCombination = new PendingPathCombination(editor, prepared.Candidates, rows,
                                        () => !IsStopping && ReferenceEquals(selectedRoute, route) && ReferenceEquals(toolboxContent, content)
                                            && version == routeContentVersion && ReferenceEquals(pathEditor, editor) && ReferenceEquals(editor.RuntimeTrackWorld, world)
                                            && IsUnchangedCombineSource(selected, selectionVersion, editor, source, revision));
                                    result = PathEditorCommandResult.Succeeded(game.Catalog.GetString("Choose a path orientation to preview, then accept it or Cancel."), source);
                                }
                                else if (prepared.Candidates.Length == 1)
                                    result = CommitPathCombination(editor, prepared.Candidates[0].Combination);
                                else if (prepared.Candidates.IsEmpty)
                                    result = editor.CombinePathsCommand(prepared.Refusal);
                                else
                                    result = ChangedCombineSourceResult(editor);
                            }
                            else
                                result = ChangedCombineSourceResult(editor);
                            ReportCombineResult(result);
                            PublishPathCombineState();
                        }
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }, request.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested)
            {
                // Cancellation owns no editor mutation or stale completion feedback.
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or MemoryPack.MemoryPackSerializationException)
            {
                Trace.TraceWarning($"Combine of saved paths '{firstId}'/'{secondId}' failed: {error}");
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (IsCurrentCombine(request, route, content, version, editor, world))
                        ReportCombineResult(IsUnchangedCombineSource(selected, selectionVersion, editor, source, revision)
                            ? PathEditorCommandResult.Failed(game.Catalog.GetString("A selected saved path could not be read. Verify the native model and access permissions, then retry."), editor.TryCaptureCurrentPathModel())
                            : ChangedCombineSourceResult(editor));
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
            finally
            {
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (ReferenceEquals(combineRequest, request))
                    {
                        combineRequest = null;
                        PublishPathCombineState();
                    }
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }

        private bool IsCurrentCombine(CancellationTokenSource request, RouteModelHeader route, ToolboxContent content, long version, PathEditor editor, TrackWorld world)
            => !IsStopping && !request.IsCancellationRequested && ReferenceEquals(combineRequest, request)
                && ReferenceEquals(selectedRoute, route) && ReferenceEquals(toolboxContent, content) && version == routeContentVersion
                && ReferenceEquals(pathEditor, editor) && ReferenceEquals(editor.RuntimeTrackWorld, world);

        private bool IsUnchangedCombineSource(KeyValuePair<string, ComparisonRequest>[] selected, long selectionVersion, PathEditor editor, PathModel source, long revision)
            => selectionVersion == comparisonRequestVersion && comparisons.Count(pair => pair.Value.IsSelected) == 2
                && selected.All(pair => comparisons.TryGetValue(pair.Key, out ComparisonRequest current)
                    && ReferenceEquals(current, pair.Value) && current.IsSelected && !current.IsLoading && current.Error == null)
                && ReferenceEquals(editor.TryCaptureCurrentPathModel(), source) && editor.CompositionRevision == revision;

        private static PathEditorCommandResult ChangedCombineSourceResult(PathEditor editor)
            => PathEditorCommandResult.Failed(PathEditor.CombineSourceChangedMessage,
                PathEditorMessageKind.CombineSelectionChangedWhileLoading, editor?.TryCaptureCurrentPathModel());

        private void CancelPathCombine()
        {
            CancellationTokenSource request = combineRequest;
            combineRequest = null;
            request?.Cancel();
            PendingPathCombination pending = pendingPathCombination;
            pendingPathCombination = null;
            pending?.Editor.ClearPathCombinationPreview();
            PublishPathCombineState();
        }

        /// <summary>Resolves an immutable displayed orientation, never an index in a newer preparation.</summary>
        internal Task SelectPathCombinationAsync(long requestVersion, int candidateIndex, PathCombinationAction action) => RunAsync(() =>
        {
            game.VerifySessionOwner();
            PendingPathCombination pending = pendingPathCombination;
            if (pending == null || requestVersion != combineRequestVersion)
            {
                ReportCombineResult(ChangedCombineSourceResult(pathEditor));
                return Task.CompletedTask;
            }
            if (!pending.IsCurrent() || !pending.Editor.IsChoosingPathCombination)
            {
                CancelPathCombine();
                ReportCombineResult(ChangedCombineSourceResult(pending.Editor));
                return Task.CompletedTask;
            }
            if (action == PathCombinationAction.Cancel)
            {
                CancelPathCombine();
                ReportCombineResult(PathEditorCommandResult.Succeeded(game.Catalog.GetString("Path combination canceled."), pending.Editor.TryCaptureCurrentPathModel()));
                return Task.CompletedTask;
            }
            if (candidateIndex < 0 || candidateIndex >= pending.Candidates.Length
                || action is not (PathCombinationAction.Preview or PathCombinationAction.Accept)
                || action == PathCombinationAction.Accept && pending.PreviewedIndex != candidateIndex)
            {
                ReportCombineResult(PathEditorCommandResult.Failed(game.Catalog.GetString("Preview a current path orientation before accepting it."), pending.Editor.TryCaptureCurrentPathModel()));
                return Task.CompletedTask;
            }
            PathEditResult combination = pending.Candidates[candidateIndex].Combination;
            if (action == PathCombinationAction.Preview)
            {
                pending.Editor.PreviewPathCombination(combination.PathModel);
                pending.PreviewedIndex = candidateIndex;
                ReportCombineResult(PathEditorCommandResult.Succeeded(game.Catalog.GetString("Path orientation previewed. Accept to create a new unsaved path, or Cancel."), pending.Editor.TryCaptureCurrentPathModel()));
            }
            else
            {
                CancelPathCombine();
                ReportCombineResult(CommitPathCombination(pending.Editor, combination));
            }
            PublishPathCombineState();
            return Task.CompletedTask;
        });

        private PathEditorCommandResult CommitPathCombination(PathEditor editor, PathEditResult combination)
        {
            if (combination.Success && editor.CanCombinePaths)
                game.HostedServices?.TrainPathToolWindow?.CaptureTransientCurrentPath();
            return editor.CombinePathsCommand(combination);
        }

        private void InvalidatePendingPathCombination()
        {
            PendingPathCombination pending = pendingPathCombination;
            if (pending != null && (!pending.IsCurrent() || !pending.Editor.IsChoosingPathCombination))
            {
                CancelPathCombine();
                ReportCombineResult(ChangedCombineSourceResult(pending.Editor));
            }
        }

        private void ReportCombineResult(PathEditorCommandResult result) => game.HostedServices?.TrainPathToolWindow?.ReportCombineResult(result);

        private void PublishPathCombineState()
            => game.HostedServices?.TrainPathToolWindow?.UpdateCombineState(combineRequest != null, combineRequestVersion,
                pendingPathCombination?.Rows ?? ImmutableArray<TrainPathCombinationRow>.Empty, pendingPathCombination?.PreviewedIndex ?? -1);

        private sealed class PendingPathCombination
        {
            public PathEditor Editor { get; }
            public ImmutableArray<PathCombinationCandidate> Candidates { get; }
            public ImmutableArray<TrainPathCombinationRow> Rows { get; }
            public Func<bool> IsCurrent { get; }
            public int PreviewedIndex { get; set; } = -1;

            public PendingPathCombination(PathEditor editor, ImmutableArray<PathCombinationCandidate> candidates,
                ImmutableArray<TrainPathCombinationRow> rows, Func<bool> isCurrent)
            {
                Editor = editor;
                Candidates = candidates;
                Rows = rows;
                IsCurrent = isCurrent;
            }
        }
    }
}
