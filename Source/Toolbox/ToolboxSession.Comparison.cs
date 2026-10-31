using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Color = Microsoft.Xna.Framework.Color;
using DrawingColor = System.Drawing.Color;

namespace FreeTrainSimulator.Toolbox
{
    internal sealed partial class ToolboxSession
    {
        private static readonly ImmutableArray<Color> comparisonPalette = ImmutableArray.Create(
            new Color(230, 159, 0), new Color(86, 180, 233), new Color(0, 158, 115),
            new Color(240, 228, 66), new Color(0, 114, 178), new Color(204, 121, 167));
        private readonly Dictionary<string, ComparisonRequest> comparisons = new Dictionary<string, ComparisonRequest>(StringComparer.OrdinalIgnoreCase);
        private long comparisonRequestVersion;

        /// <summary>Changes only an independent saved baseline; never selects, loads, or mutates the editor.</summary>
        internal Task SetSavedPathComparisonAsync(string pathId, bool selected) => RunAsync(() => SetSavedPathComparisonCoreAsync(pathId, selected));

        private async Task SetSavedPathComparisonCoreAsync(string pathId, bool selected)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pathId);
            // Admission and request capture run on the game owner; worker completions must return to that owner.
            game.VerifySessionOwner();
            if (comparisons.Remove(pathId, out ComparisonRequest previous))
            {
                previous.CancelIfLoading();
                toolboxContent?.ClearSavedPathComparison(pathId);
            }
            if (!selected)
            {
                PublishSavedPathComparisons();
                return;
            }

