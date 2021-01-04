using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.View.Track
{
    public class TrackContent
    {

        public TrackDB TrackDB { get; }

        public bool UseMetricUnits { get; }

        public Rectangle Bounds { get; private set; }
        private readonly RoadTrackDB roadTrackDB;
        private readonly TrackSectionsFile trackSections;
        private readonly SignalConfigurationFile signalConfig;

        public TrackContent(TrackDB trackDB, bool metricUnits)
        {
            TrackDB = trackDB;
            UseMetricUnits = metricUnits;
        }

        public async Task Initialize()
        {
            List<Task> initializer = new List<Task>
            {
                Task.Run(async () => await InitializeTrackSegments().ConfigureAwait(false))
            };

            await Task.WhenAll(initializer).ConfigureAwait(false);

        }

        private async Task InitializeTrackSegments()
        {
            List<Task> renderItems = new List<Task>
            {
                Task.Run(() => AddTrackSegments()),
//                Task.Run(() => AddTrackItems()),
            };

            await Task.WhenAll(renderItems).ConfigureAwait(false);
        }

        private Task AddTrackSegments()
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

            foreach (TrackNode trackNode in TrackDB.TrackNodes)
            {
                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = TrackDB.TrackNodes[trackEndNode.TrackPins[0].Link] as TrackVectorNode;
                        UpdateBounds(trackEndNode.UiD.Location);
                        break;
                    case TrackVectorNode trackVectorNode:
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

                            foreach (TrackPin pin in trackVectorNode.TrackPins)
                            {
                                TrackNode connectedNode = TrackDB.TrackNodes[pin.Link];
                                UpdateBounds(section.Location);
                                UpdateBounds(connectedNode.UiD.Location);
                            }
                        }
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        foreach (TrackPin pin in trackJunctionNode.TrackPins)
                        {
                            if (TrackDB.TrackNodes[pin.Link] is TrackVectorNode vectorNode && vectorNode.TrackVectorSections.Length > 0)
                            {
                                TrackVectorSection item = pin.Direction == Common.TrackDirection.Reverse ? vectorNode.TrackVectorSections.First() : vectorNode.TrackVectorSections.Last();
                                UpdateBounds(item.Location);
                                UpdateBounds(trackJunctionNode.UiD.Location);

                            }
                        }
                        break;
                }
            }
            Bounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX + 1), (int)(maxY - minY + 1));
            return Task.CompletedTask;
        }
    }
}
