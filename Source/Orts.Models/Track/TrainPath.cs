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

            PathItems.AddRange(pathFile.PathNodes.Select(node => new TrainPathPoint(node, trackModel)));
            TrainPathPoint.LinkPathPoints(PathItems);
        }
    }
}