            RouteModelHeader route = selectedRoute;
            ToolboxContent content = toolboxContent;
            long routeVersion = routeContentVersion;
            PathModelHeader header = menuPaths.FirstOrDefault(path => string.Equals(path.Id, pathId, StringComparison.OrdinalIgnoreCase));
            if (route == null || content == null || header == null)
            {
                ReportComparisonStatus(game.Catalog.GetString("Only saved paths in the current route can be compared."), true);
                PublishSavedPathComparisons();
                return;
            }
            Color color = previous?.IsSelected == true ? previous.Color
                : comparisonPalette.FirstOrDefault(candidate => !comparisons.Values.Any(request => request.IsSelected && request.Color == candidate));
            if (color == default)
            {
                ReportComparisonStatus(game.Catalog.GetString("At most six saved paths can be compared. Uncheck a comparison before adding another."), true);
                PublishSavedPathComparisons();
                return;
            }
            ComparisonRequest request = new ComparisonRequest(++comparisonRequestVersion, color, CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token));
            comparisons[header.Id] = request;
            try
            {
                PublishSavedPathComparisons();
                SavedPathComparison prepared = await Task.Run(async () =>
                {
                    PathModel saved = await route.PathModelUncached(header.Id, request.Cancellation.Token).ConfigureAwait(false);
                    return saved == null || !string.Equals(saved.Id, header.Id, StringComparison.OrdinalIgnoreCase)
                        ? throw new InvalidOperationException("Saved comparison model is missing or has a different identity.")
                        : SavedPathComparison.Create(saved, ((ITrackNodeInfoContext)content).TrackWorld, color, request.Cancellation.Token);
                }, request.Cancellation.Token).ConfigureAwait(false);
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (IsCurrentComparison(route, content, routeVersion, header.Id, request))
                    {
                        request.IsLoading = false;
                        content.SetSavedPathComparison(prepared);
                        PublishSavedPathComparisons();
                        ReportComparisonStatus(game.Catalog.GetString("Saved path comparison displayed."), false);
                    }
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
            {
                await FailComparisonAsync(route, content, routeVersion, header.Id, request,
                    game.Catalog.GetString("Saved path comparison was canceled. Check the saved path again to retry.")).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or MemoryPack.MemoryPackSerializationException)
            {
                Trace.TraceWarning($"Saved path comparison '{header.Id}' failed: {error}");
                string message = error.Message switch
                {
                    SavedPathComparison.UnresolvedMessage => game.Catalog.GetString("Saved path comparison requires an unambiguous, resolved main and passing route. Open the saved path and repair or select its route, then save again."),
                    SavedPathComparison.MissingGeometryMessage => game.Catalog.GetString("Saved path comparison could not match its selected track geometry. Validate the route and saved path before comparing again."),
                    _ => game.Catalog.GetString("The saved path could not be loaded for comparison. Verify the native saved model and access permissions, then try again."),
                };
                await FailComparisonAsync(route, content, routeVersion, header.Id, request, message).ConfigureAwait(false);
            }
            catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
            {
                Trace.TraceError($"Saved path comparison '{header.Id}' failed unexpectedly: {error}");
                await FailComparisonAsync(route, content, routeVersion, header.Id, request,
                    game.Catalog.GetString("The saved path could not be loaded for comparison. Verify the native saved model and access permissions, then try again.")).ConfigureAwait(false);
                throw;
            }
            finally
            {
                await game.InvokeOnGameThreadAsync(() =>
                {
                    // Cancellation and disposal share the owner, including stale and faulted requests.
                    request.IsLoading = false;
                    request.Cancellation.Dispose();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }

        private bool IsCurrentComparison(RouteModelHeader route, ToolboxContent content, long routeVersion, string id, ComparisonRequest request)
            => !IsStopping && routeVersion == routeContentVersion && ReferenceEquals(route, selectedRoute) && ReferenceEquals(content, toolboxContent)
                && comparisons.TryGetValue(id, out ComparisonRequest current) && current.Version == request.Version;

        private Task FailComparisonAsync(RouteModelHeader route, ToolboxContent content, long routeVersion, string id, ComparisonRequest request, string message)
            => game.InvokeOnGameThreadAsync(() =>
            {
                request.IsLoading = false;
                if (IsCurrentComparison(route, content, routeVersion, id, request))
                {
                    request.IsSelected = false;
                    request.Error = message;
                    content.ClearSavedPathComparison(id);
                    PublishSavedPathComparisons();
                    ReportComparisonStatus(message, true);
                }
                return Task.CompletedTask;
            });

        private void ReportComparisonStatus(string message, bool warning) => game.HostedServices?.TrainPathToolWindow?.ReportComparisonStatus(message, warning);

        private void PublishSavedPathComparisons()
        {
            ImmutableArray<TrainPathComparisonRow> rows = menuPaths.Select(path =>
            {
                comparisons.TryGetValue(path.Id, out ComparisonRequest request);
                Color color = request?.IsSelected == true ? request.Color : default;
                return new TrainPathComparisonRow(path.Id, path.Name, routeContentVersion, request?.IsSelected == true, request?.IsLoading == true, request?.Error,
                    request?.IsSelected == true ? DrawingColor.FromArgb(color.A, color.R, color.G, color.B) : DrawingColor.Empty);
            }).ToImmutableArray();
            game.HostedServices?.TrainPathToolWindow?.UpdateComparisons(rows);
        }

        private void ClearSavedPathComparisons()
        {
            foreach (ComparisonRequest request in comparisons.Values)
                request.CancelIfLoading();
            comparisons.Clear();
            toolboxContent?.ClearSavedPathComparisons();
            game.HostedServices?.TrainPathToolWindow?.UpdateComparisons(ImmutableArray<TrainPathComparisonRow>.Empty);
        }

        private Task RefreshSavedPathComparisonAfterSaveAsync(RouteModelHeader route, long version, string savedId)
            => game.InvokeOnGameThreadAsync(() =>
            {
                return !IsStopping && version == routeContentVersion && ReferenceEquals(route, selectedRoute)
                    && comparisons.TryGetValue(savedId, out ComparisonRequest request) && request.IsSelected
                    ? SetSavedPathComparisonCoreAsync(savedId, true)
                    : Task.CompletedTask;
            });

        private sealed class ComparisonRequest
        {
            /// <summary>Monotonic per-request witness, invalidated by removal even if the same ID is checked again.</summary>
            internal long Version { get; }
            /// <summary>Reserved route-local map and legend color.</summary>
            internal Color Color { get; }
            /// <summary>Cancellation of the private saved read and preparation, not a shared cache operation.</summary>
            internal CancellationTokenSource Cancellation { get; }
            internal bool IsSelected { get; set; } = true;
            internal bool IsLoading { get; set; } = true;
            internal string Error { get; set; }

            internal ComparisonRequest(long version, Color color, CancellationTokenSource cancellation)
            {
                Version = version;
                Color = color;
                Cancellation = cancellation;
            }

            internal void CancelIfLoading()
            {
                if (IsLoading)
                    Cancellation.Cancel();
            }
        }
    }
}
