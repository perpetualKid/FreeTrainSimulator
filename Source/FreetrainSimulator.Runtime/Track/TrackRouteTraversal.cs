namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>A physical traversal between track nodes through a specific source connector.</summary>
    internal readonly record struct TrackRouteTraversal
    {
        /// <summary>Index of the track node where the traversal begins.</summary>
        internal int FromNodeIndex { get; init; }

        /// <summary>Index of the connector used at the source track node.</summary>
        internal int FromConnectorIndex { get; init; }

        /// <summary>Index of the track node where the traversal ends.</summary>
        internal int ToNodeIndex { get; init; }

        /// <summary>Initializes a new instance of the <see cref="TrackRouteTraversal"/> record struct.</summary>
        internal TrackRouteTraversal(int fromNodeIndex, int fromConnectorIndex, int toNodeIndex)
        {
            FromNodeIndex = fromNodeIndex;
            FromConnectorIndex = fromConnectorIndex;
            ToNodeIndex = toNodeIndex;
        }
    }
}
