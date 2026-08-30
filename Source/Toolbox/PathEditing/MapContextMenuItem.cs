using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>
    /// A single entry of the map surface context menu, resolved on the game thread. Captions are applied by the
    /// shell so they can be localized.
    /// </summary>
    internal sealed record MapContextMenuItem
    {
        /// <summary>Action to apply when the entry is selected.</summary>
        public MapContextMenuAction Action { get; init; }

        /// <summary>Authored path node the action applies to, or -1 for path-scoped actions.</summary>
        public int NodeIndex { get; init; }

        /// <summary>Route candidate index for <see cref="MapContextMenuAction.SelectRouteCandidate"/>, otherwise -1.</summary>
        public int CandidateIndex { get; init; }

        /// <summary>Optional detail appended to the caption, used for route candidate descriptions.</summary>
        public string Detail { get; init; }

        /// <summary>Track anchor selected for placement-oriented actions, or <see langword="null"/>.</summary>
        public PathNode PlacementAnchor { get; init; }

        /// <summary>Whether this entry is a visual separator rather than a command.</summary>
        public bool IsSeparator => Action == MapContextMenuAction.Separator;

        /// <summary>A visual separator between menu sections.</summary>
        public static MapContextMenuItem Separator { get; } = new MapContextMenuItem(MapContextMenuAction.Separator);

        /// <summary>
        /// Initializes a new instance of the <see cref="MapContextMenuItem"/> record for a path-scoped action.
        /// </summary>
        public MapContextMenuItem(MapContextMenuAction action)
            : this(action, -1, -1, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapContextMenuItem"/> record for a node or span action.
        /// </summary>
        public MapContextMenuItem(MapContextMenuAction action, int nodeIndex)
            : this(action, nodeIndex, -1, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapContextMenuItem"/> record.
        /// </summary>
        public MapContextMenuItem(MapContextMenuAction action, int nodeIndex, int candidateIndex, string detail)
        {
            Action = action;
            NodeIndex = nodeIndex;
            CandidateIndex = candidateIndex;
            Detail = detail;
        }
    }
}
