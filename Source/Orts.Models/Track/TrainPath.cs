using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
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
#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainPathItem> PathItems { get; } = new List<TrainPathItem>();
#pragma warning restore CA1002 // Do not expose generic lists

        public TrainPath(PathFile pathFile, Game game)
        {
            ArgumentNullException.ThrowIfNull(pathFile);
            ArgumentNullException.ThrowIfNull(game);

            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel trackModel = TrackModel.Instance<RailTrackModel>(game);

            PathNode previousNode = null;

            foreach (PathNode node in pathFile.PathNodes)
            {
                PathItems.Add(new TrainPathItem(node));

                PointD nodeLocation = PointD.FromWorldLocation(node.Location);


                if (node.NextMainNode > -1)
                {
                    if (previousNode == null || node.NextMainNode > previousNode.NextMainNode)
                        previousNode = node;
                }
                else // end node, but not necessarily the last node in the list
                {
                }
            }
        }
    }
}
