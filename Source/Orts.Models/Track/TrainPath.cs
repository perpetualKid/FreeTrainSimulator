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

            foreach (PathNode node in pathFile.PathNodes)
            {
                PathItems.Add(new TrainPathItem(node, trackModel));
                //finding the node which connects to the end node, primarily used to get the inbound direction on end node
                //end node is not necessarily the last node in the list, hence we rather look which one is inbound 
                if (node.NodeType == PathNodeType.End)
                {
                    PathItems[^1].NextMainItem = PathItems[^2];
                }
            }

            //set the previous node on the end node, required for TrainPathItem direction/alignment
            foreach (TrainPathItem node in PathItems)
            {
                if (node.PathNode.NextMainNode != -1)
                    node.NextMainItem = PathItems[node.PathNode.NextMainNode];
                if (node.PathNode.NextSidingNode != -1)
                    node.NextSidingItem = PathItems[node.PathNode.NextSidingNode];
            }
        }
    }
}
