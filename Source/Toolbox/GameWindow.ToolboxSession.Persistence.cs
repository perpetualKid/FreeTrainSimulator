using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.PathEditing;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow
    {
        internal sealed partial class ToolboxSession
        {
            private PathEditor saveDialogEditor;
            private PathModel saveDialogSource;
            private long saveDialogRouteVersion;

            internal TrainPathSaveState CaptureTrainPathSaveState()
            {
                if (IsStopping)
                    throw new InvalidOperationException("The Toolbox session is stopping.");
                
                PathModel pathModel = pathEditor?.TryCaptureCurrentPathModel();
                
                if (pathModel == null)
                    return null;

                saveDialogEditor = pathEditor;
                saveDialogSource = pathModel;
                saveDialogRouteVersion = routeContentVersion;
                PathModelHeader pathDetails = new PathModelHeader
                {
                    Id = pathModel.Id,
                    Name = pathModel.Name,
                    Start = pathModel.Start,
                    End = pathModel.End,
                    PlayerPath = pathModel.PlayerPath,
                };
                return new TrainPathSaveState(pathDetails, pathModel.Id);
            }

            internal Task<bool> TrainPathIdExistsAsync(string pathId) => RunAsync(async () =>
            {
                RouteModelHeader route = selectedRoute;
                if (string.IsNullOrWhiteSpace(pathId) || route == null)
                    return false;

                ImmutableArray<PathModelHeader> paths = await route.GetRoutePaths(CancellationToken.None).WaitAsync(lifetime.Token).ConfigureAwait(false);
                return paths.Any(path => string.Equals(path.Id, pathId, StringComparison.OrdinalIgnoreCase));
            });

            internal Task SubmitTrainPathSaveAsync(TrainPathSaveRequest saveRequest) => RunAsync(() => SavePathCoreAsync(saveRequest));

            private async Task SavePathCoreAsync(TrainPathSaveRequest saveRequest)
            {
                ArgumentNullException.ThrowIfNull(saveRequest);
                PathEditor editor = pathEditor;
                RouteModelHeader route = selectedRoute;
                PathModel submittedSourceModel = editor?.TryCaptureCurrentPathModel();
                long saveRouteContentVersion = routeContentVersion;
                
                if (editor == null || route == null || !ReferenceEquals(editor, saveDialogEditor)
                    || !ReferenceEquals(submittedSourceModel, saveDialogSource) || saveDialogRouteVersion != saveRouteContentVersion
                    || !string.Equals(editor.PathId, saveRequest.SourcePathId, StringComparison.OrdinalIgnoreCase))
                {
                    Trace.TraceWarning("Cannot save train path because the active route or path changed before save submission.");
                    return;
                }

                PathSaveOperation operation = null;
                
                try
                {
                    ImmutableArray<PathModelHeader> routePaths = await route.GetRoutePaths(CancellationToken.None).WaitAsync(lifetime.Token).ConfigureAwait(false);
                    bool targetPathExists = routePaths.Any(path => string.Equals(path.Id, saveRequest.PathDetails.Id, StringComparison.OrdinalIgnoreCase));
                    
                    if (!saveRequest.CanSubmit(targetPathExists))
                    {
                        Trace.TraceInformation($"Save As for train path '{saveRequest.PathDetails.Id}' was not confirmed.");
                        return;
                    }
                    
                    operation = await game.InvokeOnGameThreadAsync(() =>
                    {
                        if (IsStopping || saveRouteContentVersion != routeContentVersion || !ReferenceEquals(selectedRoute, route)
                            || !ReferenceEquals(pathEditor, editor) || !ReferenceEquals(editor.TryCaptureCurrentPathModel(), submittedSourceModel))
                        {
                            Trace.TraceWarning("Cannot save train path because the active route or path changed before persistence began.");
                            return Task.FromResult<PathSaveOperation>(null);
                        }
                        return Task.FromResult(editor.BeginSave(saveRequest.PathDetails));
                    }).ConfigureAwait(false);

                    if (operation == null)
                        return;
                    
                    // Once persistence starts, drain its actual result, even after session cancellation.
                    PathPersistenceValidationResult validation = await PathSaveOperationConsumer.ConsumeAsync(editor, operation, game.InvokeOnGameThreadAsync).ConfigureAwait(false);
                    if (!validation.PersistenceAllowed)
                    {
                        Trace.TraceWarning(validation.FailureMessage);
                        await game.InvokeOnGameThreadAsync(() =>
                        {
                            if (ReferenceEquals(pathEditor, editor))
                                game.hostedTrainPathToolWindow?.ReportBlockedSave(validation, operation.SourceModel);
                            return Task.CompletedTask;
                        }).ConfigureAwait(false);
                        return;
                    }
                    bool saveCompletionApplied = await game.InvokeOnGameThreadAsync(() =>
                    {
                        bool current = ReferenceEquals(pathEditor, editor) && ReferenceEquals(editor.TryCaptureCurrentPathModel(), validation.PathModel);
                        return Task.FromResult(current && (game.hostedTrainPathToolWindow?.CompleteSavedPath(operation.SourcePathId,
                            validation.PathModel.Id, validation.PathModel, game.Catalog.GetString("Path saved.")) ?? true));
                    }).ConfigureAwait(false);
                    
                    if (!saveCompletionApplied)
                        return;
                }
                catch (OperationCanceledException ex)
                {
                    if (PersistenceSucceeded(operation))
                    {
                        Trace.TraceWarning($"Train path persistence succeeded, but save finalization was canceled: {ex.Message}");
                        return;
                    }
                    Trace.TraceInformation($"Train path save was canceled: {ex.Message}");
                    await ReportTrainPathStatusWarningAsync(editor, saveRequest.SourcePathId, operation?.SourceModel ?? submittedSourceModel,
                        game.Catalog.GetString("Path save was canceled. Your changes are still available; try saving again.")).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    if (PersistenceSucceeded(operation))
                    {
                        Trace.TraceError($"Train path persistence succeeded, but save finalization failed: {ex}");
                        return;
                    }
                    Trace.TraceError($"Failed to save train path: {ex}");
                    string message = ex is UnauthorizedAccessException
                        ? game.Catalog.GetString("The path could not be saved because access was denied. Your changes are still available; try saving again.")
                        : game.Catalog.GetString("The path could not be saved. Your changes are still available; try saving again.");
                    await ReportTrainPathStatusWarningAsync(editor, saveRequest.SourcePathId, operation?.SourceModel ?? submittedSourceModel, message).ConfigureAwait(false);
                    return;
                }
                await RefreshPathListAfterSaveAsync(route, saveRouteContentVersion).ConfigureAwait(false);
            }

            private static bool PersistenceSucceeded(PathSaveOperation operation)
                => operation?.PersistenceTask.IsCompletedSuccessfully == true && operation.PersistenceTask.Result.PersistenceAllowed;

            private Task ReportTrainPathStatusWarningAsync(PathEditor editor, string expectedPathId, PathModel expectedSourceModel, string message)
                => game.InvokeOnGameThreadAsync(() =>
                {
                    if (ReferenceEquals(pathEditor, editor) && string.Equals(editor.PathId, expectedPathId, StringComparison.OrdinalIgnoreCase))
                        game.hostedTrainPathToolWindow?.ReportSaveFailure(message, expectedSourceModel);
                    return Task.CompletedTask;
                });

            private async Task RefreshPathListAfterSaveAsync(RouteModelHeader route, long saveRouteContentVersion)
            {
                try
                {
                    long refreshVersion = Interlocked.Increment(ref pathListRefreshVersion);
                    ImmutableArray<PathModelHeader> paths = await route.GetRoutePaths(CancellationToken.None).WaitAsync(lifetime.Token).ConfigureAwait(false);
                    await game.InvokeOnGameThreadAsync(() =>
                    {
                        if (!IsStopping && refreshVersion == pathListRefreshVersion && saveRouteContentVersion == routeContentVersion && ReferenceEquals(selectedRoute, route))
                        {
                            game.menu.PopulatePaths(paths);
                            game.hostedTrainPathToolWindow?.UpdatePaths(paths);
                        }
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    Trace.TraceInformation($"Train path list refresh was canceled after save: {ex.Message}");
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    Trace.TraceError($"Failed to refresh the train path list after save: {ex}");
                }
            }
        }
    }
}
