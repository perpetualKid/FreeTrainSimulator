using System.Collections.Immutable;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Hosted-mode bridge that builds the main-window status bar content on the game thread. Mirrors the
    /// pull/snapshot model of the other hosted tool windows: <see cref="RefreshSnapshot"/> runs on the game
    /// thread each frame and publishes an immutable <see cref="StatusBarSnapshot"/> that the WPF view model
    /// reads lock-free through <see cref="CaptureSnapshot"/>.
    /// <para>
    /// The status bar surfaces mouse-driven map information: tile/location coordinates from the map location
    /// context, and the nearest track node and track item from their respective information providers. The
    /// snapshot is an ordered list of <see cref="StatusBarField"/>s, so additional fields can be added later
    /// simply by appending to the builder here without touching the WPF rendering.
    /// </para>
    /// </summary>
    internal sealed class StatusBarToolWindow
    {
        private const string NodeIndexKey = "Node Index";
        private const string ItemTypeKey = "Item Type";
        private const string ItemIndexKey = "Item Index";

        private IMapLocationContext locationContext;
        private ITrackNodeInfoContext trackNodeInfoContext;
        private ITrackItemInfoContext trackItemInfoContext;
        private volatile StatusBarSnapshot snapshot = StatusBarSnapshot.Empty;

        /// <summary>Latest status bar content; safe to read from the WPF UI thread.</summary>
        public StatusBarSnapshot CaptureSnapshot() => snapshot;

        /// <summary>
        /// Updates the active map contexts. Called on the game thread when the content area changes (route
        /// loaded/unloaded); null contexts clear the corresponding fields.
        /// </summary>
        internal void UpdateContexts(IMapLocationContext location, ITrackNodeInfoContext trackNode, ITrackItemInfoContext trackItem)
        {
            locationContext = location;
            trackNodeInfoContext = trackNode;
            trackItemInfoContext = trackItem;
        }

        /// <summary>
        /// Rebuilds the immutable snapshot from the current contexts. Must be called on the game thread,
        /// because the underlying providers are mutated there each frame.
        /// </summary>
        internal void RefreshSnapshot()
        {
            ImmutableArray<StatusBarField>.Builder fields = ImmutableArray.CreateBuilder<StatusBarField>();

            AppendLocationFields(fields);
            AppendTrackNodeFields(fields);
            AppendTrackItemFields(fields);

            snapshot = new StatusBarSnapshot { Fields = fields.ToImmutable() };
        }

        private void AppendLocationFields(ImmutableArray<StatusBarField>.Builder fields)
        {
            PointD worldPoint = locationContext?.WorldPosition ?? PointD.None;
            WorldLocation location = PointD.ToWorldLocation(worldPoint);

            fields.Add(new StatusBarField { Key = "Tile", Value = $"{location.Tile.X}, {location.Tile.Z}" });
            fields.Add(new StatusBarField { Key = "LocationX", Value = $"{location.Location.X,4:F1}" });
            fields.Add(new StatusBarField { Key = "LocationZ", Value = $"{location.Location.Z,4:F1}" });
        }

        private void AppendTrackNodeFields(ImmutableArray<StatusBarField>.Builder fields)
        {
            string nodeIndex = ReadValue(trackNodeInfoContext?.TrackNodeInfo, NodeIndexKey);
            fields.Add(new StatusBarField { Key = "TrackNode", Label = "Track", Value = nodeIndex });
        }

        private void AppendTrackItemFields(ImmutableArray<StatusBarField>.Builder fields)
        {
            INameValueInformationProvider trackItemInfo = trackItemInfoContext?.TrackItemInfo;
            string itemType = ReadValue(trackItemInfo, ItemTypeKey);
            string itemIndex = ReadValue(trackItemInfo, ItemIndexKey);

            string itemValue = string.IsNullOrEmpty(itemType)
                ? itemIndex
                : string.IsNullOrEmpty(itemIndex) ? itemType : $"{itemType} {itemIndex}";

            fields.Add(new StatusBarField { Key = "TrackItem", Label = "Item", Value = itemValue });
        }

        // Reads a single value from an information provider's detail dictionary. The dictionary indexer
        // returns null for missing keys (it does not throw), so a null provider/key simply yields null.
        private static string ReadValue(INameValueInformationProvider provider, string key)
        {
            return provider?.DetailInfo?[key];
        }
    }
}
