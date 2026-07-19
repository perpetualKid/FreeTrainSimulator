// COPYRIGHT 2014, 2018 by the Open Rails project.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace ORTS.TrackViewer.Drawing
{
    /// <summary>
    /// Class to contain all information loaded for the route that is not Trackviewer specific. So basically loading all relevant route files,
    /// like TrackDB for rails and roads, TsectionDat, without further processing
    /// </summary>
    internal static class RouteData
    {
        private static string storedRoutePath;
        private static Dictionary<int, string> signalFileNames;

        /// <summary>
        /// Constructor. Loads all the relevant files for the route
        /// </summary>
        /// <param name="routePath">Path to the route directory</param>
        /// <param name="messageDelegate">The delegate that will deal with the message we want to send to the user</param>
        public static void Load(RouteModelHeader routeModelHeader, MessageDelegate messageDelegate)
        {
            messageDelegate?.Invoke(TrackViewer.catalog.GetString("Loading trackfile .trk ..."));
            RouteModel routeModel = Task.Run(async () => await (routeModelHeader.GetExtended(CancellationToken.None).ConfigureAwait(false))).Result;

            FolderStructure.ContentFolder.RouteFolder routeFolder = routeModel.MstsRouteFolder();
            storedRoutePath = routeFolder.CurrentFolder;

            Task.Run(async () => await RuntimeDataResolver.Initialize(routeModel, true, null, CancellationToken.None).ConfigureAwait(false)).Wait();
        }

        /// <summary>
        /// Get the filename of the file where the signal shape is defined.
        /// </summary>
        /// <param name="signalIndex">The index (from the .tdb) of the signal</param>
        public static string GetSignalFilename(int signalIndex)
        {
            if (signalFileNames == null)
            {
                signalFileNames = new Dictionary<int, string>();
                string WFilePath = storedRoutePath + @"\WORLD\";

                HashSet<TokenID> Tokens = new HashSet<TokenID>
                {
                    TokenID.Signal
                };

                string[] wfiles;
                try
                {
                    wfiles = Directory.GetFiles(WFilePath, "*.w");
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    wfiles = Array.Empty<string>();
                }
                foreach (string fileName in wfiles)
                {
                    if (Path.GetFileName(fileName).Length != 17)
                        continue;

                    WorldFile WFile;
                    try
                    {
                        WFile = new WorldFile(fileName, Tokens);
                    }
                    catch (FileLoadException error)
                    {
                        Trace.WriteLine(error);
                        continue;
                    }

                    // loop through all signals

                    foreach (WorldObject worldObject in WFile.Objects)
                    {
                        if (worldObject.GetType() != typeof(SignalObject))
                            continue;

                        SignalObject thisWorldObject = worldObject as SignalObject;
                        if (thisWorldObject.SignalUnits == null)
                            continue; //this has no unit, will ignore it and treat it as static in scenary.cs

                        foreach (SignalUnit si in thisWorldObject.SignalUnits)
                        {
                            signalFileNames[si.TrackItem] = thisWorldObject.FileName;
                        }
                    }
                }
            }

            signalFileNames.TryGetValue(signalIndex, out string signalFileName);
            if (string.IsNullOrEmpty(signalFileName))
            {
                return "unknown";
            }
            else
            {
                return signalFileName;
            }
        }
    }

    /// <summary>
    /// This is a big class where the drawing of everything in the track data base is done. 
    /// This means tracks themselves (meaning so-called vector nodes that contain a number of sections,
    /// each of which is drawn separately), junctions and endnodes (drawn using textures), track items (platforms,
    /// sidings, signals, hazards, ...), and the same for roads and road-items.
    /// The methods for these are DrawTracks, DrawRoads, DrawJunctionAndEndNodes, DrawTrackItems, DrawRoadTrackItems
    /// For all things drawn it is also tracked which of these things is closest to the (current) mouse location.
    /// Those particular things are then re-drawn in highlight colors. These things are also available for other
    /// uses, like a statusbar and using it for path editor. The method to call is DrawHighlights
    /// The drawing itself is done by calls to routines in drawarea, that translates world-coordinates to screen coordinates 
    /// and then calls basic drawing routines.
    /// 
    /// There are also a number of methods to find specific items or tracks given their index, such that the user can search for them.
    /// 
    /// At last there are a number of utility methods like GetLength, UIDlocation.
    /// </summary>
    internal sealed class DrawTrackDB
    {
        #region public members
        // Maximal and minimal tile numbers from the track database
        /// <summary>Maximum of the TileX index found in the track database</summary>
        public int MaxTileX { get; private set; }
        /// <summary>Minimum of the TileX index found in the track database</summary>
        public int MinTileX { get; private set; }
        /// <summary>Maximum of the TileZ index found in the track database</summary>
        public int MaxTileZ { get; private set; }
        /// <summary>Minimum of the TileZ index found in the track database</summary>
        public int MinTileZ { get; private set; }

        /// <summary>(approximate) world location of the sidings indexed by siding name</summary>
        public Dictionary<string, WorldLocation> SidingLocations { get; private set; }
        /// <summary>(approximate) world location of the platforms indexed by platform name</summary>
        public Dictionary<string, WorldLocation> PlatformLocations { get; private set; }
        /// <summary>(approximate) world location of the stations indexed by station name</summary>
        public Dictionary<string, WorldLocation> StationLocations { get; private set; }

        /// <summary>Rail (so not road) track closest to the mouse</summary>
        private CloseToMouseTrack closestRailTrack;
        /// <summary>Road track closest to the mouse</summary>
        public CloseToMouseTrack ClosestRoadTrack { get; private set; }
        /// <summary>Either Road or Rail track (but must be drawn) that is closest to the mouse</summary>
        public CloseToMouseTrack ClosestTrack { get; private set; }
        /// <summary>The drawn junction or end node that is closest to the mouse</summary>
        public CloseToMouseJunctionOrEnd ClosestJunctionOrEnd { get; private set; }
        /// <summary>The drawn track item (either road or rail) that is closest to the mouse</summary>
        public CloseToMouseItem ClosestTrackItem { get; private set; }
        #endregion

        #region private members
        /// <summary>Track Section Data</summary>
        private readonly FreeTrainSimulator.Models.Track.TrackSectionModel trackSections;
        private readonly TrackWorld trackWorld;
        private readonly FreeTrainSimulator.Models.Track.TrackDatabase railTrackDatabase;
        private readonly FreeTrainSimulator.Models.Track.TrackDatabase roadTrackDatabase;

        /// <summary>Normally highlights are based on mouse location. When searching this is overridden</summary>
        private bool IsHighlightOverridden;
        /// <summary>Normally highlights are based on mouse location. When searching this is overridden</summary>
        private bool IsHighlightOverriddenTrItem;

        /// <summary>Table of track-items. Basically a copy of TrackDB.TrItemTable, but then using drawable track items </summary>
        private DrawableTrackItem[] railTrackItemTable;
        /// <summary>Table of road-track-items. Basically a copy of roadTrackDB.TrItemTable, but then using drawable track items </summary>
        private DrawableTrackItem[] roadTrackItemTable;
        /// <summary>Direction-angle of track indexed by tracknode index (of the endnode)</summary>
        private readonly Dictionary<int, float> endnodeAngles = new Dictionary<int, float>();

        // various fields to optimize drawing efficiency
        private int tileXIndexStart;
        private int tileXIndexStop;
        private int tileZIndexStart;
        private int tileZIndexStop;
        #endregion

        #region Constructor and initialization
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="messageDelegate">The delegate that will deal with the message we want to send to the user</param>
        public DrawTrackDB(MessageDelegate messageDelegate)
        {
            trackSections = RuntimeDataResolver.Instance.TrackSections;
            trackWorld = RuntimeDataResolver.Instance.TrackWorld;
            railTrackDatabase = trackWorld.TrackDatabase;
            roadTrackDatabase = trackWorld.RoadDatabase;

            messageDelegate(TrackViewer.catalog.GetString("Finding the angles to draw signals, endnodes, ..."));

            FindExtremeTiles();
            FillAvailableIndexes();
            FindSignalDetails();
            FindEndnodeOrientations();
            FindSidingsAndPlatforms();

            closestRailTrack = new CloseToMouseTrack();
            ClosestRoadTrack = new CloseToMouseTrack();
            ClosestJunctionOrEnd = new CloseToMouseJunctionOrEnd();
            ClosestTrackItem = new CloseToMouseItem();

        }

        /// <summary>
        /// Determines the minimal and maximale tileX/Z from the database.
        /// </summary>
        private void FindExtremeTiles()
        {
            MinTileX = +1000000;
            MinTileZ = +1000000;
            MaxTileX = -1000000;
            MaxTileZ = -1000000;
            foreach (FreeTrainSimulator.Models.Track.VectorNode tn in railTrackDatabase.VectorNodes)
            {
                for (int tvsi = 0; tvsi < tn.VectorSections.Length; tvsi++)
                {
                    FreeTrainSimulator.Models.Track.VectorSectionNode tvs = tn.VectorSections[tvsi];
                    if (tvs.Location.Tile.X < MinTileX)
                    { MinTileX = tvs.Location.Tile.X; }
                    ;
                    if (tvs.Location.Tile.Z < MinTileZ)
                    { MinTileZ = tvs.Location.Tile.Z; }
                    ;
                    if (tvs.Location.Tile.X > MaxTileX)
                    { MaxTileX = tvs.Location.Tile.X; }
                    ;
                    if (tvs.Location.Tile.Z > MaxTileZ)
                    { MaxTileZ = tvs.Location.Tile.Z; }
                    ;
                }
            }
        }

        /// <summary>
        /// Find, for each signal, the orientation/angle we need to draw it
        /// </summary>
        private void FindSignalDetails()
        {
            foreach (FreeTrainSimulator.Models.Track.VectorNode vectorNode in railTrackDatabase.VectorNodes)
            {
                if (!railTrackDatabase.TrackItemSelectors.TryGetValue(vectorNode.NodeIndex, out FreeTrainSimulator.Models.Track.TrackItemIndex trackItemIndex))
                    continue;

                foreach (int itemIndex in trackItemIndex.TrackItems)
                {
                    if (itemIndex < 0 || itemIndex >= railTrackItemTable.Length)
                        continue;
                    DrawableTrackItem trackItem = railTrackItemTable[itemIndex];
                    if (trackItem is DrawableSignalItem signalItem)
                    {
                        signalItem.FindAngle(vectorNode.NodeIndex);
                        if (railTrackDatabase.TrackItems[itemIndex] is FreeTrainSimulator.Models.Track.SignalTrackItem signalTrackItem)
                        {
                            signalItem.SetNormalSignal(signalTrackItem.NormalSignal);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For each endnode, find its orientattion. So we can draw a line in the correct direction.
        /// </summary>
        private void FindEndnodeOrientations()
        {
            if (railTrackDatabase == null)
                return;

            foreach (FreeTrainSimulator.Models.Track.EndNode endNode in railTrackDatabase.EndNodes)
            {
                endnodeAngles[endNode.NodeIndex] = 0;//default value in case we cannot find a better one

                FreeTrainSimulator.Models.Track.TrackNodeConnectorIndex connectors = railTrackDatabase.TrackNodeConnectors[endNode.NodeIndex];
                int connectedNodeIndex = connectors.TrackNodeConnectors[0].Link;
                FreeTrainSimulator.Models.Track.VectorNode connectedVectorNode = railTrackDatabase.TrackNodes[connectedNodeIndex] as FreeTrainSimulator.Models.Track.VectorNode;
                if (connectedVectorNode == null)
                    continue;

                FreeTrainSimulator.Models.Track.TrackNodeConnectorIndex vectorConnectors = railTrackDatabase.TrackNodeConnectors[connectedVectorNode.NodeIndex];
                if (vectorConnectors.TrackNodeConnectors[0].Link == endNode.NodeIndex)
                {
                    //find angle at beginning of vector node
                    FreeTrainSimulator.Models.Track.VectorSectionNode section = connectedVectorNode.VectorSections[0];
                    endnodeAngles[endNode.NodeIndex] = section.Direction.Y;
                }
                else
                {
                    //find angle at end of vector node
                    FreeTrainSimulator.Models.Track.VectorSectionNode section = connectedVectorNode.VectorSections[^1];
                    endnodeAngles[endNode.NodeIndex] = section.Direction.Y;
                    try
                    { // try to get even better in case the last section is curved
                        trackSections.TrackSections.TryGetValue(section.NodeIndex, out FreeTrainSimulator.Models.Track.TrackSection trackSection);
                        if (trackSection.Curved)
                        {
                            endnodeAngles[endNode.NodeIndex] += MathHelper.ToRadians(trackSection.Angle);
                        }
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch { }
#pragma warning restore CA1031 // Do not catch general exception types
                }
            }
        }

        /// <summary>
        /// Generate a list of locations for all platforms and for all sidings, so people can go to these locations from menu
        /// </summary>
        private void FindSidingsAndPlatforms()
        {
            SidingLocations = new Dictionary<string, WorldLocation>();
            PlatformLocations = new Dictionary<string, WorldLocation>();
            StationLocations = new Dictionary<string, WorldLocation>();

            if (railTrackDatabase == null)
                return;

            foreach (FreeTrainSimulator.Models.Track.TrackItemBase trackItem in railTrackDatabase.TrackItems)
            {
                if (trackItem is FreeTrainSimulator.Models.Track.SidingTrackItem siding)
                {
                    SidingLocations[siding.SidingName] = siding.Location;
                }

                if (trackItem is FreeTrainSimulator.Models.Track.PlatformTrackItem platform)
                {
                    PlatformLocations[platform.PlatformName] = platform.Location;
                    StationLocations[platform.StationName] = platform.Location;
                }
            }
        }
        #endregion

        #region Cache available tracknodes, items, etc per tile
        /// <summary>
        /// In preparation of actual drawing we first have to know which tiles are visible.
        /// And then we translate the visible tiles to array/list start and stop indexes to be used
        /// </summary>
        /// <param name="drawArea">The area upon which we draw, which determines the visible tiles</param>
        private void PrepareDrawing(DrawArea drawArea)
        {
            // determine the min and max values of the tiles that we actually need to draw
            // in some cases (e.g. during initialization) the drawing area itself is really outside the track database,
            // so we have to account for that.
            int actualTileXLeft = Math.Max(Math.Min(drawArea.LocationUpperLeft.Tile.X, MaxTileX), MinTileX);
            int actualTileXRight = Math.Min(Math.Max(drawArea.LocationLowerRight.Tile.X, MinTileX), MaxTileX);
            int actualTileZBot = Math.Max(Math.Min(drawArea.LocationLowerRight.Tile.Z, MaxTileZ), MinTileZ);
            int actualTileZTop = Math.Min(Math.Max(drawArea.LocationUpperLeft.Tile.Z, MinTileZ), MaxTileZ);

            SetTileIndexes(actualTileXLeft, actualTileXRight, actualTileZBot, actualTileZTop);
        }

        /// <summary>
        /// Translate the min and max values of the tileX and tileY into indexes to be used in 'availability' lists
        /// </summary>
        private void SetTileIndexes(int actualTileXLeft, int actualTileXRight, int actualTileZBot, int actualTileZTop)
        {
            tileXIndexStart = actualTileXLeft - MinTileX;
            tileXIndexStop = actualTileXRight - MinTileX;
            tileZIndexStart = actualTileZBot - MinTileZ;
            tileZIndexStop = actualTileZTop - MinTileZ;
        }

        /// <summary>
        /// For each of the various types of tracknodes we list the ones per tile.
        /// </summary>
        private List<FreeTrainSimulator.Models.Track.VectorNode>[][] availableRailVectorNodeIndexes;
        private List<FreeTrainSimulator.Models.Track.VectorNode>[][] availableRoadVectorNodeIndexes;
        private List<FreeTrainSimulator.Models.Track.TrackNodeBase>[][] availablePointNodeIndexes;
        private List<DrawableTrackItem>[][] availableRailItemIndexes;
        private List<DrawableTrackItem>[][] availableRoadItemIndexes;

        /// <summary>
        /// Run over the track databases, find the locations of nodes and items, and add the nodes and items to the correct
        /// 'available' list, indexed by tile.
        /// </summary>
        private void FillAvailableIndexes()
        {
            SetTileIndexes(MinTileX, MaxTileX, MinTileZ, MaxTileZ);
            availableRailVectorNodeIndexes = new List<FreeTrainSimulator.Models.Track.VectorNode>[tileXIndexStop + 1][];
            availableRoadVectorNodeIndexes = new List<FreeTrainSimulator.Models.Track.VectorNode>[tileXIndexStop + 1][];
            availablePointNodeIndexes = new List<FreeTrainSimulator.Models.Track.TrackNodeBase>[tileXIndexStop + 1][];
            availableRailItemIndexes = new List<DrawableTrackItem>[tileXIndexStop + 1][];
            availableRoadItemIndexes = new List<DrawableTrackItem>[tileXIndexStop + 1][];
            InitIndexedLists(availableRailVectorNodeIndexes);
            InitIndexedLists(availableRoadVectorNodeIndexes);
            InitIndexedLists(availablePointNodeIndexes);
            InitIndexedLists(availableRailItemIndexes);
            InitIndexedLists(availableRoadItemIndexes);

            // find rail track point nodes (junctions and end nodes)
            foreach (FreeTrainSimulator.Models.Track.JunctionNode junctionNode in railTrackDatabase.JunctionNodes)
            {
                AddLocationToAvailableList(junctionNode.Location, availablePointNodeIndexes, junctionNode);
            }
            foreach (FreeTrainSimulator.Models.Track.EndNode endNode in railTrackDatabase.EndNodes)
            {
                AddLocationToAvailableList(endNode.Location, availablePointNodeIndexes, endNode);
            }

            // find rail track vector nodes
            foreach (FreeTrainSimulator.Models.Track.VectorNode vectorNode in railTrackDatabase.VectorNodes)
            {
                for (int tvsi = 0; tvsi < vectorNode.VectorSections.Length; tvsi++)
                {
                    List<WorldLocation> locationList = FindLocationList(vectorNode, tvsi);
                    foreach (WorldLocation location in locationList)
                    {
                        AddLocationToAvailableList(location, availableRailVectorNodeIndexes, vectorNode);
                    }
                }
            }

            if (roadTrackDatabase != null)
            {
                foreach (FreeTrainSimulator.Models.Track.VectorNode vectorNode in roadTrackDatabase.VectorNodes)
                {
                    for (int tvsi = 0; tvsi < vectorNode.VectorSections.Length; tvsi++)
                    {
                        List<WorldLocation> locationList = FindLocationList(vectorNode, tvsi);
                        foreach (WorldLocation location in locationList)
                        {
                            AddLocationToAvailableList(location, availableRoadVectorNodeIndexes, vectorNode);
                        }
                    }
                }
            }

            // find rail track items
            railTrackItemTable = new DrawableTrackItem[railTrackDatabase.TrackItems.Length];
            for (int i = 0; i < railTrackDatabase.TrackItems.Length; i++)
            {
                FreeTrainSimulator.Models.Track.TrackItemBase trackItem = railTrackDatabase.TrackItems[i];
                DrawableTrackItem drawableTrackItem = DrawableTrackItem.CreateDrawableTrItem(trackItem);
                railTrackItemTable[i] = drawableTrackItem;
                AddLocationToAvailableList(drawableTrackItem.WorldLocation, availableRailItemIndexes, drawableTrackItem);
            }

            // find road track items
            if (roadTrackDatabase != null)
            {
                roadTrackItemTable = new DrawableTrackItem[roadTrackDatabase.TrackItems.Length];
                for (int i = 0; i < roadTrackDatabase.TrackItems.Length; i++)
                {
                    FreeTrainSimulator.Models.Track.TrackItemBase trackItem = roadTrackDatabase.TrackItems[i];
                    DrawableTrackItem drawableTrackItem = DrawableTrackItem.CreateDrawableTrItem(trackItem);
                    roadTrackItemTable[i] = drawableTrackItem;
                    AddLocationToAvailableList(drawableTrackItem.WorldLocation, availableRoadItemIndexes, drawableTrackItem);
                }
            }

            // remove double entries
            MakeUniqueLists(availableRailVectorNodeIndexes);
            MakeUniqueLists(availableRoadVectorNodeIndexes);
            MakeUniqueLists(availablePointNodeIndexes);
            MakeUniqueLists(availableRailItemIndexes);
            MakeUniqueLists(availableRoadItemIndexes);
        }

        /// <summary>
        /// From the location find the tile and then the corresponding indexes for our arrays/lists
        /// And then add the given item to the given list at the correct indexes
        /// </summary>
        /// <typeparam name="T">Type of the item we want to add to the list.</typeparam>
        /// <param name="location">Worldlocation of the item, that gives us the tile indexes</param>
        /// <param name="ArrayOfListsToAddTo">To which list we have to add the item</param>
        /// <param name="item">The item we want to add to the list, at the correct index</param>
        private void AddLocationToAvailableList<T>(in WorldLocation location, List<T>[][] ArrayOfListsToAddTo, T item)
        {
            //possibly the location is out of the allowed region (e.g. because possibly undefined).
            if (location.Tile.X < MinTileX || location.Tile.X > MaxTileX || location.Tile.Z < MinTileZ || location.Tile.Z > MaxTileZ)
                return;
            int TileXIndex = location.Tile.X - MinTileX;
            int TileZIndex = location.Tile.Z - MinTileZ;
            ArrayOfListsToAddTo[TileXIndex][TileZIndex].Add(item);
        }

        /// <summary>
        /// basically just make sure all elements in the two dimensional array have an empty list to start with.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="itemlist"></param>
        private void InitIndexedLists<T>(List<T>[][] itemlist)
        {
            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                itemlist[xindex] = new List<T>[tileZIndexStop + 1];
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    itemlist[xindex][zindex] = new List<T>();
                }
            }
        }

        /// <summary>
        /// For each list in the given 2D array, make sure the list contains only unique elements
        /// </summary>
        /// <typeparam name="T">Type of object that is in the list (not actually used)</typeparam>
        /// <param name="arrayOfLists">2D array containing non-null lists</param>
        private void MakeUniqueLists<T>(List<T>[][] arrayOfLists)
        {
            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    arrayOfLists[xindex][zindex] = arrayOfLists[xindex][zindex].Distinct().ToList();
                }
            }
        }

        /// <summary>
        /// For a vector section, generate a list of world-locations that are used to determine whether or not the
        /// vector section will be drawn when a tile is visible
        /// </summary>
        /// <param name="trackNodeIndex">Index of the tracknode</param>
        /// <param name="trackVectorSectionIndex">Index of the vector section in the tracknode</param>
        /// <param name="useRailTracks">Must we use rail or road tracks</param>
        /// <returns>A list of world locations on the vector section</returns>
        private List<WorldLocation> FindLocationList(FreeTrainSimulator.Models.Track.VectorNode vectorNode, int trackVectorSectionIndex)
        {
            List<WorldLocation> resultList = new List<WorldLocation>();

            FreeTrainSimulator.Models.Track.VectorSectionNode tvs = vectorNode.VectorSections[trackVectorSectionIndex];

            trackSections.TrackSections.TryGetValue(tvs.NodeIndex, out FreeTrainSimulator.Models.Track.TrackSection trackSection);
            if (trackSection == null)
                return resultList;

            float trackSectionLength = trackSection.Length;

            // We want to make sure all tiles that a track crosses are noted.
            // To do this, we make a box around the track (straight or curved), and for all locations of that box
            // we calculate the min and max values of the tileX and tileZ. We then return a list of 4 worldlocations
            // that contain 0 for X,Y,Z and the various min/max combinations of tileX and tileZ
            // The assumption here is that no single track section crosses a while tile of 2014 meters
            List<WorldLocation> boxList = new List<WorldLocation>();
            WorldLocation beginLocation = FindLocationInSection(tvs, trackSection, 0);
            WorldLocation endLocation = FindLocationInSection(tvs, trackSection, trackSectionLength);
            boxList.Add(beginLocation);
            boxList.Add(endLocation);
            if (trackSection.Curved)
            {   // For straight, the box effectively has zero width
                // For curved, here, the box has a width. It will be a rectangle containing begin and end node on one side.
                // On the other side it will touch the middle point of the curve/arc. 
                // The box will then contain the full curve as long as the curve is not more than 180 degrees
                WorldLocation midLocation = FindLocationInSection(tvs, trackSection, trackSectionLength / 2);

                // (deltaX, deltaZ) is a vector from begin to end.
                double deltaX = (endLocation.Location.X - endLocation.Location.X);
                double deltaZ = (endLocation.Location.Z - endLocation.Location.Z);
                deltaX += WorldLocation.TileSize * (endLocation.Tile.X - endLocation.Tile.X);
                deltaZ += WorldLocation.TileSize * (endLocation.Tile.Z - endLocation.Tile.Z);

                WorldLocation begin2Location = new WorldLocation(midLocation.Tile, (float)(midLocation.Location.X - deltaX / 2), midLocation.Location.Y, (float)(midLocation.Location.Z - deltaZ / 2));

                WorldLocation end2Location = new WorldLocation(midLocation.Tile, (float)(midLocation.Location.X + deltaX / 2), midLocation.Location.Y, (float)(midLocation.Location.Z + deltaZ / 2));

                boxList.Add(begin2Location);
                boxList.Add(end2Location);
            }

            //normalize all locations so that they are on their native tile.
            for (int i = 0; i < boxList.Count; i++)
            {
                boxList[i] = boxList[i].Normalize();
            }

            //find Max/Min of tiles
            List<int> tileXValues = boxList.Select(i => (int)i.Tile.X).ToList();
            List<int> tileZValues = boxList.Select(i => (int)i.Tile.Z).ToList();
            int minTileX = tileXValues.Min();
            int maxTileX = tileXValues.Max();
            int minTileZ = tileZValues.Min();
            int maxTileZ = tileZValues.Max();

            //create result list
            resultList.Add(new WorldLocation(minTileX, minTileZ, 0, 0, 0));
            resultList.Add(new WorldLocation(maxTileX, minTileZ, 0, 0, 0));
            resultList.Add(new WorldLocation(minTileX, maxTileZ, 0, 0, 0));
            resultList.Add(new WorldLocation(maxTileX, maxTileZ, 0, 0, 0));
            return resultList;
        }

        #endregion

        #region Drawing
        /// <summary>
        /// Draw the tracks from the track database 
        /// </summary>
        /// <param name="drawArea">The drawing area to draw upon</param>
        public void DrawTracks(DrawArea drawArea)
        {
            PrepareDrawing(drawArea);
            closestRailTrack.Reset();

            bool[] hasBeenDrawn = new bool[railTrackDatabase.TrackNodes.Length];
            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    foreach (FreeTrainSimulator.Models.Track.VectorNode vectorNode in availableRailVectorNodeIndexes[xindex][zindex])
                    {
                        if (hasBeenDrawn[vectorNode.NodeIndex])
                            continue;
                        DrawVectorNode(drawArea, vectorNode, DrawColors.colorsNormal, closestRailTrack);
                        hasBeenDrawn[vectorNode.NodeIndex] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Draw all the roads (if settings are right), in the same way as drawing the tracks
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        public void DrawRoads(DrawArea drawArea)
        {
            if (!Properties.Settings.Default.drawRoads)
                return;
            //if (roadTrackDB == null) LoadRoadTrackDB();
            PrepareDrawing(drawArea);

            ClosestRoadTrack.Reset();
            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    foreach (FreeTrainSimulator.Models.Track.VectorNode vectorNode in availableRoadVectorNodeIndexes[xindex][zindex])
                    {
                        DrawVectorNode(drawArea, vectorNode, DrawColors.colorsRoads, ClosestRoadTrack);
                    }
                }
            }
        }

        /// <summary>
        /// Draw the various highlights (tracks/roads and items/junctions/endnodes, based on what is closest to the mouse)
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        /// <param name="doAll">highlight everything possible or just train tracks</param>
        public void DrawTrackHighlights(DrawArea drawArea, bool doAll)
        {
            if (!CheckForHighlightOverrideTracks())
            {
                ClosestTrack = closestRailTrack; // we still need this for path editing
                return;
            }

            if (doAll)
            {
                if (Properties.Settings.Default.drawRoads && ClosestRoadTrack.IsCloserThan(closestRailTrack))
                {   // high light the closest road track
                    ClosestTrack = ClosestRoadTrack;
                    DrawHighlightTracks(drawArea, ClosestRoadTrack, DrawColors.colorsRoadsHighlight, DrawColors.colorsRoadsHotlight);
                }
                else
                {   //highlight the closest train track
                    ClosestTrack = closestRailTrack;
                    DrawHighlightTracks(drawArea, closestRailTrack, DrawColors.colorsHighlight, DrawColors.colorsHotlight);
                }
            }
            else
            { // basically for inset only
                DrawHighlightTracks(drawArea, closestRailTrack, DrawColors.colorsHighlight, DrawColors.colorsHotlight);
            }
        }

        /// <summary>
        /// Draw the various highlights (tracks/roads and items/junctions/endnodes, based on what is closest to the mouse)
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        public void DrawItemHighlights(DrawArea drawArea)
        {
            if (!CheckForHighlightOverrideItems())
            {
                return;
            }

            if (ClosestTrackItem.DrawableTrackItem != null && ClosestTrackItem.IsCloserThan(ClosestJunctionOrEnd))
            {
                // Highlight the closest track item
                ClosestTrackItem.DrawableTrackItem.Draw(drawArea, DrawColors.colorsHighlight, IsHighlightOverriddenTrItem);
            }
            else if (ClosestJunctionOrEnd.JunctionOrEndNode != null)
            {   // Highlight the closest junction
                if (ClosestJunctionOrEnd.Description == "junction")
                {
                    DrawJunctionNode(drawArea, ClosestJunctionOrEnd.JunctionOrEndNode, DrawColors.colorsHighlight);
                }
                else
                {
                    DrawEndNode(drawArea, ClosestJunctionOrEnd.JunctionOrEndNode, DrawColors.colorsHighlight);
                }
            }

        }

        /// <summary>
        /// Highlight tracks (either from train or from road track)
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        /// <param name="closeToMouseTrack">The train/road track that is closest to the mouse</param>
        /// <param name="highColors">Colorscheme for highlights</param>
        /// <param name="hotColors">Colorscheme for hotlights</param>
        private void DrawHighlightTracks(DrawArea drawArea, CloseToMouseTrack closeToMouseTrack, ColorScheme highColors, ColorScheme hotColors)
        {
            DrawVectorNode(drawArea, closeToMouseTrack.TrackNode as FreeTrainSimulator.Models.Track.VectorNode, highColors, null);
            if (Properties.Settings.Default.statusShowVectorSections)
            {
                DrawTrackSection(drawArea, closeToMouseTrack.TrackNode as FreeTrainSimulator.Models.Track.VectorNode, closeToMouseTrack.VectorSection, hotColors, null, -1);
            }
        }

        /// <summary>
        /// Draw the track of a MSTS vectorNode (from track database)
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        /// <param name="tn">The tracknode from track database (assumed to be a vector node)</param>
        /// <param name="colors">Colorscheme to use</param>
        /// <param name="closeToMouseTrack">The object to track which vector node is closest to the mouse</param>
        private void DrawVectorNode(DrawArea drawArea, FreeTrainSimulator.Models.Track.VectorNode tn, ColorScheme colors, CloseToMouseTrack closeToMouseTrack)
        {
            if (tn == null)
                return;
            for (int tvsi = 0; tvsi < tn.VectorSections.Length; tvsi++)
            {
                FreeTrainSimulator.Models.Track.VectorSectionNode tvs = tn.VectorSections[tvsi];
                DrawTrackSection(drawArea, tn, tvs, colors, closeToMouseTrack, tvsi);
            }
        }

        /// <summary>
        /// Draw a specific vectorSection of a vectorNode
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        /// <param name="tn">The tracknode from track database (assumed to be a vector node)</param>
        /// <param name="tvs">The vectorSection itself that needs to be drawn</param>
        /// <param name="colors">Colorscheme to use</param>
        /// <param name="closeToMouseTrack">The object to track which vector node is closest to the mouse</param>
        /// <param name="tvsi">The index of the trackvector section, needed only for closeToMouseTrack</param>
        /// <remarks>Note that his is very similar to DrawTrackSection in class DrawPath, but this one always
        /// draws the whole section and it checks the distance to the mouse</remarks>
        private void DrawTrackSection(DrawArea drawArea, FreeTrainSimulator.Models.Track.VectorNode tn, FreeTrainSimulator.Models.Track.VectorSectionNode tvs, ColorScheme colors, CloseToMouseTrack closeToMouseTrack, int tvsi)
        {
            if (tvs == null)
                return;
            trackSections.TrackSections.TryGetValue(tvs.NodeIndex, out FreeTrainSimulator.Models.Track.TrackSection trackSection);
            if (trackSection == null)
                return;

            ref readonly WorldLocation thisLocation = ref tvs.Location;
            if (closeToMouseTrack != null)
            {
                closeToMouseTrack.CheckMouseDistance(thisLocation, drawArea.MouseLocation, tn, tvs, tvsi, drawArea.Scale);
            }

            if (trackSection.Curved)
            {
                drawArea.DrawArc(trackSection.Gauge, colors.TrackCurved, thisLocation,
                    trackSection.Radius, tvs.Direction.Y, trackSection.Angle, 0);
            }
            else
            {
                drawArea.DrawLine(trackSection.Gauge, colors.TrackStraight, thisLocation,
                    trackSection.Length, tvs.Direction.Y, 0);
            }
        }

        /// <summary>
        /// Draw all the junction and endNodes
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        public void DrawJunctionAndEndNodes(DrawArea drawArea)
        {
            ClosestJunctionOrEnd.Reset();
            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    foreach (FreeTrainSimulator.Models.Track.TrackNodeBase tn in availablePointNodeIndexes[xindex][zindex])
                    {
                        if (tn is FreeTrainSimulator.Models.Track.JunctionNode && Properties.Settings.Default.showJunctionNodes)
                        {
                            DrawJunctionNode(drawArea, tn, DrawColors.colorsNormal);
                        }
                        else if (tn is FreeTrainSimulator.Models.Track.EndNode && Properties.Settings.Default.showEndNodes)
                        {
                            DrawEndNode(drawArea, tn, DrawColors.colorsNormal);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draw a specific junction node.
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="tn">The trackNode (assumed to be a activeNodeAsJunction)</param>
        /// <param name="colors">The colorscheme to use for drawing the activeNodeAsJunction</param>
        private void DrawJunctionNode(DrawArea drawArea, FreeTrainSimulator.Models.Track.TrackNodeBase tn, ColorScheme colors)
        {
            ClosestJunctionOrEnd.CheckMouseDistance(tn.Location, drawArea.MouseLocation, tn, "junction");
            drawArea.DrawTexture(tn.Location, "disc", 3f, 2, colors.Junction);
        }

        /// <summary>
        /// Draw a specific end node.
        /// </summary>
        /// <param name="drawArea">The area to draw upon</param>
        /// <param name="tn">The trackNode (assumed to be a activeNodeAsJunction)</param>
        /// <param name="colors">The colorscheme to use for drawing the activeNodeAsJunction</param>
        private void DrawEndNode(DrawArea drawArea, FreeTrainSimulator.Models.Track.TrackNodeBase tn, ColorScheme colors)
        {
            ClosestJunctionOrEnd.CheckMouseDistance(tn.Location, drawArea.MouseLocation, tn, "endnode");
            float angle = endnodeAngles[tn.NodeIndex];
            drawArea.DrawLine(3f, colors.EndNode, tn.Location, 2f, angle, 0);
        }

        /// <summary>
        /// Draw the various track items like signals, crossings, etc
        /// </summary>
        /// <param name="drawArea">Area to draw the items on</param>
        public void DrawTrackItems(DrawArea drawArea)
        {

            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    foreach (DrawableTrackItem trackItem in availableRailItemIndexes[xindex][zindex])
                    {
                        if (trackItem.Draw(drawArea, DrawColors.colorsNormal, IsHighlightOverriddenTrItem))
                        {
                            ClosestTrackItem.CheckMouseDistance(trackItem.WorldLocation, drawArea.MouseLocation, trackItem);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draw the various road track items, mainly car spawners but also level crossings (again).
        /// </summary>
        /// <param name="drawArea">Area to draw the items on</param>
        public void DrawRoadTrackItems(DrawArea drawArea)
        {
            ClosestTrackItem.Reset(); // dirtily assumes this is called before normal track items
            // we only want the carspawners here
            if (!Properties.Settings.Default.showCarSpawners && !Properties.Settings.Default.showRoadCrossings)
                return;

            for (int xindex = tileXIndexStart; xindex <= tileXIndexStop; xindex++)
            {
                for (int zindex = tileZIndexStart; zindex <= tileZIndexStop; zindex++)
                {
                    foreach (DrawableTrackItem trackItem in availableRoadItemIndexes[xindex][zindex])
                    {
                        if (trackItem.Draw(drawArea, DrawColors.colorsNormal, IsHighlightOverriddenTrItem))
                        {
                            ClosestTrackItem.CheckMouseDistance(trackItem.WorldLocation, drawArea.MouseLocation, trackItem);
                        }
                    }
                }
            }
        }
        #endregion

        #region Searching and highlight override
        /// <summary>
        /// From the track node index find the tracknode, find its location, and prepare hightlighting
        /// </summary>
        /// <param name="tni">The trackNodeIndex identifying the tracknode</param>
        /// <returns>The eturn the (center) location of a tracknode or WorldLocation.None if no tracknode could be identified</returns>
        public WorldLocation TrackNodeHighlightOverride(int tni)
        {
            if ((tni < 0) || (tni >= railTrackDatabase.TrackNodes.Length))
                return WorldLocation.None;
            FreeTrainSimulator.Models.Track.TrackNodeBase tn = railTrackDatabase.TrackNodes[tni];
            if (tn == null)
                return WorldLocation.None;

            IsHighlightOverridden = true;
            if (tn is FreeTrainSimulator.Models.Track.JunctionNode)
            {
                searchJunctionOrEnd = new CloseToMouseJunctionOrEnd(tn, "junction");
                return tn.Location;
            }
            else if (tn is FreeTrainSimulator.Models.Track.EndNode)
            {
                searchJunctionOrEnd = new CloseToMouseJunctionOrEnd(tn, "endnode");
                return tn.Location;
            }


            //vector node. 
            searchTrack = new CloseToMouseTrack(tn);

            var nodeConnectors = railTrackDatabase.TrackNodeConnectors[tni].TrackNodeConnectors;
            FreeTrainSimulator.Models.Track.TrackNodeBase nodeBehind = railTrackDatabase.TrackNodes[nodeConnectors[0].Link];
            FreeTrainSimulator.Models.Track.TrackNodeBase nodeAhead = railTrackDatabase.TrackNodes[nodeConnectors[1].Link];
            return TrackLocation(tn, nodeBehind, nodeAhead);
        }

        /// <summary>
        /// From the track node index find the tracknode, find its location, and prepare hightlighting
        /// </summary>
        /// <param name="tni">The trackNodeIndex identifying the tracknode</param>
        /// <returns>The eturn the (center) location of a tracknode or Worldlocation.None if no tracknode could be identified</returns>
        public WorldLocation TrackNodeHighlightOverrideRoad(int tni)
        {
            if (roadTrackDatabase == null)
                return WorldLocation.None;
            if ((tni < 0) || (tni >= roadTrackDatabase.TrackNodes.Length))
                return WorldLocation.None;
            FreeTrainSimulator.Models.Track.TrackNodeBase tn = roadTrackDatabase.TrackNodes[tni];
            if (tn == null)
                return WorldLocation.None;

            IsHighlightOverridden = true;

            if (tn is FreeTrainSimulator.Models.Track.EndNode)
            {
                searchJunctionOrEnd = new CloseToMouseJunctionOrEnd(tn, "endnode");
                return tn.Location;
            }

            //vector node
            searchTrack = new CloseToMouseTrack(tn);
            var nodeConnectors = roadTrackDatabase.TrackNodeConnectors[tni].TrackNodeConnectors;
            FreeTrainSimulator.Models.Track.TrackNodeBase nodeBehind = roadTrackDatabase.TrackNodes[nodeConnectors[0].Link];
            FreeTrainSimulator.Models.Track.TrackNodeBase nodeAhead = roadTrackDatabase.TrackNodes[nodeConnectors[1].Link];
            return TrackLocation(tn, nodeBehind, nodeAhead);
        }

        /// <summary>
        /// Find the item with the given index. And if it exists, prepare for highlighting it
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <returns>The location of the found item (or WorldLocation.None)</returns>
        public WorldLocation TrackItemHighlightOverride(int itemIndex)
        {
            IsHighlightOverriddenTrItem = false; // do not show all items, just yet. Only after CheckForHighlightOverride
            if ((itemIndex < 0) || (itemIndex >= railTrackItemTable.Length))
                return WorldLocation.None;
            IsHighlightOverridden = true;
            DrawableTrackItem item = railTrackItemTable[itemIndex];
            searchTrItem = new CloseToMouseItem(item);
            return item.WorldLocation;
        }

        /// <summary>
        /// Find the road item with the given index. And if it exists, prepare for highlighting it
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <returns>The location of the found item (or WorldLocation.None)</returns>
        public WorldLocation TrackItemHighlightOverrideRoad(int itemIndex)
        {
            IsHighlightOverriddenTrItem = false; // do not show all items, just yet. Only after CheckForHighlightOverride
            if (roadTrackDatabase == null)
                return WorldLocation.None;
            if ((itemIndex < 0) || (itemIndex >= roadTrackItemTable.Length))
                return WorldLocation.None;
            IsHighlightOverridden = true;
            DrawableTrackItem item = roadTrackItemTable[itemIndex];
            searchTrItem = new CloseToMouseItem(item);
            return item.WorldLocation;
        }

        /// <summary>
        /// We need to store the nodes/items that the user was searching for, so we can highlight them
        /// </summary>
        private CloseToMouseJunctionOrEnd searchJunctionOrEnd;
        private CloseToMouseTrack searchTrack;
        private CloseToMouseItem searchTrItem;

        /// <summary>
        /// Clear all override highlights, returning to highlights based on mouse location
        /// </summary>
        public void ClearHighlightOverrides()
        {
            IsHighlightOverriddenTrItem = false;
            IsHighlightOverridden = false;
            searchJunctionOrEnd = null;
            searchTrack = null;
            searchTrItem = null;
        }

        /// <summary>
        /// Check whether there is an highlight override for tracks (meaning the highlight is coming from a search, 
        /// not from being closest to the mouse), and if there is make sure the track to highlighted is indeed used.
        /// </summary>
        /// <returns>True in case the highlight needs to be drawn</returns>
        private bool CheckForHighlightOverrideTracks()
        {
            if (!IsHighlightOverridden)
            {
                return Properties.Settings.Default.showTrackHighlights;
            }

            // To be sure the inset also shows the correct track, we need to make sure to make a deeper copy, instead
            // of changing only the reference.
            if (searchTrack != null)
            {
                closestRailTrack = new CloseToMouseTrack(searchTrack.TrackNode);
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Check whether there is an highlight override for items (meaning the highlight is coming from a search, 
        /// not from being closest to the mouse), and if there is make sure the item to highlighted is indeed used.
        /// </summary>
        /// <returns>True in case the highlight needs to be drawn</returns>
        private bool CheckForHighlightOverrideItems()
        {
            IsHighlightOverriddenTrItem = (IsHighlightOverridden && (searchTrItem != null));
            if (!IsHighlightOverridden)
            {
                return Properties.Settings.Default.showItemHighlights;
            }

            bool foundHighlightItem = false;
            if (searchJunctionOrEnd != null)
            {
                ClosestJunctionOrEnd = searchJunctionOrEnd;
                foundHighlightItem = true;
            }

            if (searchTrItem != null)
            {
                ClosestTrackItem = searchTrItem;
                foundHighlightItem = true;
            }

            return foundHighlightItem;
        }
        #endregion

        #region Utilities
        /// <summary>
        /// return a single location that can be used to zoom around a track vector node
        /// </summary>
        /// <param name="tn">The trackNode self, assumed to be a vector node</param>
        /// <param name="nodeBehind">The junction or end node at the beginning of the vector node</param>
        /// <param name="nodeAhead">The junction or end node at the end of the vector node</param>
        /// <returns>The worldlocation describing the track</returns>
        /// <remarks>Obviously, a single location is always an estimate. Currently tries to find middle of end points</remarks>
        private static WorldLocation TrackLocation(FreeTrainSimulator.Models.Track.TrackNodeBase tn, FreeTrainSimulator.Models.Track.TrackNodeBase nodeBehind, FreeTrainSimulator.Models.Track.TrackNodeBase nodeAhead)
        {
            if (tn is not FreeTrainSimulator.Models.Track.VectorNode tvn)
                return WorldLocation.None;
            if (nodeBehind == null)
            {
                if (nodeAhead == null)
                {
                    // no junctions or end node at both sides. Oh, well, just take the first point
                    FreeTrainSimulator.Models.Track.VectorSectionNode tvs = tvn.VectorSections[0];
                    return tvs.Location.SetElevation(0);
                }
                else
                {
                    return nodeAhead.Location;
                }
            }
            else
            {
                if (nodeAhead == null)
                {
                    return nodeBehind.Location;
                }
                else
                {
                    return MiddleLocation(nodeBehind.Location, nodeAhead.Location);
                }
            }

        }

        /// <summary>
        /// Return the location in the middle between the two given points.
        /// </summary>
        /// <param name="location1">Location of first point</param>
        /// <param name="location2">Location of second point</param>
        /// <returns>middle of both points</returns>
        /// <remarks>Should perhaps be in the WorldLocation class itself</remarks>
        private static WorldLocation MiddleLocation(in WorldLocation location1, in WorldLocation location2)
        {
            WorldLocation location2Normalized = location2.NormalizeTo(location1.Tile);
            Vector3 middleVector = (location1.Location + location2Normalized.Location) / 2;
            return new WorldLocation(location1.Tile, middleVector);
        }

        /// <summary>
        /// find the WorldLocation given the indexes to the vector node, vector section and distance into the section.
        /// </summary>
        /// <param name="trackNodeIndex"></param>
        /// <param name="trackVectorSectionIndex"></param>
        /// <param name="distanceAlongSection"></param>
        /// <param name="useRailTracks">Must we use rail or road tracks</param>
        public WorldLocation FindLocation(int trackNodeIndex, int trackVectorSectionIndex, float distanceAlongSection, bool useRailTracks)
        {
            try
            {
                FreeTrainSimulator.Models.Track.TrackDatabase database = useRailTracks ? railTrackDatabase : roadTrackDatabase;
                if (database == null || database.TrackNodes[trackNodeIndex] is not FreeTrainSimulator.Models.Track.VectorNode vectorNode)
                    return WorldLocation.None;

                FreeTrainSimulator.Models.Track.VectorSectionNode tvs = vectorNode.VectorSections[trackVectorSectionIndex];

                trackSections.TrackSections.TryGetValue(tvs.NodeIndex, out FreeTrainSimulator.Models.Track.TrackSection trackSection);

                return FindLocationInSection(tvs, trackSection, distanceAlongSection);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return WorldLocation.None;
            }
        }

        /// <summary>
        /// Find the world location on a track
        /// </summary>
        /// <param name="tvs">Track vector section for which you want the location</param>
        /// <param name="trackSection">Track section corresponding to the track vector section. Could in principle be found from tvs, but if it is given, this is faster.</param>
        /// <param name="distanceAlongSection">Distance along the track</param>
        /// <returns></returns>
        private static WorldLocation FindLocationInSection(FreeTrainSimulator.Models.Track.VectorSectionNode tvs, FreeTrainSimulator.Models.Track.TrackSection trackSection, float distanceAlongSection)
        {
            ref readonly WorldLocation location = ref tvs.Location;

            float cosA = (float)Math.Cos(tvs.Direction.Y);
            float sinA = (float)Math.Sin(tvs.Direction.Y);
            if (!trackSection.Curved)
            {
                // note, angle is 90 degrees off, and different sign. 
                // So Delta X = cos(90-A)=sin(A); Delta Y,Z = sin(90-A) = cos(A)    
                return new WorldLocation(location.Tile, location.Location.X + sinA * distanceAlongSection, location.Location.Y, location.Location.Z + cosA * distanceAlongSection);
            }
            else
            {
                int sign = (trackSection.Angle > 0) ? -1 : 1;
                float angleRadians = -distanceAlongSection / trackSection.Radius;
                float cosArotated = (float)Math.Cos(tvs.Direction.Y + sign * angleRadians);
                float sinArotated = (float)Math.Sin(tvs.Direction.Y + sign * angleRadians);
                float deltaX = sign * trackSection.Radius * (cosA - cosArotated);
                float deltaZ = sign * trackSection.Radius * (sinA - sinArotated);
                return new WorldLocation(location.Tile, location.Location.X - deltaX, location.Location.Y, location.Location.Z + deltaZ);
            }
        }
        #endregion
    }
}
