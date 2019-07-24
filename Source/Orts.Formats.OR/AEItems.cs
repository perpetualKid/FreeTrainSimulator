// COPYRIGHT 2014, 2015 by the Open Rails project.
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

using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Orts.Formats.Msts;
using Orts.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Orts.Formats.OR
{
    /// <summary>
    /// MSTSItems retains all the items comming from MSTS route config represented by GlobalItem derived classes.
    /// </summary>
    public class MSTSItems
    {
        public List<AEJunctionItem> switches;
        public List<SideItem> sidings;
        public List<AEBufferItem> buffers;
        public List<TrackSegment> segments;

        /// <summary>
        /// Search through 'segments', 'switches' or 'buffer' for the item in relation with the given TrackNode and sectionIdx
        /// </summary>
        /// <param name="node">The TrackNode to search for</param>
        /// <param name="sectionIdx">in case of multiple VectorNode, the index of the relevant vector</param>
        /// <returns>GlobalItem, use the typeItem as 'TypeItem' to do the casting</returns>
        public GlobalItem GetTrackSegment(TrackNode node, int sectionIdx)
        {
            if (node.TrJunctionNode != null)
            {
                foreach (var item in switches)
                {
                    if (item.associateNode.TrJunctionNode.Idx == node.Index)
                        return (GlobalItem)item;
                }
            }
            else if (node.TrEndNode)
            {
                foreach (var item in buffers)
                {
                    if (item.associateNode.Index == node.Index)
                        return (GlobalItem)item;
                }
            }
            else if (sectionIdx >= 0)
            {
                //foreach (var sideItem in segments)
                for (int cnt = 0; cnt < segments.Count; cnt++)
                {
                    var item = segments[cnt];
                    if (item.associateNodeIdx == node.Index && item.associateSectionIdx == sectionIdx)
                        return (GlobalItem)item;
                }
                foreach (var item in segments)
                {
                    if (item.associateNodeIdx == node.Index)
                        return (GlobalItem)item;
                }
            }
            return null;
        }

        /// <summary>
        /// Search through 'segments' for the item in relation with the given TrackNodeIdx and sectionIdx
        /// This methof return only a TrackSegment
        /// </summary>
        /// <param name="nodeIdx">The index of the node in TrackNode</param>
        /// <param name="sectionIdx">The index of the vector in the TrackNode</param>
        /// <returns>TrackSegment</returns>
        public TrackSegment GetTrackSegment(int nodeIdx, int sectionIdx)
        {
            TrackSegment trackSegment = null;
            foreach (var segment in segments)
            {
                if (segment.associateNodeIdx == nodeIdx && segment.associateSectionIdx == sectionIdx)
                {
                    trackSegment = segment;
                    break;
                }
            }

            return trackSegment;
        }
    }

    #region TagItem

    /// <summary>
    /// Used to represent a tag, a mark with a name and used to facilitate navigation in the viewer 
    /// </summary>
    public class TagItem : GlobalItem
    {
        [JsonProperty("nameTag")]
        public string nameTag { get; set; }
        [JsonProperty("nameVisible")]
        public bool nameVisible;

        public override void alignEdition(TypeEditor interfaceType, GlobalItem ownParent)
        { 
            setMovable();
            asMetadata = true;
        }

        public override void ConfigCoord (in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
            typeItem = (int)TypeItem.TAG_ITEM;
            nameVisible = false;
        }

        public override void Update(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
        }
    }
    #endregion

    #region StationItem

    public class StationItem : GlobalItem
    {
        [JsonProperty("nameStation")]
        public string nameStation;
        [JsonProperty("nameVisible")]
        public bool nameVisible;
        [JsonProperty("stationArea")]
        public List<StationAreaItem> stationArea;
        [JsonProperty("icoAngle")]
        public float icoAngle;
        [JsonProperty("areaCompleted")]
        public bool areaCompleted;
        [JsonProperty("configuredBuffer")]
        public List<AEBufferItem> insideBuffers;

        [JsonIgnore]
        private List<TrackSegment> segmentsInStation;
        [JsonIgnore]
        public AETraveller traveller { get; protected set; }
        [JsonIgnore]
        public StationPathsHelper StationPathsHelper;

        public override void alignEdition(TypeEditor interfaceType, GlobalItem ownParent)
        {
            if (interfaceType == TypeEditor.ROUTECONFIG)
            {
                setMovable();
                setRotable();
                setEditable();
                asMetadata = true;
            }
        }

        public override void ConfigCoord(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
            typeItem = (int)TypeItem.STATION_ITEM;
            nameVisible = false;
        }

        public override void Update(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
        }

        public override void SetAngle(float angle)
        {
            icoAngle = angle;
        }


        public void AddBuffers(ORRouteConfig orRouteConfig, List<AEBufferItem> buffers)
        {
            List<System.Drawing.PointF> polyPoints = getPolyPoints();
            if (insideBuffers.Count > 0)
            {
                insideBuffers.Clear();
            }
            foreach (AEBufferItem buffer in buffers)
            {
                if (DrawUtility.PointInPolygon(buffer.Location, polyPoints)) // && buffer.Configured && buffer.DirBuffer != AllowedDir.NONE)
                {
                    insideBuffers.Add(buffer);
                    buffer.alignEdition(TypeEditor.ROUTECONFIG, this);
                    orRouteConfig.AddItem(buffer);
                }
            }
        }

        public override void Complete(ORRouteConfig orRouteConfig, MSTSItems aeItems, MSTSBase tileBase)
        {
            if (stationArea.Count > 0)
            {
                AddBuffers(orRouteConfig, aeItems.buffers);
                checkForNewConnector(orRouteConfig, aeItems, tileBase);
                areaCompleted = true;
                searchForPaths(orRouteConfig, aeItems, tileBase);
                GetPaths();
            }
        }
        public void GetPaths()
        {
            List<StationPath> paths = null;
            for (int i = 0; i < this.stationArea.Count; i++)
            {
                StationAreaItem item = this.stationArea[i];
                if (item.IsInterface() && item.stationConnector.getLineSegment() != null &&
                    (item.stationConnector.getDirConnector() == AllowedDir.IN ||
                    item.stationConnector.getDirConnector() == AllowedDir.InOut))
                {
                    paths = item.stationConnector.stationPaths.getPaths();
                    StationPathsHelper.Add(item.stationConnector.getLabel(), paths);
                }
            }
            foreach (AEBufferItem buffer in insideBuffers)
            {
                if (buffer.stationPaths == null)
                    continue;
                paths = buffer.stationPaths.getPaths();
                StationPathsHelper.Add(buffer.NameBuffer, paths);
            }
        }

        public List<System.Drawing.PointF> getPolyPoints() //  Or Polygone, it's the same
        {
            List<System.Drawing.PointF> polyPoints = new List<System.Drawing.PointF>();
            foreach (StationAreaItem SAItem in stationArea)
            {
                float X = (SAItem.Coord.TileX * 2048f + SAItem.Coord.X);
                float Y = SAItem.Coord.TileY * 2048f + SAItem.Coord.Y;
                //  TODO: Le spolypoint doivent avoir des coordonnées absolues sur la map
                polyPoints.Add(new System.Drawing.PointF(X, Y));
            }
            return polyPoints;
        }

        public List<AESegment> getPolySegment()
        {
            StationAreaItem item;
            StationAreaItem item2;
            PointF tf;
            PointF tf2;
            if (this.stationArea.Count <= 0)
            {
                return null;
            }
            List<AESegment> list = new List<AESegment>();
            for (int i = 0; i < (this.stationArea.Count - 1); i++)
            {
                item = this.stationArea[i];
                item2 = this.stationArea[i + 1];
                tf = new PointF((item.Coord.TileX * 2048f) + item.Coord.X, (item.Coord.TileY * 2048f) + item.Coord.Y);
                tf2 = new PointF((item2.Coord.TileX * 2048f) + item2.Coord.X, (item2.Coord.TileY * 2048f) + item2.Coord.Y);
                list.Add(new AESegment(tf, tf2));
            }
            item = this.stationArea[0];
            item2 = this.stationArea[this.stationArea.Count - 1];
            tf = new PointF((item.Coord.TileX * 2048f) + item.Coord.X, (item.Coord.TileY * 2048f) + item.Coord.Y);
            tf2 = new PointF((item2.Coord.TileX * 2048f) + item2.Coord.X, (item2.Coord.TileY * 2048f) + item2.Coord.Y);
            list.Add(new AESegment(tf2, tf));
            return list;
        }

        public override double FindItem(PointF point, double snap, double actualDist, MSTSItems aeItems)
        {
            double iconDist = double.PositiveInfinity;
            List<System.Drawing.PointF> poly = getPolyPoints();
            int i, j = poly.Count - 1;
            bool oddNodes = false;
            double dist = double.PositiveInfinity;
            double usedSnap = snap;

            visible = false;
            if (!((Location.X < point.X - usedSnap) || (Location.X > point.X + usedSnap)
                || (Location.Y < point.Y - usedSnap) || (Location.Y > point.Y + usedSnap)))
            {
                visible = true;
                iconDist = (Math.Sqrt(Math.Pow((Location.X - point.X), 2) + Math.Pow((Location.Y - point.Y), 2)));
            }
            //File.AppendAllText(@"F:\temp\AE.txt", "FindItem: pointX: " + point.X +
            //    " pointY: " + point.Y + "\n");
            for (i = 0; i < poly.Count; i++)
            {
                dist = ((StationAreaItem)stationArea[i]).FindItem(point, usedSnap, iconDist < actualDist ? iconDist : actualDist, aeItems);
                if (stationArea[i].IsVisible())
                {
                    visible = false;
                    return dist;
                }
                //File.AppendAllText(@"F:\temp\AE.txt", "FindItem: polyX" + poly[i].X + 
                //    " polyY: " + poly[i].Y + 
                //    " polyX(j): " + poly[j].X +
                //    " polyY (j): " + poly[j].Y + "\n");
                if ((poly[i].Y < point.Y && poly[j].Y >= point.Y ||
                    poly[j].Y < point.Y && poly[i].Y >= point.Y)
                && (poly[i].X <= point.X || poly[j].X <= point.X))
                {
                    oddNodes ^= (poly[i].X + (point.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) * (poly[j].X - poly[i].X) < point.X);
                    //File.AppendAllText(@"F:\temp\AE.txt", "oddNodes ^=\n");
                }
                j = i;
            }

            if (oddNodes)
            {
                highlightTrackFromArea(aeItems);
                return 0;
            }
            return iconDist;
        }

        public double FindItemExact(PointF point, double actualDist, MSTSItems aeItems)
        {
            return FindItem(point, 0.0, actualDist, aeItems);
        }

        public void highlightTrackFromArea(MSTSItems aeItems)
        {
            foreach (StationAreaItem item in stationArea)
            {
                item.highlightTrackFromArea(aeItems);
            }
            foreach (AEBufferItem buffer in insideBuffers)
            {
                buffer.highlightTrackFromArea(aeItems);
            }
        }

        public void searchForPaths(ORRouteConfig orRouteConfig, MSTSItems aeItems, MSTSBase tileBase)
        {
            List<StationPath> paths = null;
            //StationPathsHelper.Clear();
            
            for (int i = 0; i < this.stationArea.Count; i++)
            {
                double positiveInfinity = double.PositiveInfinity;
                StationAreaItem item = this.stationArea[i];
                if (item.IsInterface() && item.stationConnector.getLineSegment() != null &&
                    (item.stationConnector.getDirConnector() == AllowedDir.IN ||
                    item.stationConnector.getDirConnector() == AllowedDir.InOut))
                {
                    TrackSegment segment = item.stationConnector.getLineSegment();
                    AETraveller myTravel = new AETraveller(this.traveller);
                    myTravel.place(segment.associateNodeIdx, (int)item.Coord.TileX, (int)item.Coord.TileY, item.Coord.X, item.Coord.Y);
                    myTravel.Move(1.0f);
                    PointF position = myTravel.getCoordinate();
                    if (FindItemExact(position, positiveInfinity, aeItems) == 0.0)
                    {
                        paths = item.stationConnector.searchPaths(myTravel, getListConnector(), aeItems, this);
                    }
                    else
                    {
                        myTravel.ReverseDirection();
                        myTravel.Move(2.0f);
                        position = myTravel.getCoordinate();
                        if (this.FindItemExact(position, positiveInfinity, aeItems) == 0.0)
                        {
                            paths = item.stationConnector.searchPaths(myTravel, getListConnector(), aeItems, this);
                        }
                    }
                    //StationPathsHelper.Add(item.stationConnector.getLabel(), paths);
                }
            }
            foreach (AEBufferItem buffer in insideBuffers)
            {
                AETraveller myTravel = new AETraveller(this.traveller);
                myTravel.place((int)buffer.Coord.TileX, (int)buffer.Coord.TileY, buffer.Coord.X, buffer.Coord.Y);
                if (myTravel.EndNodeAhead() != null)
                    myTravel.ReverseDirection();
                paths = buffer.searchPaths(myTravel, getListConnector(), aeItems, this);
                //StationPathsHelper.Add(buffer.NameBuffer, paths);
            }
        }

        public void checkForNewConnector(ORRouteConfig orRouteConfig, MSTSItems mstsItems, MSTSBase tileBase)
        {
            //  First, we remove all un configured connectors
            //foreach (StationAreaItem SAWidget in stationArea)
            for (int cnt = 0; cnt < stationArea.Count; cnt++)
            {
                var SAItem = stationArea[cnt];
                if (SAItem.IsInterface() && !SAItem.getStationConnector().isConfigured())
                {
                    removeConnector(orRouteConfig, SAItem);
                    cnt--;
                }
            }
            //  Next, we search for new connectors and add them
            foreach (var item in stationArea)
            {
                if (!item.IsInterface())
                    continue;
                TrackSegment trackSegment = mstsItems.GetTrackSegment(item.associateNodeIdx, item.associateSectionIdx);
                item.DefineAsInterface(trackSegment);
            }
            foreach (var segment in mstsItems.segments)
            {
                int num = 0;
                List<System.Drawing.PointF> pointsIntersect = new List<System.Drawing.PointF>();
                
                List<AESegment> polySegments = getPolySegment();
                if (segment.associateNodeIdx == 344)
                    num = 0;
                for (int cntPointSegment = 0; cntPointSegment < polySegments.Count; cntPointSegment++)
                {
                    PointF pointIntersect = DrawUtility.FindIntersection(polySegments[cntPointSegment], new AESegment (segment));
                    if (!pointIntersect.IsEmpty)
                    {
                        StationAreaItem newPoint = new StationAreaItem(TypeEditor.ROUTECONFIG, this);
                        MSTSCoord coord = new MSTSCoord(pointIntersect);
                        newPoint.ConfigCoord(coord);
                        num++;
                        //newPoint.toggleSelected();
                        stationArea.Insert(num, newPoint);
                        newPoint.DefineAsInterface(segment);
                        newPoint.setAngle(getPolyPoints());
                        orRouteConfig.AddItem(newPoint);
                    }
                    num++;
                }
            }
        }

        public void removeConnector(ORRouteConfig orRouteConfig, StationAreaItem connector)
        {
            stationArea.Remove(connector);
            orRouteConfig.RemoveConnectorItem(connector);

        }

        public void setTraveller(AETraveller travel)
        {
            this.traveller = travel;
        }

        private List<TrackSegment> getListConnector()
        {
            List<TrackSegment> list = new List<TrackSegment>();
            for (int i = 0; i < this.stationArea.Count; i++)
            {
                StationAreaItem item = this.stationArea[i];
                if (item.IsInterface() && item.stationConnector != null && item.stationConnector.getLineSegment() != null)
                {
                    list.Add(item.stationConnector.getLineSegment());
                }
            }
            return list;
        }

        public bool IsInStation(in MSTSCoord place)
        {
            double iconDist = double.PositiveInfinity;
            List<System.Drawing.PointF> poly = getPolyPoints();
            int i, j = poly.Count - 1;
            bool oddNodes = false;
            PointF placeNormalized = place.ConvertToPointF();

            visible = false;
            if (!((Location.X < placeNormalized.X) || (Location.X > placeNormalized.X)
                || (Location.Y < placeNormalized.Y) || (Location.Y > placeNormalized.Y)))
            {
                visible = true;
                iconDist = (Math.Sqrt(Math.Pow((Location.X - placeNormalized.X), 2) + Math.Pow((Location.Y - placeNormalized.Y), 2)));
            }
            //File.AppendAllText(@"F:\temp\AE.txt", "FindItem: pointX: " + point.X +
            //    " pointY: " + point.Y + "\n");
            for (i = 0; i < poly.Count; i++)
            {
                if ((poly[i].Y < placeNormalized.Y && poly[j].Y >= placeNormalized.Y ||
                    poly[j].Y < placeNormalized.Y && poly[i].Y >= placeNormalized.Y)
                && (poly[i].X <= placeNormalized.X || poly[j].X <= placeNormalized.X))
                {
                    oddNodes ^= (poly[i].X + (placeNormalized.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) * (poly[j].X - poly[i].X) < placeNormalized.X);
                    //File.AppendAllText(@"F:\temp\AE.txt", "oddNodes ^=\n");
                }
                j = i;
            }

            if (oddNodes)
            {
                return true;
            }
            return false;
        }
    }
    #endregion

    #region StationAreaItem


    public class StationAreaItem : GlobalItem
    {
        [JsonProperty("Connector")]
        public StationConnector stationConnector = null;
        [JsonIgnore]
        bool selected = false;
        [JsonIgnore]
        public StationItem parent { get; protected set; }

        public StationAreaItem(TypeEditor interfaceType, StationItem myParent)
        {
            alignEdition(interfaceType, myParent);
            parent = myParent;
        }

        public override void alignEdition(TypeEditor interfaceType, GlobalItem ownParent)
        {
            if (interfaceType == TypeEditor.ROUTECONFIG)
            {
                setMovable();
                asMetadata = true;
            }
            if (parent == null)
                parent = (StationItem)ownParent;
        }

        public override void ConfigCoord(in MSTSCoord coord)
        {
            base.ConfigCoord(in coord);
        }

        public void DefineAsInterface(TrackSegment segment)
        {
            typeItem = (int)TypeItem.STATION_CONNECTOR;
            if (stationConnector == null)
            {
                stationConnector = new StationConnector(segment);
            }
            stationConnector.Init(segment);
            associateNode = segment.associateNode;
            associateNodeIdx = segment.associateNodeIdx;
            associateSectionIdx = segment.associateSectionIdx;
            setLineSnap();
            setEditable();
            asMetadata = true;
        }

        public void setAngle(List<System.Drawing.PointF> polyPoint)
        {
            stationConnector.setIcoAngle(Coord.ConvertToPointF(), polyPoint);
        }

        public override void Update(in MSTSCoord coord)
        {   
                base.ConfigCoord(in coord);
        }

        public bool IsInterface()
        {
            if (typeItem == (int)TypeItem.STATION_CONNECTOR)
                return true;
            else
                return false;
        }

        public override void Edit()
        {
        }

        public StationConnector getStationConnector()
        {
            return stationConnector;
        }

        public void highlightTrackFromArea(MSTSItems aeItems)
        {
            if (IsInterface())
            {
                stationConnector.highlightTrackFromArea(aeItems);
            }
        }
    }

    public class StationConnector
    {
        [JsonProperty("dirConnector")]
        public AllowedDir dirConnector { get; protected set; }
        [JsonProperty("labelConnector")]
        public string label { get; protected set; }
        [JsonIgnore]
        List<string> allowedDirections;
        [JsonIgnore]
        public TrackSegment segment { get; protected set; }
        [JsonPropertyAttribute("angle")]
        public float angle;
        [JsonProperty("Configured")]
        bool configured;
        [JsonProperty("StationPaths")]
        public StationPaths stationPaths { get; protected set; }
        [JsonProperty("ChainedConnector")]
        public string ChainedConnector { get; protected set; }    // Circle chain
        [JsonConstructor]
        public StationConnector(int i)
        {
            //File.AppendAllText(@"F:\temp\AE.txt", "Json StationConnector :" + (int)i + "\n");
            allowedDirections = new List<string>();
            allowedDirections.Add("   ");
            allowedDirections.Add("In");
            allowedDirections.Add("Out");
            allowedDirections.Add("Both");
            dirConnector = AllowedDir.NONE;
            configured = false;
            stationPaths = new StationPaths();
            label = "";
        }

        public StationConnector(TrackSegment segment)
        {
            //File.AppendAllText(@"F:\temp\AE.txt", "StationConnector\n");
            this.segment = segment;
            segment.HasConnector = this;
            allowedDirections = new List<string>();
            allowedDirections.Add("   ");
            allowedDirections.Add("In");
            allowedDirections.Add("Out");
            allowedDirections.Add("Both");
            dirConnector = AllowedDir.NONE;
            configured = false;
            stationPaths = new StationPaths();
            label = "";
        }

        public void Init(TrackSegment info)
        {
            segment = info;
            info.HasConnector = this;
        }

        public bool isConfigured() { return configured; }
        public string getLabel()
        {
            return label;
        }

        public AllowedDir getDirConnector()
        {
            return dirConnector;
        }

        public TrackSegment getLineSegment()
        {
            return segment;
        }

        public void setIcoAngle(PointF posit, List<PointF> polyPoints)
        {
            double tempo;
            PointF end1 = segment.associateSegment.startPoint;
            PointF end2 = segment.associateSegment.endPoint;

            if (DrawUtility.PointInPolygon(end1, polyPoints))
            {
                tempo = Math.Atan2(end1.X - posit.X, end1.Y - posit.Y);
                angle = (float)((tempo * 180.0d) / Math.PI) - 90f;
            }
            else if (DrawUtility.PointInPolygon(end2, polyPoints))
            {
                tempo = Math.Atan2(end2.X - posit.X, end2.Y - posit.Y);
                angle = (float)((tempo * 180.0d) / Math.PI) - 90f;
            }
            else
            {
                angle = 0.0f;
            }
        }

        public void highlightTrackFromArea(MSTSItems aeItems)
        {
            if (stationPaths != null)
            {
                stationPaths.highlightTrackFromArea(aeItems);
            }
        }

        public List<StationPath> searchPaths(AETraveller myTravel, List<TrackSegment> listConnector, MSTSItems aeItems, StationItem parent)
        {
            if (stationPaths == null)
            {
                stationPaths = new StationPaths();
            }
            stationPaths.Clear();
            List<StationPath> paths = stationPaths.explore(myTravel, listConnector, aeItems, parent);
            return paths;
        }

 

    }
    #endregion

    #region TrackSegment
    /// <summary>
    /// Defines a geometric Track segment.
    /// </summary>
    public class TrackSegment : GlobalItem
    {
        public string segmentLabel;
        public AESegment associateSegment;
        public bool isCurved = false;
        private bool snapped = false;
        public double lengthSegment;
        public float angle1, angle2;
        public List<SideItem> sidings;
        public bool linkToOther = false;
        public StationConnector HasConnector = null;
        public bool Configured = false;
        [JsonIgnore]
        public StationItem parentStation { get; protected set; }


        public bool setAreaSnaps(MSTSItems aeItems)
        {
            snapped = true;
            //var indexClosest = (int)associateNodeIdx;
            //foreach (var segment in mstsItems.segments)
            //{
            //    if (segment.associateNodeIdx == indexClosest && segment.inStationArea)
            //    {
            //        segment.setSnap();
            //    }
            //}
            return snapped;
        }

        public void InStation(StationItem parent)
        {
            parentStation = parent;
            setEditable();
        }
    }

    #endregion
}
