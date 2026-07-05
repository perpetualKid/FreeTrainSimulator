using System;
using System.Collections.Immutable;
using System.Drawing;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Toolbox.PopupWindows;
using FreeTrainSimulator.Toolbox.Settings;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Hosted-mode bridge exposing read-only map location data for a dockable WPF location window.
    /// Uses the same pull/snapshot model as other hosted tool windows.
    /// </summary>
    internal sealed class LocationToolWindow : IToolboxToolWindow
    {
        private readonly ProfileToolboxSettingsModel toolboxSettings;
        private IMapLocationContext locationContext;
        private volatile ToolWindowSnapshot snapshot = ToolWindowSnapshot.Empty;
        private volatile bool active;
        private PointD previousWorldPoint = PointD.None;
        private bool updateRequired = true;

        internal LocationToolWindow(ProfileToolboxSettingsModel toolboxSettings)
        {
            this.toolboxSettings = toolboxSettings ?? throw new ArgumentNullException(nameof(toolboxSettings));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.LocationWindow;

        public string Title => UseWorldCoordinates ? "World Coordinates" : "Tile Coordinates";

        public bool Active
        {
            get => active;
            set => active = value;
        }

        internal bool UseWorldCoordinates
        {
            get => toolboxSettings.ToolWindowSettings.LocationToolSettings.UseWorldCoordinates;
            set
            {
                if (value == UseWorldCoordinates)
                    return;

                toolboxSettings.ToolWindowSettings = toolboxSettings.ToolWindowSettings with
                {
                    LocationToolSettings = toolboxSettings.ToolWindowSettings.LocationToolSettings with { UseWorldCoordinates = value }
                };
                updateRequired = true;
            }
        }

        internal void ToggleCoordinateMode()
        {
            UseWorldCoordinates = !UseWorldCoordinates;
        }

        internal void UpdateLocationContext(IMapLocationContext context)
        {
            locationContext = context;
            updateRequired = true;
        }

        public ToolWindowSnapshot CaptureSnapshot() => snapshot;

        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            PointD worldPoint = locationContext?.WorldPosition ?? PointD.None;
            if (!updateRequired && previousWorldPoint == worldPoint)
                return;

            previousWorldPoint = worldPoint;
            updateRequired = false;

            ImmutableArray<ToolWindowRow>.Builder rows = ImmutableArray.CreateBuilder<ToolWindowRow>();

            WorldLocation location = PointD.ToWorldLocation(worldPoint);
            if (UseWorldCoordinates)
            {
                (double latitude, double longitude) = EarthCoordinates.ConvertWTC(location);
                (string latitudeText, string longitudeText) = EarthCoordinates.ToString(latitude, longitude);
                rows.Add(new ToolWindowRow { Name = "Coordinates", Value = $"{latitudeText} {longitudeText}", Color = Color.Orange });
            }
            else
            {
                rows.Add(new ToolWindowRow { Name = "Tile (X:Z)", Value = $"{location.Tile.X}:{location.Tile.Z}", Color = Color.Orange });
                rows.Add(new ToolWindowRow { Name = "Location (x, z)", Value = $"{location.Location.X,4:00.##} {location.Location.Z,4:00.##}", Color = Color.Orange });
            }

            snapshot = new ToolWindowSnapshot { Rows = rows.ToImmutable() };
        }
    }
}
