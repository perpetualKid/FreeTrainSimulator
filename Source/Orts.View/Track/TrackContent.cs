using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

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

        internal List<TrackSegment> TrackSegments { get; } = new List<TrackSegment>();
        internal List<TrackEndSegment> TrackEndNodes { get; } = new List<TrackEndSegment>();
        internal List<JunctionNode> JunctionNodes { get; } = new List<JunctionNode>();


        public bool UseMetricUnits { get; }

        public Rectangle Bounds { get; private set; }
        private readonly RoadTrackDB roadTrackDB;
        private readonly SignalConfigurationFile signalConfig;

        public TrackContent(TrackDB trackDB, TrackSectionsFile trackSections, bool metricUnits)
        {
            this.trackDB = trackDB;
            trackSectionsFile = trackSections;
            UseMetricUnits = metricUnits;
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

            /// update bounds 
            void UpdateBounds(in WorldLocation location)
            {
                minX = Math.Min(minX, location.TileX * WorldLocation.TileSize + location.Location.X);
                minY = Math.Min(minY, location.TileZ * WorldLocation.TileSize + location.Location.Z);
                maxX = Math.Max(maxX, location.TileX * WorldLocation.TileSize + location.Location.X);
                maxY = Math.Max(maxY, location.TileZ * WorldLocation.TileSize + location.Location.Z);
            }

            foreach (TrackNode trackNode in trackDB.TrackNodes)
            {
                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = trackDB.TrackNodes[trackEndNode.TrackPins[0].Link] as TrackVectorNode;
                        TrackEndNodes.Add(new TrackEndSegment(trackEndNode, connectedVectorNode, trackSectionsFile.TrackSections));
                        UpdateBounds(in trackEndNode.UiD.Location);
                        break;
                    case TrackVectorNode trackVectorNode:
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            UpdateBounds(in trackVectorSection.Location);
                            TrackSegments.Add(new TrackSegment(trackVectorSection, trackSectionsFile.TrackSections));
                        }
                        if (trackVectorNode.TrackVectorSections.Length > 1)
                        {
                            for (int i = 0; i < trackVectorNode.TrackVectorSections.Length - 1; i++)
                            {
                                ref readonly WorldLocation start = ref trackVectorNode.TrackVectorSections[i].Location;
                                UpdateBounds(start);
                                ref readonly WorldLocation end = ref trackVectorNode.TrackVectorSections[i + 1].Location;
                                UpdateBounds(end);
                            }
                        }
                        else
                        {
                            TrackVectorSection section = trackVectorNode.TrackVectorSections[0];
                            UpdateBounds(section.Location);

                            foreach (TrackPin pin in trackVectorNode.TrackPins)
                            {
                                TrackNode connectedNode = trackDB.TrackNodes[pin.Link];
                                UpdateBounds(connectedNode.UiD.Location);
                            }
                        }
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        foreach (TrackPin pin in trackJunctionNode.TrackPins)
                        {
                            if (trackDB.TrackNodes[pin.Link] is TrackVectorNode vectorNode && vectorNode.TrackVectorSections.Length > 0)
                            {
                                TrackVectorSection item = pin.Direction == Common.TrackDirection.Reverse ? vectorNode.TrackVectorSections.First() : vectorNode.TrackVectorSections.Last();
                                UpdateBounds(item.Location);
                            }
                        }
                        UpdateBounds(trackJunctionNode.UiD.Location);
                        JunctionNodes.Add(new JunctionNode(trackJunctionNode));
                        break;
                }
            }
            Bounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX + 1), (int)(maxY - minY + 1));
        }

        private void AddTrackItems(IEnumerable<TrackItem> trackItems)
        {
            foreach (TrackItem trackItem in trackItems)
            {
                switch (trackItem.TrackItemId)
                { }
            }
        }
    }
}
