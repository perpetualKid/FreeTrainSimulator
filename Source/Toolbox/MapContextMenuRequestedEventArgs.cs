using System;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Describes a request to show the map context menu, raised by the hosted game after hit testing the
    /// pointer position against the current train path. Positions are in map surface client pixels.
    /// </summary>
    internal sealed class MapContextMenuRequestedEventArgs : EventArgs
    {
        /// <summary>Pointer x position in map surface client pixels.</summary>
        public int X { get; }

        /// <summary>Pointer y position in map surface client pixels.</summary>
        public int Y { get; }

        /// <summary>Authored path node index under the pointer, or -1 when no node was hit.</summary>
        public int NodeIndex { get; }

        /// <summary>Indicates whether the node under the pointer can be moved.</summary>
        public bool CanMoveNode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MapContextMenuRequestedEventArgs"/> class.
        /// </summary>
        public MapContextMenuRequestedEventArgs(int x, int y, int nodeIndex, bool canMoveNode)
        {
            X = x;
            Y = y;
            NodeIndex = nodeIndex;
            CanMoveNode = canMoveNode;
        }
    }
}
