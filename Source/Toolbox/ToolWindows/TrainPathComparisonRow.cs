using System.Drawing;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>Immutable route-scoped saved comparison state, separate from serialized models and editor selection.</summary>
    internal sealed record TrainPathComparisonRow
    {
        /// <summary>Saved path identity in the selected route.</summary>
        public string Id { get; init; }
        /// <summary>Name from the saved path header, not a transient editor model.</summary>
        public string Name { get; init; }
        /// <summary>Route/content revision that scopes queued UI commands even when IDs are reused in another folder.</summary>
        public long RouteContentVersion { get; init; }
        /// <summary>Whether this saved baseline is requested for independent display.</summary>
        public bool IsSelected { get; init; }
        /// <summary>Whether saved I/O or geometry preparation is still running.</summary>
        public bool IsLoading { get; init; }
        /// <summary>Localized actionable failure, or null when no failure occurred.</summary>
        public string Error { get; init; }
        /// <summary>Assigned map and legend color; empty when not selected.</summary>
        public Color Color { get; init; }

        /// <summary>Initializes one saved comparison row.</summary>
        public TrainPathComparisonRow(string id, string name, long routeContentVersion, bool isSelected, bool isLoading, string error, Color color)
        {
            Id = id;
            Name = name;
            RouteContentVersion = routeContentVersion;
            IsSelected = isSelected;
            IsLoading = isLoading;
            Error = error;
            Color = color;
        }
    }
}
