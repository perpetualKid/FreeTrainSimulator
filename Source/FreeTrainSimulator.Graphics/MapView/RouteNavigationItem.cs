namespace FreeTrainSimulator.Graphics.MapView
{
    /// <summary>
    /// Identifies the kind of named route entity targeted by a route-navigation action.
    /// </summary>
    public enum RouteNavigationKind
    {
        /// <summary>A station, identified by name and centered on its aggregated platform extent.</summary>
        Station,

        /// <summary>A single platform within a station.</summary>
        Platform,

        /// <summary>A single siding.</summary>
        Siding,
    }

    /// <summary>
    /// A selectable route-navigation entry (station, platform, or siding) exposed to the WPF shell so it can
    /// list and center the map on named route entities without referencing the internal map widget types.
    /// </summary>
    public sealed record RouteNavigationItem
    {
        /// <summary>Stable index of this entry within its kind-specific list, used to request navigation.</summary>
        public int Index { get; init; }

        /// <summary>Display name of the entity (station, platform, or siding name).</summary>
        public string Name { get; init; }

        /// <summary>
        /// Grouping label: the owning station name for platforms, the nearest station name for sidings, or
        /// <see langword="null"/> for stations themselves.
        /// </summary>
        public string GroupName { get; init; }

        public RouteNavigationItem(int index, string name, string groupName)
        {
            Index = index;
            Name = name;
            GroupName = groupName;
        }
    }
}
