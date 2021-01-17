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

        internal List<TrackSegment> TrackSegments { get; } = new List<TrackSegment>();
        internal List<TrackEndSegment> TrackEndNodes { get; } = new List<TrackEndSegment>();
        internal List<JunctionNode> JunctionNodes { get; } = new List<JunctionNode>();

        internal List<TrackItemBase> TrackItems { get; } = new List<TrackItemBase>();

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
                            TrackSection trackSection = trackSectionsFile.TrackSections.Get(trackVectorSection.SectionIndex);
                            if (trackSection != null)
                                TrackSegments.Add(new TrackSegment(trackVectorSection, trackSection));
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

        private void AddTrackItems(TrackItem[] trackItems)
        {
            TrackItems.AddRange(TrackItemBase.Create(trackItems, SignalConfigFile, trackDB));
        }
    }
}
