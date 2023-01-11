using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    /// <summary>
    /// A train's path as definied in <see cref="PathFile"> path file, enriched with information about the actual track layout 
    /// (i.e. whether path points are on actual track or else considered invalid)
    /// </summary>
    public class TrainPath
    {
        public bool Invalid { get; set; }
#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathItem> PathItems { get; } = new List<TrainPathItem>();
#pragma warning restore CA1002 // Do not expose generic lists

        public TrainPath(PathFile pathFile, Game game)
        {
            ArgumentNullException.ThrowIfNull(pathFile);
            ArgumentNullException.ThrowIfNull(game);

            TrackModel trackModel = TrackModel.Instance<RailTrackModel>(game);

            TrainPathItem previousNode = null;
            TrainPathItem endNode = null;

            foreach (PathNode node in pathFile.PathNodes)
            {
                PathItems.Add(new TrainPathItem(node, trackModel));
                //finding the node which connects to the end node, primarily used to get the inbound direction on end node
                //end node is not necessarily the last node in the list, hence we rather look which one is inbound 
                if (node.NextMainNode > -1 && (previousNode == null || node.NextMainNode > previousNode.PathNode.NextMainNode))
                    previousNode = PathItems[^1];
                if (node.NodeType == PathNodeType.End)
                    endNode = PathItems[^1];
            }
            if (endNode == null)
            {
                Trace.TraceWarning("Path has no explicit end node. May indicate a loop.");
                Invalid = true;
            }
            else
                endNode.NextMainItem = previousNode;

            //set the previous node on the end node, required for TrainPathItem direction/alignment
            foreach (TrainPathItem node in PathItems)
            {
                if (node.PathNode.NextMainNode != -1)
                    node.NextMainItem = PathItems[node.PathNode.NextMainNode];
            }
        }
    }
}
