// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

/* SCENERY
 * 
 * Scenery objects are specified in WFiles located in the WORLD folder of the route.
 * Each WFile describes scenery for a 2048 meter square region of the route.
 * This assembly is responsible for loading and unloading the WFiles as 
 * the camera moves over the route.  
 * 
 * Loaded WFiles are each represented by an instance of the WorldFile class. 
 * 
 * A SceneryDrawer object is created by the Viewer. Each time SceneryDrawer.Update is 
 * called, it disposes of WorldFiles that have gone out of range, and creates new 
 * WorldFile objects for WFiles that have come into range.
 * 
 * Currently the SceneryDrawer. Update is called 10 times a second from a background 
 * thread in the Viewer class.
 * 
 * SceneryDrawer loads the WFile in which the viewer is located, and the 8 WFiles 
 * surrounding the viewer.
 * 
 * When a WorldFile object is created, it creates StaticShape objects for each scenery
 * item.  The StaticShape objects add themselves to the Viewer's content list, sharing
 * mesh files and textures wherever possible.
 * 
 */

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Models;
using Orts.Simulation;
using Orts.Simulation.World;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Orts.ActivityRunner.Viewer3D
{
    public class SceneryDrawer
    {
        private readonly Viewer Viewer;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        public List<WorldFile> WorldFiles = new List<WorldFile>();
        private int TileX;
        private int TileZ;
        private int VisibleTileX;
        private int VisibleTileZ;

        public SceneryDrawer(Viewer viewer)
        {
            Viewer = viewer;
        }

        public void Load()
        {
            var cancellation = Viewer.LoaderProcess.CancellationToken;

            if (TileX != VisibleTileX || TileZ != VisibleTileZ)
            {
                TileX = VisibleTileX;
                TileZ = VisibleTileZ;
                var worldFiles = WorldFiles;
                var newWorldFiles = new List<WorldFile>();
                var oldWorldFiles = new List<WorldFile>(worldFiles);
                var needed = (int)Math.Ceiling((float)Viewer.Settings.ViewingDistance / 2048f);
                for (var x = -needed; x <= needed; x++)
                {
                    for (var z = -needed; z <= needed; z++)
                    {
                        if (cancellation.IsCancellationRequested)
                            break;
                        var tile = worldFiles.FirstOrDefault(t => t.Tile.X == TileX + x && t.Tile.Z == TileZ + z);
                        if (tile == null)
                            tile = LoadWorldFile(TileX + x, TileZ + z, x == 0 && z == 0);
                        if (tile != null)
                        {
                            newWorldFiles.Add(tile);
                            oldWorldFiles.Remove(tile);
                        }
                    }
                }
                foreach (var tile in oldWorldFiles)
                    tile.Unload();
                WorldFiles = newWorldFiles;
                Viewer.TryLoadingNightTextures = true; // when Tiles loaded change you can try
                Viewer.TryLoadingDayTextures = true; // when Tiles loaded change you can try
            }
            else if (Viewer.NightTexturesNotLoaded && !Viewer.ClockTimeBeforeNoon && Viewer.TryLoadingNightTextures)
            {
                var sunHeight = Viewer.MaterialManager.sunDirection.Y;
                if (sunHeight < 0.10f && sunHeight > 0.01)
                {
                    long remainingMemorySpace = Viewer.LoadMemoryThreshold - System.Environment.WorkingSet;
                    if (remainingMemorySpace >= 0) // if not we'll try again
                    {
                        // Night is coming, it's time to load the night textures
                        var success = Viewer.MaterialManager.LoadNightTextures();
                        if (success)
                        {
                            Viewer.NightTexturesNotLoaded = false;
                        }
                    }
                    Viewer.TryLoadingNightTextures = false;
                }
                else if (sunHeight <= 0.01)
                    Viewer.NightTexturesNotLoaded = false; // too late to try, we must give up and we don't load the night textures
            }
            else if (Viewer.DayTexturesNotLoaded && Viewer.ClockTimeBeforeNoon && Viewer.TryLoadingDayTextures)
            {
                var sunHeight = Viewer.MaterialManager.sunDirection.Y;
                if (sunHeight > -0.10f && sunHeight < -0.01)
                {
                    long remainingMemorySpace = Viewer.LoadMemoryThreshold - System.Environment.WorkingSet;
                    if (remainingMemorySpace >= 0) // if not we'll try again
                    {
                        // Day is coming, it's time to load the day textures
                        var success = Viewer.MaterialManager.LoadDayTextures();
                        if (success)
                        {
                            Viewer.DayTexturesNotLoaded = false;
                        }
                    }
                    Viewer.TryLoadingDayTextures = false;
                }
                else if (sunHeight >= -0.01)
                    Viewer.DayTexturesNotLoaded = false; // too late to try, we must give up and we don't load the day textures. TODO: is this OK?
            }
        }

        internal void Mark()
        {
            var worldFiles = WorldFiles;
            foreach (var tile in worldFiles)
                tile.Mark();
        }

        public float GetBoundingBoxTop(in WorldLocation location, float blockSize)
        {
            return GetBoundingBoxTop(location.TileX, location.TileZ, location.Location.X, location.Location.Z, blockSize);
        }

        public float GetBoundingBoxTop(int tileX, int tileZ, float x, float z, float blockSize)
        {
            // Normalize the coordinates to the right tile.
            while (x >= 1024) { x -= 2048; tileX++; }
            while (x < -1024) { x += 2048; tileX--; }
            while (z >= 1024) { z -= 2048; tileZ++; }
            while (z < -1024) { z += 2048; tileZ--; }

            // Fetch the tile we're looking up elevation for; if it isn't loaded, no elevation.
            var worldFiles = WorldFiles;
            var worldFile = worldFiles.FirstOrDefault(wf => wf.Tile.X == tileX && wf.Tile.Z == tileZ);
            if (worldFile == null)
                return float.MinValue;

            return worldFile.GetBoundingBoxTop(x, z, blockSize);
        }

        public void Update(in ElapsedTime elapsedTime)
        {
            var worldFiles = WorldFiles;
            foreach (var worldFile in worldFiles)
                worldFile.Update(elapsedTime);
        }

        public void LoadPrep()
        {
            VisibleTileX = Viewer.Camera.TileX;
            VisibleTileZ = Viewer.Camera.TileZ;
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var worldFiles = WorldFiles;
            foreach (var worldFile in worldFiles)
                // TODO: This might impair some shadows.
                if (Viewer.Camera.InFov(new Vector3((worldFile.Tile.X - Viewer.Camera.TileX) * 2048, 0, (worldFile.Tile.Z - Viewer.Camera.TileZ) * 2048), 1448))
                    worldFile.PrepareFrame(frame, elapsedTime);
        }

        private WorldFile LoadWorldFile(int tileX, int tileZ, bool visible)
        {
            Trace.Write("W");
            try
            {
                return new WorldFile(Viewer, tileX, tileZ, visible);
            }
            catch (FileLoadException error)
            {
                Trace.WriteLine(error);
                return null;
            }
        }
    }

    public class WorldFile: ITileCoordinate<Tile>
    {
        private const int MinimumInstanceCount = 5;

        public List<BaseShape> SceneryObjects { get; } = new List<BaseShape>();
        public List<DynamicTrackViewer> DynamicTrackList { get; } = new List<DynamicTrackViewer>();
        public List<ForestViewer> ForestList { get; } = new List<ForestViewer>();
        public List<RoadCarSpawner> CarSpawners { get; } = new List<RoadCarSpawner>();
        public List<TrItemLabel> Sidings { get; } = new List<TrItemLabel>();
        public List<TrItemLabel> Platforms { get; } = new List<TrItemLabel>();
        public List<PickupObject> PickupList { get; } = new List<PickupObject>();
        public List<BoundingBox> BoundingBoxes { get; } = new List<BoundingBox>();
        
        private readonly Tile tile;

        public ref readonly Tile Tile => ref tile;

        private readonly Viewer Viewer;

        /// <summary>
        /// Open the specified WFile and load all the scenery objects into the viewer.
        /// If the file doesn't exist, then return an empty WorldFile object.
        /// </summary>
        /// <param name="visible">Tiles adjacent to the current visible tile may not be modelled.
        /// This flag decides whether a missing file leads to a warning message.</param>
        public WorldFile(Viewer viewer, int tileX, int tileZ, bool visible)
        {
            Viewer = viewer;
            tile = new Tile(tileX, tileZ);

            var cancellation = Viewer.LoaderProcess.CancellationToken;

            // determine file path to the WFile at the specified tile coordinates
            var WFileName = WorldFileNameFromTileCoordinates(tileX, tileZ);
            var WFilePath = Path.Combine(viewer.Simulator.RouteFolder.WorldFolder, WFileName);

            // if there isn't a file, then return with an empty WorldFile object
            if (!File.Exists(WFilePath))
            {
                if (visible)
                    Trace.TraceWarning("World file missing - {0}", WFilePath);
                return;
            }

            // read the world file 
            var WFile = new Formats.Msts.Files.WorldFile(WFilePath);

            // check for existence of world file in OpenRails subfolder
            WFilePath = Path.Combine(viewer.Simulator.RouteFolder.WorldFolder, "Openrails", WFileName);
            if (File.Exists(WFilePath))
            {
                // We have an OR-specific addition to world file
                WFile.InsertORSpecificData(WFilePath);
            }

            // to avoid loop checking for every object this pre-check is performed
            bool containsMovingTable = Simulator.Instance.MovingTables.Where(m => m.WFile.Equals(WFileName, StringComparison.OrdinalIgnoreCase)).Any();

            // create all the individual scenery objects specified in the WFile
            foreach (var worldObject in WFile.Objects)
            {
                if (worldObject.DetailLevel > viewer.Settings.WorldObjectDensity)
                    continue;

                // If the loader has been asked to temrinate, bail out early.
                if (cancellation.IsCancellationRequested)
                    break;

                // Get the position of the scenery object into ORTS coordinate space.
                ref readonly WorldPosition worldMatrix = ref worldObject.WorldPosition;

                var shadowCaster = (worldObject.StaticFlags & (uint)StaticFlag.AnyShadow) != 0 || viewer.Settings.ShadowAllShapes;
                var animated = (worldObject.StaticFlags & (uint)StaticFlag.Animate) != 0;
                var global = (worldObject is TrackObject) || (worldObject is HazardObject) || (worldObject.StaticFlags & (uint)StaticFlag.Global) != 0;

                // TransferObj have a FileName but it is not a shape, so we need to avoid sanity-checking it as if it was.
                var fileNameIsNotShape = (worldObject is TransferObject || worldObject is HazardObject);

                // Determine the file path to the shape file for this scenery object and check it exists as expected.
                string shapeFilePath = fileNameIsNotShape || string.IsNullOrEmpty(worldObject.FileName) ? null : global ? viewer.Simulator.RouteFolder.ContentFolder.ShapeFile(worldObject.FileName) : viewer.Simulator.RouteFolder.ShapeFile(worldObject.FileName);
                if (shapeFilePath != null)
                {
                    shapeFilePath = Path.GetFullPath(shapeFilePath);
                    if (!File.Exists(shapeFilePath))
                    {
                        Trace.TraceWarning("{0} scenery object {1} with StaticFlags {3:X8} references non-existent {2}", WFileName, worldObject.UiD, shapeFilePath, worldObject.StaticFlags);
                        shapeFilePath = null;
                    }
                }

                if (shapeFilePath != null && File.Exists(shapeFilePath + "d"))
                {
                    var shape = new ShapeDescriptorFile(shapeFilePath + "d");
                    if (shape.Shape.EsdBoundingBox != null)
                    {
                        var min = shape.Shape.EsdBoundingBox.Min;
                        var max = shape.Shape.EsdBoundingBox.Max;
                        var transform = Matrix.Invert(worldMatrix.XNAMatrix);
                        // Not sure if this is needed, but it is to correct for center-of-gravity being not the center of the box.
                        //transform.M41 += (max.X + min.X) / 2;
                        //transform.M42 += (max.Y + min.Y) / 2;
                        //transform.M43 += (max.Z + min.Z) / 2;
                        BoundingBoxes.Add(new BoundingBox(transform, new Vector3((max.X - min.X) / 2, (max.Y - min.Y) / 2, (max.Z - min.Z) / 2), worldMatrix.XNAMatrix.Translation.Y));
                    }
                }

                try
                {
                    if (worldObject.GetType() == typeof(TrackObject))
                    {
                        var trackObj = (TrackObject)worldObject;
                        // Switch tracks need a link to the simulator engine so they can animate the points.
                        TrackJunctionNode trJunctionNode = trackObj.WorldLocation != WorldLocation.None ? RuntimeData.Instance.TrackDB.GetJunctionNode(tile.X, tile.Z, (int)trackObj.UiD) : null;
                        // We might not have found the junction node; if so, fall back to the static track shape.
                        if (trJunctionNode != null)
                        {
                            if (viewer.Settings.UseSuperElevation > 0) SuperElevationManager.DecomposeStaticSuperElevation(viewer, DynamicTrackList, trackObj, worldMatrix, tile.X, tile.Z, shapeFilePath);
                            SceneryObjects.Add(new SwitchTrackShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), trJunctionNode));
                        }
                        else
                        {
                            //if want to use super elevation, we will generate tracks using dynamic tracks
                            if (viewer.Settings.UseSuperElevation > 0
                                && SuperElevationManager.DecomposeStaticSuperElevation(viewer, DynamicTrackList, trackObj, worldMatrix, tile.X, tile.Z, shapeFilePath))
                            {
                                //var success = SuperElevation.DecomposeStaticSuperElevation(viewer, dTrackList, trackObj, worldMatrix, TileX, TileZ, shapeFilePath);
                                //if (success == 0) sceneryObjects.Add(new StaticTrackShape(viewer, shapeFilePath, worldMatrix));
                            }
                            //otherwise, use shapes
                            else if (!containsMovingTable) SceneryObjects.Add(new StaticTrackShape(shapeFilePath, worldMatrix));
                            else
                            {
                                var found = false;
                                foreach (MovingTable movingTable in Simulator.Instance.MovingTables)
                                {
                                    if (worldObject.UiD == movingTable.UID && WFileName == movingTable.WFile)
                                    {
                                        found = true;
                                        if (movingTable is TurnTable turnTable)
                                        {
                                            turnTable.ComputeCenter(worldMatrix);
                                            Quaternion quaternion = Quaternion.CreateFromRotationMatrix(worldObject.WorldPosition.XNAMatrix);
                                            //quaternion.Z *= -1;
                                            var startingY = Math.Asin(-2 * (quaternion.X * quaternion.Z - quaternion.Y * quaternion.W));
                                            //var startingY = Math.Asin(-2 * (worldObject.QDirection.A * worldObject.QDirection.C - worldObject.QDirection.B * worldObject.QDirection.D));
                                            SceneryObjects.Add(new TurntableShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None, turnTable, startingY));
                                        }
                                        else if (movingTable is TransferTable transferTable)
                                        {
                                            transferTable.ComputeCenter(worldMatrix);
                                            SceneryObjects.Add(new TransfertableShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None, transferTable));
                                        }
                                        break;
                                    }
                                }
                                if (!found) SceneryObjects.Add(new StaticTrackShape(shapeFilePath, worldMatrix));
                            }
                        }
                        if (viewer.Simulator.Settings.Wire == true && viewer.Simulator.Route.Electrified == true
                            && worldObject.DetailLevel != 2   // Make it compatible with routes that use 'HideWire', a workaround for MSTS that 
                            && worldObject.DetailLevel != 3   // allowed a mix of electrified and non electrified track see http://msts.steam4me.net/tutorials/hidewire.html
                            )
                        {
                            int success = Wire.DecomposeStaticWire(viewer, DynamicTrackList, trackObj, worldMatrix);
                            //if cannot draw wire, try to see if it is converted. modified for DynaTrax
                            if (success == 0 && trackObj.FileName.Contains("Dyna", StringComparison.OrdinalIgnoreCase)) Wire.DecomposeConvertedDynamicWire(viewer, DynamicTrackList, trackObj, worldMatrix);
                        }
                    }
                    else if (worldObject.GetType() == typeof(DynamicTrackObject))
                    {
                        if (viewer.Simulator.Settings.Wire && viewer.Simulator.Route.Electrified)
                            Wire.DecomposeDynamicWire(viewer, DynamicTrackList, (DynamicTrackObject)worldObject, worldMatrix);
                        // Add DyntrackDrawers for individual subsections
                        if (viewer.Settings.UseSuperElevation > 0 && SuperElevationManager.UseSuperElevationDyn(viewer, DynamicTrackList, (DynamicTrackObject)worldObject, worldMatrix))
                            SuperElevationManager.DecomposeDynamicSuperElevation(viewer, DynamicTrackList, (DynamicTrackObject)worldObject, worldMatrix);
                        else DynamicTrack.Decompose(viewer, DynamicTrackList, (DynamicTrackObject)worldObject, worldMatrix);

                    } // end else if DyntrackObj
                    else if (worldObject.GetType() == typeof(Formats.Msts.Models.ForestObject))
                    {
                        if (!(worldObject as Formats.Msts.Models.ForestObject).IsYard)
                            ForestList.Add(new ForestViewer(viewer, (Formats.Msts.Models.ForestObject)worldObject, worldMatrix));
                    }
                    else if (worldObject.GetType() == typeof(Formats.Msts.Models.SignalObject))
                    {
                        SceneryObjects.Add(new SignalShape((SignalObject)worldObject, shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None));
                    }
                    else if (worldObject.GetType() == typeof(TransferObject))
                    {
                        SceneryObjects.Add(new TransferShape((TransferObject)worldObject, worldMatrix));
                    }
                    else if (worldObject.GetType() == typeof(LevelCrossingObject))
                    {
                        SceneryObjects.Add(new LevelCrossingShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None, (LevelCrossingObject)worldObject));
                    }
                    else if (worldObject.GetType() == typeof(HazardObject))
                    {
                        var h = HazardShape.CreateHazard(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None, (HazardObject)worldObject);
                        if (h != null) SceneryObjects.Add(h);
                    }
                    else if (worldObject.GetType() == typeof(SpeedPostObject))
                    {
                        SceneryObjects.Add(new SpeedPostShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), (SpeedPostObject)worldObject));
                    }
                    else if (worldObject.GetType() == typeof(CarSpawnerObject))
                    {
                        if (Simulator.Instance.CarSpawnerLists != null && ((CarSpawnerObject)worldObject).ListName != null)
                        {
                            // TODO 20210712 need to find a more robust solution "(Simulator.Instance.CarSpawnerLists as List<CarSpawners>).FindIndex"
                            ((CarSpawnerObject)worldObject).CarSpawnerListIndex = (Simulator.Instance.CarSpawnerLists as List<CarSpawners>).FindIndex(x => x.ListName == ((CarSpawnerObject)worldObject).ListName);
                            if (((CarSpawnerObject)worldObject).CarSpawnerListIndex < 0 || ((CarSpawnerObject)worldObject).CarSpawnerListIndex > Simulator.Instance.CarSpawnerLists.Count - 1) ((CarSpawnerObject)worldObject).CarSpawnerListIndex = 0;
                        }
                        else ((CarSpawnerObject)worldObject).CarSpawnerListIndex = 0;
                        CarSpawners.Add(new RoadCarSpawner(viewer, worldMatrix, (CarSpawnerObject)worldObject));
                    }
                    else if (worldObject.GetType() == typeof(SidingObject))
                    {
                        Sidings.Add(new TrItemLabel(worldMatrix, (SidingObject)worldObject));
                    }
                    else if (worldObject.GetType() == typeof(PlatformObject))
                    {
                        Platforms.Add(new TrItemLabel(worldMatrix, (PlatformObject)worldObject));
                    }
                    else if (worldObject.GetType() == typeof(StaticObject))
                    {
                        // preTestShape for lookup if it is an animated clock shape with subobjects named as clock hands 
                        StaticShape preTestShape = (new StaticShape(shapeFilePath, worldMatrix, shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None));

                        // FirstOrDefault() checks for "animations( 0 )" as this is a valid entry in *.s files
                        // and is included by MSTSexporter for Blender 2.8+ Release V4.0 or older
                        IEnumerable<AnimationNode> animNodes = preTestShape.SharedShape.Animations?.FirstOrDefault()?.AnimationNodes ?? Enumerable.Empty<AnimationNode>();
                        bool isAnimatedClock = animNodes.Any(node => Regex.IsMatch(node.Name, @"^orts_[hmsc]hand_clock", RegexOptions.IgnoreCase));

                        if (isAnimatedClock)
                        {
                            SceneryObjects.Add(new AnalogClockShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None));
                        }
                        else if (animated)
                            SceneryObjects.Add(new AnimatedShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None));
                        else
                            SceneryObjects.Add(new StaticShape(shapeFilePath, worldMatrix, shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None));
                    }
                    else if (worldObject.GetType() == typeof(PickupObject))
                    {
                        SceneryObjects.Add(new FuelPickupItemShape(shapeFilePath, new FixedWorldPositionSource(worldMatrix), shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None, (PickupObject)worldObject));
                        PickupList.Add((PickupObject)worldObject);
                    }
                    else // It's some other type of object - not one of the above.
                    {
                        SceneryObjects.Add(new StaticShape(shapeFilePath, worldMatrix, shadowCaster ? ShapeFlags.ShadowCaster : ShapeFlags.None));
                    }
                }
                catch (Exception error)
                {
                    Trace.WriteLine(new FileLoadException($"{worldMatrix} scenery object {worldObject.UiD} failed to load", error));
                }
            }

            // Check if there are activity restricted speedposts to be loaded

            if (Viewer.Simulator.ActivityRun != null && Viewer.Simulator.ActivityFile.Activity.ActivityRestrictedSpeedZones != null)
            {
                foreach (TempSpeedPostItem tempSpeedItem in Viewer.Simulator.ActivityRun.TempSpeedPostItems)
                {
                    if (tempSpeedItem.WorldPosition.TileX == tile.X && tempSpeedItem.WorldPosition.TileZ == tile.Z)
                    {
                        if (Viewer.SpeedpostDatFile == null)
                        {
                            Trace.TraceWarning($"{Viewer.Simulator.RouteFolder.CurrentFolder}\\speedpost.dat missing; speed posts for temporary speed restrictions in tile {tile.X} {tile.Z} will not be visible.");
                            break;
                        }
                        else
                        {
                            SceneryObjects.Add(new StaticShape(
                                tempSpeedItem.IsWarning ? Viewer.SpeedpostDatFile.ShapeNames[SpeedPostShapeNames.Warning] :
                                (tempSpeedItem.IsResume ? Viewer.SpeedpostDatFile.ShapeNames[SpeedPostShapeNames.EndRestriction] : Viewer.SpeedpostDatFile.ShapeNames[SpeedPostShapeNames.StartRestriction]),
                                tempSpeedItem.WorldPosition, ShapeFlags.None));
                        }
                    }
                }
            }

            if (Viewer.Settings.ModelInstancing)
            {
                // Instancing collapsed multiple copies of the same model in to a single set of data (the normal model
                // data, plus a list of position information for each copy) and then draws them in a single batch.
                var instances = new Dictionary<string, List<BaseShape>>();
                foreach (var shape in SceneryObjects)
                {
                    // Only allow StaticShape and StaticTrackShape instances for now.
                    if (shape.GetType() != typeof(StaticShape) && shape.GetType() != typeof(StaticTrackShape))
                        continue;

                    // Must have a file path so we can collapse instances on something.
                    var path = shape.SharedShape.FilePath;
                    if (path == null)
                        continue;

                    if (path != null && !instances.ContainsKey(path))
                        instances.Add(path, new List<BaseShape>());

                    if (path != null)
                        instances[path].Add(shape);
                }
                foreach (var path in instances.Keys)
                {
                    if (instances[path].Count >= MinimumInstanceCount)
                    {
                        var sharedInstance = new SharedStaticShapeInstance(path, instances[path]);
                        foreach (var model in instances[path])
                            SceneryObjects.Remove(model);
                        SceneryObjects.Add(sharedInstance);
                    }
                }
            }

            if (viewer.Settings.UseSuperElevation > 0) SuperElevationManager.DecomposeStaticSuperElevation(Viewer, DynamicTrackList, tile.X, tile.Z);
            if (Viewer.World.Sounds != null) Viewer.World.Sounds.AddByTile(tile.X, tile.Z);
        }

        //Method to check a shape name is listed in "openrails\clocks.dat"
        public static ClockType OrClockType(string shape)
        {
            if (null != Simulator.Instance.Clocks)
                foreach (Clock clock in Simulator.Instance.Clocks)
                {
                    if (Path.GetFileName(clock.Name).Equals(shape, StringComparison.OrdinalIgnoreCase))
                        return clock.ClockType;
                }
            return ClockType.Unknown;
        }

        public void Unload()
        {
            foreach (var obj in SceneryObjects)
                obj.Unload();
            if (Viewer.World.Sounds != null) Viewer.World.Sounds.RemoveByTile(tile.X, tile.Z);
        }

        internal void Mark()
        {
            foreach (var shape in SceneryObjects)
                shape.Mark();
            foreach (var dTrack in DynamicTrackList)
                dTrack.Mark();
            foreach (var forest in ForestList)
                forest.Mark();
        }

        public float GetBoundingBoxTop(float x, float z, float blockSize)
        {
            var location = new Vector3(x, float.MinValue, -z);
            foreach (var boundingBox in BoundingBoxes)
            {
                if (boundingBox.Size.X < blockSize / 2 || boundingBox.Size.Z < blockSize / 2)
                    continue;

                var boxLocation = Vector3.Transform(location, boundingBox.Transform);
                if (-boundingBox.Size.X <= boxLocation.X && boxLocation.X <= boundingBox.Size.X && -boundingBox.Size.Z <= boxLocation.Z && boxLocation.Z <= boundingBox.Size.Z)
                    location.Y = Math.Max(location.Y, boundingBox.Height + boundingBox.Size.Y);
            }
            return location.Y;
        }

        public void Update(in ElapsedTime elapsedTime)
        {
            foreach (var spawner in CarSpawners)
                spawner.Update(elapsedTime);
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            foreach (var shape in SceneryObjects)
                shape.PrepareFrame(frame, elapsedTime);
            foreach (var dTrack in DynamicTrackList)
                dTrack.PrepareFrame(frame, elapsedTime);
            foreach (var forest in ForestList)
                forest.PrepareFrame(frame, elapsedTime);
        }

        /// <summary>
        /// Build a w filename from tile X and Z coordinates.
        /// Returns a string eg "w-011283+014482.w"
        /// </summary>
        public static string WorldFileNameFromTileCoordinates(int tileX, int tileZ)
        {
            var filename = "w" + FormatTileCoordinate(tileX) + FormatTileCoordinate(tileZ) + ".w";
            return filename;
        }

        /// <summary>
        /// For building a filename from tile X and Z coordinates.
        /// Returns the string representation of a coordinate
        /// eg "+014482"
        /// </summary>
        private static string FormatTileCoordinate(int tileCoord)
        {
            return $"{(tileCoord < 0 ? "-" : "+")}{Math.Abs(tileCoord):000000}";
        }
    }

    public readonly struct BoundingBox
    {
        public readonly Matrix Transform;
        public readonly Vector3 Size;
        public readonly float Height;

        internal BoundingBox(Matrix transform, Vector3 size, float height)
        {
            Transform = transform;
            Size = size;
            Height = height;
        }
    }
}
