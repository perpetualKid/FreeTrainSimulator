using System;
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
        private CancellationTokenSource compositionRequest;
        private ImmutableArray<TrainPathCompositionSourceRow> compositionSources = ImmutableArray<TrainPathCompositionSourceRow>.Empty;

        internal Task SetSavedPathCompositionAsync(TrainPathCompositionSourceRow source, bool append) => RunAsync(() => SetSavedPathCompositionCoreAsync(source, append));

        private async Task SetSavedPathCompositionCoreAsync(TrainPathCompositionSourceRow source, bool append)
        {
            game.VerifySessionOwner();
            if (!append)
            {
                CancelPathComposition();
                ReportCompositionResult(PathEditorCommandResult.Succeeded(game.Catalog.GetString("Path composition canceled."), pathEditor?.TryCaptureCurrentPathModel()));
                return;
            }
            PathEditor editor = pathEditor;
            PathModel current = editor?.TryCaptureCurrentPathModel();
            RouteModelHeader route = selectedRoute;
            ToolboxContent content = toolboxContent;
            TrackWorld world = editor?.RuntimeTrackWorld;
            long version = routeContentVersion;
            if (source == null || source.RouteContentVersion != version || route == null || content == null
                || !menuPaths.Any(path => string.Equals(path.Id, source.Id, StringComparison.OrdinalIgnoreCase)))
            {
                ReportCompositionResult(PathEditorCommandResult.Failed(game.Catalog.GetString("The saved-path list changed. Select a current source to retry."), current));
                return;
            }
            if (editor?.CanAppendSavedPath != true)
            {
                ReportCompositionResult(PathEditorCommandResult.Failed(game.Catalog.GetString("Saved-path composition requires an idle, event-free main path and is unavailable during repair or saving."), current));
                return;
            }
            long revision = editor.CompositionRevision;
            CancelPathComposition();
            using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            compositionRequest = request;
            PublishPathCompositionState();
            try
            {
                await Task.Run(async () =>
                {
                    PathModel extension = await route.PathModelUncached(source.Id, request.Token).ConfigureAwait(false);
                    if (extension == null || !string.Equals(extension.Id, source.Id, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The saved extension is missing or has a different identity.");
                    PathEditResult prepared = PathModelEditor.AppendPathAtMatchingEndpoint(current, extension, world, request.Token);
                    if (prepared.Success)
                    {
                        PathPersistenceValidationResult validation = PathPersistenceValidationPolicy.ValidateForPersistence(prepared.PathModel, world);
                        if (!validation.PersistenceAllowed)
                            prepared = PathEditResult.Failed(validation.FailureMessage, current);
                    }
                    request.Token.ThrowIfCancellationRequested();
                    await game.InvokeOnGameThreadAsync(() =>
                    {
                        if (IsCurrentPathComposition(request, route, content, version, editor, world))
                        {
                            compositionRequest = null;
                            ReportCompositionResult(editor.CommitPathCompositionCommand(current, revision, prepared));
                            PublishPathCompositionState();
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
                Trace.TraceWarning($"Saved-path composition source '{source.Id}' failed: {error}");
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (IsCurrentPathComposition(request, route, content, version, editor, world))
                        ReportCompositionResult(editor.CommitPathCompositionCommand(current, revision,
                            PathEditResult.Failed(game.Catalog.GetString("The saved extension could not be read. Verify the native model and access permissions, then retry."), current)));
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
            finally
            {
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (ReferenceEquals(compositionRequest, request))
                    {
                        compositionRequest = null;
                        PublishPathCompositionState();
                    }
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }

        private bool IsCurrentPathComposition(CancellationTokenSource request, RouteModelHeader route, ToolboxContent content, long version, PathEditor editor, TrackWorld world)
            => !IsStopping && !request.IsCancellationRequested && ReferenceEquals(compositionRequest, request)
                && ReferenceEquals(selectedRoute, route) && ReferenceEquals(toolboxContent, content) && version == routeContentVersion
                && ReferenceEquals(pathEditor, editor) && ReferenceEquals(editor.RuntimeTrackWorld, world);

        private void CancelPathComposition()
        {
            CancellationTokenSource request = compositionRequest;
            compositionRequest = null;
            request?.Cancel();
            PublishPathCompositionState();
        }

        private void PublishPathCompositionSources()
        {
            compositionSources = menuPaths.Select(path => new TrainPathCompositionSourceRow(path.Id, path.Name, routeContentVersion)).ToImmutableArray();
            PublishPathCompositionState();
        }

        private void PublishPathCompositionState()
            => game.HostedServices?.TrainPathToolWindow?.UpdateCompositionState( IsStopping || selectedRoute == null ? ImmutableArray<TrainPathCompositionSourceRow>.Empty : compositionSources, compositionRequest != null);

        private void ReportCompositionResult(PathEditorCommandResult result) => game.HostedServices?.TrainPathToolWindow?.ReportCompositionResult(result);
    }
}
