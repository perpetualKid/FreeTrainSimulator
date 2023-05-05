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

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    public class OverlayLayer
    {
        public string Name { get; set; }
        public bool Visible { get; set; }
    }

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
        public readonly float directionDeg;

        public LatLonDirection(LatLon latLon, float directionDeg)
        {
            this.LatLon = latLon;
            this.directionDeg = directionDeg;
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
        public LatLon latLon;
        public string color;
        public TypeOfPointOnApiMap typeOfPointOnApiMap;
        public string name;
    }

    public class LineOnApiMap
    {
        public LatLon latLonFrom;
        public LatLon latLonTo;
    }

    public class InfoApiMap
    {
        public string typeOfLocomotive;
        public string baseLayer;
        public OverlayLayer[] overlayLayer;

        public LinkedList<PointOnApiMap> pointOnApiMapList;
        public LinkedList<LineOnApiMap> lineOnApiMapList;

        public double latMin;
        public double latMax;
        public double lonMin;
        public double lonMax;

        public InfoApiMap(string powerSupplyName)
        {
            InitLocomotiveType(powerSupplyName);

            pointOnApiMapList = new LinkedList<PointOnApiMap>();
            lineOnApiMapList = new LinkedList<LineOnApiMap>();

            latMax = -999999f;
            latMin = +999999f;
            lonMax = -999999f;
            lonMin = +999999f;
        }

        private void InitLocomotiveType(string powerSupplyName)
        {
            if (powerSupplyName.Contains("steam", System.StringComparison.OrdinalIgnoreCase))
                typeOfLocomotive = "steam";
            else
                if (powerSupplyName.Contains("diesel", System.StringComparison.OrdinalIgnoreCase))
                typeOfLocomotive = "diesel";
            else
                typeOfLocomotive = "electric";
        }

        public static LatLon convertToLatLon(in WorldLocation worldLocation)
        {
            double latitude;
            double longitude;
            (latitude, longitude) = EarthCoordinates.ConvertWTC(worldLocation);
            LatLon latLon = new LatLon(MathHelper.ToDegrees((float)latitude), MathHelper.ToDegrees((float)longitude));

            return latLon;
        }

        public void addToPointOnApiMap(in WorldLocation worldLocation, string color, TypeOfPointOnApiMap typeOfPointOnApiMap, string name)
        {
            LatLon latLon = convertToLatLon(worldLocation);

            addToPointOnApiMap(latLon,
                color, typeOfPointOnApiMap, name);
        }

        public void addToPointOnApiMap(LatLon latLon, string color, TypeOfPointOnApiMap typeOfPointOnApiMap, string name)
        {
            PointOnApiMap pointOnApiMap = new PointOnApiMap
            {
                latLon = latLon,
                color = color,
                typeOfPointOnApiMap = typeOfPointOnApiMap,
                name = name
            };

            if (pointOnApiMap.typeOfPointOnApiMap == TypeOfPointOnApiMap.Named)
                // named last is the list so that they get displayed on top
                pointOnApiMapList.AddLast(pointOnApiMap);
            else
                pointOnApiMapList.AddFirst(pointOnApiMap);

            if (pointOnApiMap.latLon.Lat > latMax)
                latMax = pointOnApiMap.latLon.Lat;
            if (pointOnApiMap.latLon.Lat < latMin)
                latMin = pointOnApiMap.latLon.Lat;
            if (pointOnApiMap.latLon.Lon > lonMax)
                lonMax = pointOnApiMap.latLon.Lon;
            if (pointOnApiMap.latLon.Lon < lonMin)
                lonMin = pointOnApiMap.latLon.Lon;
        }

        public void addToLineOnApiMap(LatLon latLonFrom, LatLon latLongTo)
        {
            LineOnApiMap lineOnApiMap = new LineOnApiMap
            {
                latLonFrom = latLonFrom,
                latLonTo = latLongTo
            };
            lineOnApiMapList.AddLast(lineOnApiMap);
        }

        public void addTrNodesToPointsOnApiMap(TrackNodes trackNodes)
        {
            foreach (TrackNode trackNode in trackNodes)
            {
                if (trackNode != null)
                {
                    if (trackNode.UiD != null)
                    {
                        addToPointOnApiMap(trackNode.UiD.Location, "red", TypeOfPointOnApiMap.Track, "track");
                    }

                    if ((trackNode is TrackJunctionNode junctionNode && junctionNode.UiD != null))
                    {
                        addToPointOnApiMap(junctionNode.UiD.Location, "red", TypeOfPointOnApiMap.Track, "track");
                    }

                    if ((trackNode is TrackVectorNode vectorNode && vectorNode.TrackVectorSections != null))
                    {
                        bool first = true;
                        LatLon latLonFrom = new LatLon(0, 0);
                        TrackVectorSection trVectorSectionLast = null;
                        foreach (TrackVectorSection vectorSection in vectorNode.TrackVectorSections)
                        {
                            LatLon latLonTo = convertToLatLon(vectorSection.Location);
                            addToPointOnApiMap(vectorSection.Location, "red", TypeOfPointOnApiMap.Track, "track");
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                addToLineOnApiMap(latLonFrom, latLonTo);
                            }
                            latLonFrom = latLonTo;
                            trVectorSectionLast = vectorSection;
                        }
                        if (trVectorSectionLast != null)
                        {
                            if (trackNode.TrackPins.Length == 2)
                            {
                                int link = trackNode.TrackPins[1].Link;
                                LatLon latLonTo = convertToLatLon(trackNodes[link].UiD.Location);
                                addToLineOnApiMap(latLonFrom, latLonTo);
                            }
                        }
                    }

                    if (trackNode is TrackEndNode endNode)
                    {
                        LatLon latLonFrom = convertToLatLon(endNode.UiD.Location);
                        int lastIndex = (trackNodes[trackNode.TrackPins[0].Link] as TrackVectorNode).TrackVectorSections.Length - 1;
                        LatLon latLonTo = convertToLatLon((trackNodes[trackNode.TrackPins[0].Link] as TrackVectorNode).TrackVectorSections[lastIndex].Location);
                        addToLineOnApiMap(latLonFrom, latLonTo);
                    }
                }
            }
        }

        public void addTrItemsToPointsOnApiMap(List<TrackItem> trackItems)
        {
            foreach (TrackItem trackItem in trackItems)
            {
                if (trackItem is not LevelCrossingItem)
                {
                    if (!string.IsNullOrEmpty(trackItem.ItemName))
                        addToPointOnApiMap(trackItem.Location, "green", TypeOfPointOnApiMap.Named, $"{trackItem.ItemName.Replace("'", "", StringComparison.OrdinalIgnoreCase)}, {trackItem.GetType().Name}");
                    else
                        addToPointOnApiMap(trackItem.Location, "blue", TypeOfPointOnApiMap.Other, $"{trackItem.GetType().Name}");
                }
            }
        }
    }
}