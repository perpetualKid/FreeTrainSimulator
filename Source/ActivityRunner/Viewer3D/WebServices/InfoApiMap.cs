// COPYRIGHT 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    /// <summary>
    /// Class to store the latitude and longitude of a position on the webpage map
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct LatLon
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly float Lat;
        public readonly float Lon;

        public LatLon(float latitude, float longitude)
        {
            Lat = latitude;
            Lon = longitude;
        }
    }

    /// <summary>
    /// Class to store the latitude, longitude and direction of a locomotive on the webpage map
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct LatLonDirection
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly LatLon LatLon;
        public readonly float DirectionDeg;

        public LatLonDirection(LatLon latLon, float directionDeg)
        {
            this.LatLon = latLon;
            this.DirectionDeg = directionDeg;
        }
    }

    public enum TypeOfPointOnApiMap
    {
        Track,
        Named,
        Other,
    }

    public class PointOnApiMap
    {
        public LatLon LatLon { get; set; }
        public string Color { get; set; }
        public TypeOfPointOnApiMap TypeOfPointOnApiMap { get; set; }
        public string Name { get; set; }
    }

    public class LineOnApiMap
    {
        public LatLon LatLonFrom { get; set; }
        public LatLon LatLonTo { get; set; }
    }

    public class InfoApiMap
    {
        public string TypeOfLocomotive { get; private set; }

        public Collection<PointOnApiMap> PointOnApiMapList { get;  }
        public Collection<LineOnApiMap> LineOnApiMapList { get; }

        public double LatMin { get; private set; }
        public double LatMax { get; private set; }
        public double LonMin { get; private set; }
        public double LonMax { get; private set; }

        public InfoApiMap(ILocomotivePowerSupply powerSupply)
        {
            InitLocomotiveType(powerSupply);

            PointOnApiMapList = new Collection<PointOnApiMap>();
            LineOnApiMapList = new Collection<LineOnApiMap>();
            LatMax = -999999f;
            LatMin = +999999f;
            LonMax = -999999f;
            LonMin = +999999f;
        }

        private void InitLocomotiveType(ILocomotivePowerSupply powerSupply)
        {
            ArgumentNullException.ThrowIfNull(powerSupply);

            TypeOfLocomotive = powerSupply.Type switch
            {
                PowerSupplyType.DieselMechanical or PowerSupplyType.DieselHydraulic or PowerSupplyType.DieselElectric => "diesel",
                PowerSupplyType.Steam => "steam",
                _ => "electric",
            };
        }

        private static LatLon ConvertToLatLon(in WorldLocation worldLocation)
        {
            double latitude;
            double longitude;
            (latitude, longitude) = EarthCoordinates.ConvertWTC(worldLocation);
            LatLon latLon = new LatLon(MathHelper.ToDegrees((float)latitude), MathHelper.ToDegrees((float)longitude));

            return latLon;
        }

        private void AddToPointOnApiMap(in WorldLocation worldLocation, string color, TypeOfPointOnApiMap typeOfPointOnApiMap, string name)
        {
            LatLon latLon = ConvertToLatLon(worldLocation);

            AddToPointOnApiMap(latLon,
                color, typeOfPointOnApiMap, name);
        }

        private void AddToPointOnApiMap(LatLon latLon, string color, TypeOfPointOnApiMap typeOfPointOnApiMap, string name)
        {
            PointOnApiMap pointOnApiMap = new PointOnApiMap
            {
                LatLon = latLon,
                Color = color,
                TypeOfPointOnApiMap = typeOfPointOnApiMap,
                Name = name
            };

            if (pointOnApiMap.TypeOfPointOnApiMap == TypeOfPointOnApiMap.Named)
                // named last is the list so that they get displayed on top
                PointOnApiMapList.Add(pointOnApiMap);
            else
                PointOnApiMapList.Insert(0, pointOnApiMap);

            if (pointOnApiMap.LatLon.Lat > LatMax)
                LatMax = pointOnApiMap.LatLon.Lat;
            if (pointOnApiMap.LatLon.Lat < LatMin)
                LatMin = pointOnApiMap.LatLon.Lat;
            if (pointOnApiMap.LatLon.Lon > LonMax)
                LonMax = pointOnApiMap.LatLon.Lon;
            if (pointOnApiMap.LatLon.Lon < LonMin)
                LonMin = pointOnApiMap.LatLon.Lon;
        }

        private void AddToLineOnApiMap(LatLon latLonFrom, LatLon latLongTo)
        {
            LineOnApiMap lineOnApiMap = new LineOnApiMap
            {
                LatLonFrom = latLonFrom,
                LatLonTo = latLongTo
            };
            LineOnApiMapList.Add(lineOnApiMap);
        }

        public void AddTrackNodesToPointsOnApiMap(TrackDatabase trackDatabase)
        {
            ArgumentNullException.ThrowIfNull(trackDatabase);

            foreach (TrackNodeBase trackNode in trackDatabase.TrackNodes)
            {
                if (trackNode != null)
                {
                    if (trackNode.Location != WorldLocation.None)
                    {
                        AddToPointOnApiMap(trackNode.Location, "red", TypeOfPointOnApiMap.Track, "track");
                    }

                    if (trackNode is JunctionNode junctionNode && junctionNode.Location != WorldLocation.None)
                    {
                        AddToPointOnApiMap(junctionNode.Location, "red", TypeOfPointOnApiMap.Track, "track");
                    }

                    if (trackNode is VectorNode vectorNode && vectorNode.VectorSections.Length > 0)
                    {
                        bool first = true;
                        LatLon latLonFrom = new LatLon(0, 0);
                        VectorSectionNode trVectorSectionLast = null;
                        foreach (VectorSectionNode vectorSection in vectorNode.VectorSections)
                        {
                            LatLon latLonTo = ConvertToLatLon(vectorSection.Location);
                            AddToPointOnApiMap(vectorSection.Location, "red", TypeOfPointOnApiMap.Track, "track");
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                AddToLineOnApiMap(latLonFrom, latLonTo);
                            }
                            latLonFrom = latLonTo;
                            trVectorSectionLast = vectorSection;
                        }
                        if (trVectorSectionLast != null)
                        {
                            var connectors = trackDatabase.TrackNodeConnectors[trackNode.NodeIndex].TrackNodeConnectors;
                            if (connectors.Length == 2)
                            {
                                int link = connectors[1].Link;
                                LatLon latLonTo = ConvertToLatLon(trackDatabase.TrackNodes[link].Location);
                                AddToLineOnApiMap(latLonFrom, latLonTo);
                            }
                        }
                    }

                    if (trackNode is EndNode endNode)
                    {
                        LatLon latLonFrom = ConvertToLatLon(endNode.Location);
                        var connectors = trackDatabase.TrackNodeConnectors[trackNode.NodeIndex].TrackNodeConnectors;
                        if (connectors.Length > 0 && trackDatabase.TrackNodes[connectors[0].Link] is VectorNode linkedVector && linkedVector.VectorSections.Length > 0)
                        {
                            int lastIndex = linkedVector.VectorSections.Length - 1;
                            LatLon latLonTo = ConvertToLatLon(linkedVector.VectorSections[lastIndex].Location);
                            AddToLineOnApiMap(latLonFrom, latLonTo);
                        }
                    }
                }
            }
        }

        public void AddTrackItemsToPointsOnApiMap(ImmutableArray<TrackItemBase> trackItems)
        {
            foreach (TrackItemBase trackItem in trackItems)
            {
                if (trackItem is not LevelCrossingTrackItem)
                {
                    string itemName = trackItem switch
                    {
                        SidingTrackItem siding => siding.SidingName,
                        PlatformTrackItem platform => platform.PlatformName,
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(itemName))
                        AddToPointOnApiMap(trackItem.Location, "green", TypeOfPointOnApiMap.Named, $"{itemName.Replace("'", "", StringComparison.OrdinalIgnoreCase)}, {trackItem.GetType().Name}");
                    else
                        AddToPointOnApiMap(trackItem.Location, "blue", TypeOfPointOnApiMap.Other, $"{trackItem.GetType().Name}");
                }
            }
        }
    }
}