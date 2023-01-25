using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace Orts.Models.Track
{
    /// <summary>
    /// A train's path as definied in <see cref="PathFile"> path file, enriched with information about the actual track layout 
    /// (i.e. whether path points are on actual track or else considered invalid)
    /// </summary>
    public class TrainPath
    {
        public string FilePath { get; }
        public bool Invalid { get; set; }
#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathPoint> PathItems { get; } = new List<TrainPathPoint>();
#pragma warning restore CA1002 // Do not expose generic lists

        public PathFile PathFile { get; }

        public TrainPath(PathFile pathFile, string filePath, Game game)
        {
            ArgumentNullException.ThrowIfNull(pathFile);
            ArgumentNullException.ThrowIfNull(game);

            TrackModel trackModel = TrackModel.Instance(game);
            PathFile = pathFile;
            FilePath = filePath;
            TrainPathPoint beforeEndNode = null;

            PathItems.AddRange(pathFile.PathNodes.Select(node => new TrainPathPoint(node, trackModel)));

            //linking path item nodes to their next path item node
            //on the end node, set to the previous (inbound) node instead, required for TrainPathItem direction/alignment
            //nb: inbound to the end node may not need to be the node just before in the list, so as we iterate the list, 
            //we keep a reference to the one which has the end node as successor
            //it's assumed that passing paths will reconnct to main node, and not ending on it's own
            foreach (TrainPathPoint node in PathItems)
            {
                if (node.PathNode.NextMainNode != -1)
                {
                    node.NextMainItem = PathItems[node.PathNode.NextMainNode];
                    if (node.NextMainItem.NodeType == PathNodeType.End)
                        beforeEndNode = node;
                }
                else if (node.NodeType == PathNodeType.End)
                    node.NextMainItem = beforeEndNode;

                if (node.PathNode.NextSidingNode != -1)
                    node.NextSidingItem = PathItems[node.PathNode.NextSidingNode];
            }
        }
    }
}
