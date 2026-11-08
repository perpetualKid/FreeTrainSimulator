using System;
using System.Collections.Immutable;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>
    /// Describes a request to show the map context menu, raised by the hosted game after hit testing the
    /// pointer position against the current train path. Positions are in map surface client pixels.
    /// </summary>
    internal sealed class MapContextMenuRequestedEventArgs : EventArgs
    {
        private readonly PathEditorMenuRequest request;

        /// <summary>Pointer x position in map surface client pixels.</summary>
        public int X { get; }

        /// <summary>Pointer y position in map surface client pixels.</summary>
        public int Y { get; }

        /// <summary>Authored path node index under the pointer, or -1 when no node was hit.</summary>
        public int NodeIndex { get; }

        /// <summary>Menu entries available at the pointer position, in display order.</summary>
        public ImmutableArray<MapContextMenuItem> Items => request.Items;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapContextMenuRequestedEventArgs"/> class.
        /// </summary>
        public MapContextMenuRequestedEventArgs(int x, int y, int nodeIndex, ImmutableArray<MapContextMenuItem> items, PathEditor owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            request = new PathEditorMenuRequest(items, owner);
            X = x;
            Y = y;
            NodeIndex = nodeIndex;
        }

        /// <summary>Checks menu-construction ownership on the game thread before dispatching any action.</summary>
        internal bool IsCurrent(PathEditor editor)
            => request.IsCurrent(editor);
    }
}
