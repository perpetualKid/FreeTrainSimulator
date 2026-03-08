using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record TrackNodeConnector
    {
        public int NodeIndex { get; init; }
        public ConnectorType ConnectorType { get; init; }
        /// <summary>Index of the tracknode connected to the parent of this pin</summary>
        public int Link { get; init; }
        /// <summary>In case a connection is made to a vector node this determines the side of the vector node that is connected to</summary>
        public TrackDirection Direction { get; init; }
    }
}
