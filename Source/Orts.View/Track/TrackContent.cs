using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.View.Track.Widgets;

namespace Orts.View.Track
{
    public class TrackContent
    {

        private TrackDB trackDB;
        private TrackSectionsFile trackSectionsFile;

        internal TileIndexedList<TrackSegment, Tile> TrackSegments { get; private set; }

        internal TileIndexedList<TrackEndSegment, Tile> TrackEndSegments { get; private set; }
        internal TileIndexedList<JunctionSegment, Tile> JunctionSegments { get; private set; }

        internal TileIndexedList<TrackItemBase, Tile> TrackItems { get; private set; }

        internal TileIndexedList<GridTile, Tile> Tiles { get; private set; }

        internal SignalConfigurationFile SignalConfigFile { get; }
        public bool UseMetricUnits { get; }

        public Rectangle Bounds { get; private set; }
        private readonly RoadTrackDB roadTrackDB;

        public TrackContent(TrackDB trackDB, TrackSectionsFile trackSections, SignalConfigurationFile signalConfig, bool metricUnits)
        {
            this.trackDB = trackDB;
            trackSectionsFile = trackSections;
            UseMetricUnits = metricUnits;
            SignalConfigFile = signalConfig;
        }

        public async Task Initialize()
        {
            List<Task> initializer = new List<Task>
            {
                Task.Run(async () => await InitializeTrackSegments().ConfigureAwait(false))
            };

            await Task.WhenAll(initializer).ConfigureAwait(false);
            trackDB = null;
            trackSectionsFile = null;
        }

        private async Task InitializeTrackSegments()
        {
            List<Task> renderItems = new List<Task>
            {
                Task.Run(() => AddTrackSegments()),
                Task.Run(() => AddTrackItems(trackDB.TrackItems)),
            };

            await Task.WhenAll(renderItems).ConfigureAwait(false);
        }

        private void AddTrackSegments()
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            List<TrackSegment> trackSegments = new List<TrackSegment>();
            List<TrackEndSegment> endSegments = new List<TrackEndSegment>();
            List<JunctionSegment> junctionSegments = new List<JunctionSegment>();

            foreach (TrackNode trackNode in trackDB.TrackNodes)
            {
                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = trackDB.TrackNodes[trackEndNode.TrackPins[0].Link] as TrackVectorNode;
                        endSegments.Add(new TrackEndSegment(trackEndNode, connectedVectorNode, trackSectionsFile.TrackSections));
                        break;
                    case TrackVectorNode trackVectorNode:
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            TrackSection trackSection = trackSectionsFile.TrackSections.Get(trackVectorSection.SectionIndex);
                            if (trackSection != null)
                                trackSegments.Add(new TrackSegment(trackVectorSection, trackSection));
                        }
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        foreach (TrackPin pin in trackJunctionNode.TrackPins)
                        {
                            if (trackDB.TrackNodes[pin.Link] is TrackVectorNode vectorNode && vectorNode.TrackVectorSections.Length > 0)
                            {
                                TrackVectorSection item = pin.Direction == Common.TrackDirection.Reverse ? vectorNode.TrackVectorSections.First() : vectorNode.TrackVectorSections.Last();
                            }
                        }
                        junctionSegments.Add(new JunctionSegment(trackJunctionNode));
                        break;
                }
            }
            //            Bounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX + 1), (int)(maxY - minY + 1));

            TrackSegments = new TileIndexedList<TrackSegment, Tile>(trackSegments);
            JunctionSegments = new TileIndexedList<JunctionSegment, Tile>(junctionSegments);
            TrackEndSegments = new TileIndexedList<TrackEndSegment, Tile>(endSegments);
            Tiles = new TileIndexedList<GridTile, Tile>(trackSegments.Select(d => d.Tile as ITile).Distinct().Select(t => new GridTile(t)));

            if (Tiles.Count == 1)
            {
                foreach (TrackEndSegment trackEndSegment in TrackEndSegments)
                {
                    minX = Math.Min(minX, trackEndSegment.Location.X);
                    minY = Math.Min(minY, trackEndSegment.Location.Y);
                    maxX = Math.Max(maxX, trackEndSegment.Location.X);
                    maxY = Math.Max(maxY, trackEndSegment.Location.Y);
                }
            }
            else
            {
                foreach (GridTile tile in Tiles)
                {
                    minX = Math.Min(minX, tile.Tile.X);
                    minY = Math.Min(minY, tile.Tile.Z);
                    maxX = Math.Max(maxX, tile.Tile.X);
                    maxY = Math.Max(maxY, tile.Tile.Z);
                }
                minX = minX * WorldLocation.TileSize - WorldLocation.TileSize / 2;
                maxX = maxX * WorldLocation.TileSize + WorldLocation.TileSize / 2;
                minY = minY * WorldLocation.TileSize - WorldLocation.TileSize / 2;
                maxY = maxY * WorldLocation.TileSize + WorldLocation.TileSize / 2;
            }
            Bounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));

        }

        private void AddTrackItems(TrackItem[] trackItems)
        {
            TrackItems = new TileIndexedList<TrackItemBase, Tile>(TrackItemBase.Create(trackItems, SignalConfigFile, trackDB, trackSectionsFile));
        }
    }
}
