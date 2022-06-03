// COPYRIGHT 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Scripting.Api;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.World
{

    public enum ContainerType
    {
        None,
        C20ft,
        C40ft,
        C40ftHC,
        C45ft,
        C48ft,
        C53ft
    }

    public enum ContainerStatus
    {
        OnEarth,
        Loading,
        Unloading,
        WaitingForLoading,
        WaitingForUnloading
    }

    public class ContainerList
    {
#pragma warning disable CA1002 // Do not expose generic lists
        public List<Container> Containers { get; } = new List<Container>();
#pragma warning restore CA1002 // Do not expose generic lists
    }

    public class Container : IWorldPosition
    {
        private static readonly char[] directorySeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private WorldPosition worldPosition;
        public string Name;
        public string ShapeFileName;
        public string BaseShapeFileFolderSlash;
        public float MassKG = 2000;
        public float WidthM = 2.44f;
        public float LengthM = 12.19f;
        public float HeightM = 2.59f;
        public ContainerType ContainerType = ContainerType.C40ft;
        public bool Flipped = false;
        public static float Length20ftM = 6.095f;
        public static float Length40ftM = 12.19f;

        public ref readonly WorldPosition WorldPosition => ref worldPosition;  // current position of the container
        public float RealRelativeYOffset = 0;
        public float RealRelativeZOffset = 0;
        public Vector3 IntrinsicShapeOffset;
        public ContainerHandlingItem ContainerStation;
        public Matrix RelativeContainerMatrix = Matrix.Identity;
        public MSTSWagon Wagon;
        public string LoadFilePath;


        // generates container from FreightAnim
        /*       public Container(Simulator simulator, string baseShapeFileFolderSlash, FreightAnimationDiscrete freightAnimDiscrete, ContainerHandlingItem containerStation )
               {
                   Simulator = simulator;
                   ShapeFileName = freightAnimDiscrete.ShapeFileName;
                   BaseShapeFileFolderSlash = baseShapeFileFolderSlash;
                   MassKG = freightAnimDiscrete.LoadWeightKG;
                   ContainerType = freightAnimDiscrete.ContainerType;
                   switch (ContainerType)
                   {
                       case ContainerType.C20ft:
                           LengthM = 6.1f;
                           break;
                       case ContainerType.C40ft:
                           LengthM = 12.19f;
                           break;
                       case ContainerType.C40ftHC:
                           LengthM = 12.19f;
                           HeightM = 2.9f;
                           break;
                       case ContainerType.C45ft:
                           LengthM = 13.7f;
                           break;
                       case ContainerType.C48ft:
                           LengthM = 14.6f;
                           break;
                       case ContainerType.C53ft:
                           LengthM = 16.15f;
                           break;
                       default:
                           break;
                   }
                   WorldPosition.XNAMatrix = freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix;
                   WorldPosition.TileX = freightAnimDiscrete.Wagon.WorldPosition.TileX;
                   WorldPosition.TileZ = freightAnimDiscrete.Wagon.WorldPosition.TileZ;
                   var translation = Matrix.CreateTranslation(freightAnimDiscrete.XOffset, freightAnimDiscrete.YOffset, freightAnimDiscrete.ZOffset);
                   WorldPosition.XNAMatrix = translation * WorldPosition.XNAMatrix;
                   IntrinsicShapeOffset = freightAnimDiscrete.IntrinsicShapeOffset;

                   ContainerStation = containerStation;
               }*/

        public Container(FreightAnimationDiscrete freightAnimDiscreteSource, FreightAnimationDiscrete freightAnimDiscrete, bool stacked = false)
        {
            ArgumentNullException.ThrowIfNull(freightAnimDiscreteSource);
            ArgumentNullException.ThrowIfNull(freightAnimDiscrete);

            Wagon = freightAnimDiscrete.Wagon;
            Copy(freightAnimDiscreteSource.Container);

            Vector3 totalOffset = freightAnimDiscrete.Offset - IntrinsicShapeOffset;
            if (stacked)
                totalOffset.Y += freightAnimDiscreteSource.Container.HeightM;
            Matrix translation = Matrix.CreateTranslation(totalOffset);

            worldPosition = new WorldPosition(Wagon.WorldPosition.TileX, Wagon.WorldPosition.TileZ, MatrixExtension.Multiply(translation, Wagon.WorldPosition.XNAMatrix));

            RelativeContainerMatrix = MatrixExtension.Multiply(WorldPosition.XNAMatrix, Matrix.Invert(freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix));
        }

        public Container(MSTSWagon wagon, string loadFilePath, ContainerHandlingItem containerStation = null)
        {
            Wagon = wagon;
            ContainerStation = containerStation;
            LoadFilePath = loadFilePath;
        }

        public void Copy(Container source)
        {
            ArgumentNullException.ThrowIfNull(source);

            Name = source.Name;
            BaseShapeFileFolderSlash = source.BaseShapeFileFolderSlash;
            ShapeFileName = source.ShapeFileName;
            IntrinsicShapeOffset = source.IntrinsicShapeOffset;
            ContainerType = source.ContainerType;
            ComputeDimensions();
            Flipped = source.Flipped;
            MassKG = source.MassKG;
        }

        public Container(BinaryReader inf, FreightAnimationDiscrete freightAnimDiscrete, ContainerHandlingItem containerStation, bool fromContainerStation, int stackLocationIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(containerStation);
            ArgumentNullException.ThrowIfNull(freightAnimDiscrete);

            if (fromContainerStation)
                ContainerStation = containerStation;
            else
                Wagon = freightAnimDiscrete.Wagon;
            Name = inf.ReadString();
            BaseShapeFileFolderSlash = inf.ReadString();
            ShapeFileName = inf.ReadString();
            IntrinsicShapeOffset.X = inf.ReadSingle();
            IntrinsicShapeOffset.Y = inf.ReadSingle();
            IntrinsicShapeOffset.Z = inf.ReadSingle();
            ContainerType = (ContainerType)inf.ReadInt32();
            ComputeDimensions();
            Flipped = inf.ReadBoolean();
            MassKG = inf.ReadSingle();
            if (fromContainerStation)
            {
                // compute WorldPosition starting from offsets and position of container station
                var containersCount = containerStation.StackLocations[stackLocationIndex].Containers.Count;
                var mstsOffset = IntrinsicShapeOffset;
                mstsOffset.Z *= -1;
                var totalOffset = containerStation.StackLocations[stackLocationIndex].Position - mstsOffset;
                totalOffset.Z += LengthM * (containerStation.StackLocations[stackLocationIndex].Flipped ? -1 : 1) / 2;
                if (containersCount != 0)
                    for (var iPos = containersCount - 1; iPos >= 0; iPos--)
                        totalOffset.Y += containerStation.StackLocations[stackLocationIndex].Containers[iPos].HeightM;
                totalOffset.Z *= -1;
                totalOffset = Vector3.Transform(totalOffset, containerStation.WorldPosition.XNAMatrix);
                //               WorldPosition = new WorldLocation(car.WorldPosition.TileX, car.WorldPosition.TileZ,
                //                   totalOffset.X, totalOffset.Y, -totalOffset.Z);
                worldPosition = containerStation.WorldPosition;
                WorldPosition.SetTranslation(totalOffset);
            }
            else
            {
                RelativeContainerMatrix = MatrixExtension.RestoreMatrix(inf);
                worldPosition = new WorldPosition(Wagon.WorldPosition.TileX, Wagon.WorldPosition.TileZ, MatrixExtension.Multiply(RelativeContainerMatrix, Wagon.WorldPosition.XNAMatrix));
            }
        }

        private void ComputeDimensions()
        {
            switch (ContainerType)
            {
                case ContainerType.C20ft:
                    LengthM = 6.095f;
                    break;
                case ContainerType.C40ft:
                    LengthM = 12.19f;
                    break;
                case ContainerType.C40ftHC:
                    LengthM = 12.19f;
                    HeightM = 2.9f;
                    break;
                case ContainerType.C45ft:
                    LengthM = 13.7f;
                    HeightM = 2.9f;
                    break;
                case ContainerType.C48ft:
                    LengthM = 14.6f;
                    HeightM = 2.9f;
                    break;
                case ContainerType.C53ft:
                    LengthM = 16.15f;
                    HeightM = 2.9f;
                    break;
                default:
                    break;
            }
        }

        public void ComputeContainerStationContainerPosition(int stackLocationIndex, int loadPositionVertical)
        {
            // compute WorldPosition starting from offsets and position of container station
            var mstsOffset = IntrinsicShapeOffset;
            mstsOffset.Z *= -1;
            var stackLocation = ContainerStation.StackLocations[stackLocationIndex];
            var totalOffset = stackLocation.Position - mstsOffset;
            totalOffset.Z += LengthM * (stackLocation.Flipped ? -1 : 1) / 2;
            if (stackLocation.Containers != null && stackLocation.Containers.Count != 0)
                for (var iPos = stackLocation.Containers.Count - 1; iPos >= 0; iPos--)
                    totalOffset.Y += stackLocation.Containers[iPos].HeightM;
            totalOffset.Z *= -1;
            totalOffset = Vector3.Transform(totalOffset, ContainerStation.WorldPosition.XNAMatrix);
            //               WorldPosition = new WorldLocation(car.WorldPosition.TileX, car.WorldPosition.TileZ,
            //                   totalOffset.X, totalOffset.Y, -totalOffset.Z);
            worldPosition = ContainerStation.WorldPosition;
            WorldPosition.SetTranslation(totalOffset);
        }

        public void Update()
        {

        }

        public void Save(BinaryWriter outf, bool fromContainerStation = false)
        {
            outf.Write(Name);
            outf.Write(BaseShapeFileFolderSlash);
            outf.Write(ShapeFileName);
            outf.Write(IntrinsicShapeOffset.X);
            outf.Write(IntrinsicShapeOffset.Y);
            outf.Write(IntrinsicShapeOffset.Z);
            outf.Write((int)ContainerType);
            outf.Write(Flipped);
            outf.Write(MassKG);
            if (fromContainerStation)
            {

            }
            else
                MatrixExtension.SaveMatrix(outf, RelativeContainerMatrix);
        }

        public void LoadFromContainerFile(string loadFilePath, string baseFolder)
        {
            ContainerFile containerFile = new ContainerFile(loadFilePath);
            ContainerParameters containerParameters = containerFile.ContainerParameters;
            Name = containerParameters.Name;

            ShapeFileName = @"..\" + containerParameters.ShapeFileName;
            _ = Enum.TryParse(containerParameters.ContainerType, out ContainerType);
            string root = containerParameters.ShapeFileName[..containerParameters.ShapeFileName.IndexOfAny(directorySeparators)];
            BaseShapeFileFolderSlash = Path.Combine(baseFolder, root) + Path.DirectorySeparatorChar;
            ComputeDimensions();
            IntrinsicShapeOffset = containerParameters.IntrinsicShapeOffset;
            IntrinsicShapeOffset.Z *= -1;
        }

        public void SetWorldPosition(in WorldPosition position)
        {
            worldPosition = position;
        }

        public void ComputeWorldPosition(FreightAnimationDiscrete freightAnimDiscrete)
        {
            Vector3 offset = freightAnimDiscrete.Offset;
            //            if (freightAnimDiscrete.Container != null) offset.Y += freightAnimDiscrete.Container.HeightM;
            Matrix translation = Matrix.CreateTranslation(offset - IntrinsicShapeOffset);
            worldPosition = new WorldPosition(freightAnimDiscrete.Wagon.WorldPosition.TileX, freightAnimDiscrete.Wagon.WorldPosition.TileZ,
                MatrixExtension.Multiply(translation, freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix));
            Matrix invWagonMatrix = Matrix.Invert(freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix);
            RelativeContainerMatrix = Matrix.Multiply(WorldPosition.XNAMatrix, invWagonMatrix);
        }
    }

    public class ContainerManager
    {
        private readonly Simulator simulator;

        internal bool FreightAnimNeedsInitialization = true;

        internal LoadStationsOccupancyFile LoadStationsOccupancyFile { get; private set; }
        public Dictionary<int, ContainerHandlingItem> ContainerHandlingItems { get; } = new Dictionary<int, ContainerHandlingItem>();
        public List<Container> Containers { get; } = new List<Container>();
        public Dictionary<string, Container> LoadedContainers { get; } = new Dictionary<string, Container>();

        public ContainerManager(Simulator simulator)
        {
            this.simulator = simulator;
        }

        /*        static Dictionary<int, FuelPickupItem> GetContainerHandlingItemsFromDB(TrackNode[] trackNodes, TrItem[] trItemTable)
                {
                    return (from trackNode in trackNodes
                            where trackNode != null && trackNode.TrVectorNode != null && trackNode.TrVectorNode.NoItemRefs > 0
                            from itemRef in trackNode.TrVectorNode.TrItemRefs.Distinct()
                            where trItemTable[itemRef] != null && trItemTable[itemRef].ItemType == TrItem.trItemType.trPICKUP
                            select new KeyValuePair<int, ContainerHandlingItem>(itemRef, new ContainerHandlingItem(trackNode, trItemTable[itemRef])))
                            .ToDictionary(_ => _.Key, _ => _.Value);
                }*/

        public void LoadOccupancyFromFile(string fileName)
        {
            LoadStationsOccupancyFile = new LoadStationsOccupancyFile(fileName);
        }

        public ContainerHandlingItem CreateContainerStation(WorldPosition shapePosition, int trackItemId, PickupObject pickupObject)
        {
            FuelPickupItem trackItem = simulator.FuelManager.FuelPickupItems[trackItemId];
            return new ContainerHandlingItem(shapePosition, trackItem, pickupObject);
        }

        public void Save(BinaryWriter outf)
        {
            foreach (var containerStation in ContainerHandlingItems.Values)
                containerStation.Save(outf);
        }

        public void Restore(BinaryReader inf)
        {
            foreach (var containerStation in ContainerHandlingItems.Values)
                containerStation.Restore(inf);
        }

        public void Update()
        {
            foreach (var containerStation in ContainerHandlingItems.Values)
                containerStation.Update();
        }


    }

    public class ContainerHandlingItem : FuelPickupItem, IWorldPosition
    {
        private static readonly ContainerManager containerManager = Simulator.Instance.ContainerManager;
        private WorldPosition worldPosition;

        public List<Container> Containers = new List<Container>();
        public ref readonly WorldPosition WorldPosition => ref worldPosition;
        public int MaxStackedContainers;
        public ContainerStackLocation[] StackLocations;
        public float StackLocationsLength = 12.19f;
        public int StackLocationsCount;
        //       public int[] AllocatedContainerIndices;
        public float PickingSurfaceYOffset;
        public Vector3 PickingSurfaceRelativeTopStartPosition;
        public float TargetX;
        public float TargetY;
        public float TargetZ;
        public float TargetGrabber01;
        public float TargetGrabber02;
        public double ActualX { get; set; }
        public double ActualY { get; set; }
        public double ActualZ { get; set; }
        public double ActualGrabber01 { get; set; }
        public double ActualGrabber02 { get; set; }
        public bool MoveX;
        public bool MoveY;
        public bool MoveZ;
        public bool MoveGrabber;
        public float Grabber01Offset;
        private int freePositionVertical;
        private int positionHorizontal;
        public Container HandledContainer;
        public Matrix RelativeContainerPosition;
        public Matrix InitialInvAnimationXNAMatrix = Matrix.Identity;
        public Matrix AnimationXNAMatrix = Matrix.Identity;
        private float GeneralVerticalOffset;
        public float MinZSpan;
        public float Grabber01Max;
        public float Grabber02Max;
        public int GrabberArmsParts;
        private FreightAnimationDiscrete LinkedFreightAnimation;
        public float LoadingEndDelayS { get; protected set; } = 3f;
        public float UnloadingStartDelayS { get; protected set; } = 2f;
        protected Timer DelayTimer;

        private bool messageWritten = false;
        private bool ContainerFlipped = false;
        private bool WagonFlipped = false;
        private int SelectedStackLocationIndex = -1;

        public enum ContainerStationStatus
        {
            Idle,
            LoadRaiseToPick,
            LoadHorizontallyMoveToPick,
            LoadLowerToPick,
            LoadWaitingForPick,
            LoadRaiseToLayOnWagon,
            LoadHorizontallyMoveToLayOnWagon,
            LoadLowerToLayOnWagon,
            LoadWaitingForLayingOnWagon,
            UnloadRaiseToPick,
            UnloadHorizontallyMoveToPick,
            UnloadLowerToPick,
            UnloadWaitingForPick,
            UnloadRaiseToLayOnEarth,
            UnloadHorizontallyMoveToLayOnEarth,
            UnloadLowerToLayOnEarth,
            UnloadWaitingForLayingOnEarth,
            RaiseToIdle,
        }

        public ContainerStationStatus Status = ContainerStationStatus.Idle;
        public bool ContainerAttached;

        public double TimerStartTime { get; set; }

        public ContainerHandlingItem(TrackNode trackNode, TrackItem trItem)
            : base(trackNode, trItem)
        {

        }

        public ContainerHandlingItem(WorldPosition shapePosition, FuelPickupItem item, PickupObject pickupObject) :
            base(item.TrackNode, item.Location)
        {
            worldPosition = shapePosition;
            MaxStackedContainers = pickupObject.MaxStackedContainers;
            StackLocationsLength = pickupObject.StackLocationsLength;
            StackLocationsCount = pickupObject.StackLocations.Locations.Count;
            int stackLocationsCount = StackLocationsCount;
            if (StackLocationsLength + 0.01f > Container.Length40ftM)  // locations can be double if loaded with 20ft containers
                StackLocationsCount *= 2;
            StackLocations = new ContainerStackLocation[StackLocationsCount];
            int i = 0;
            foreach (PickupObject.StackLocation worldStackLocation in pickupObject.StackLocations.Locations)
            {
                ContainerStackLocation stackLocation = new ContainerStackLocation(worldStackLocation);
                StackLocations[i] = stackLocation;
                if (StackLocationsLength + 0.01f > Container.Length40ftM)
                {
                    StackLocations[i + stackLocationsCount] = new ContainerStackLocation(stackLocation);
                    StackLocations[i + stackLocationsCount].Usable = false;
                }
                i++;
            }
            PickingSurfaceYOffset = pickupObject.PickingSurfaceYOffset;
            PickingSurfaceRelativeTopStartPosition = pickupObject.PickingSurfaceRelativeTopStartPosition;
            GrabberArmsParts = pickupObject.GrabberArmsParts;
            DelayTimer = new Timer(this);
            // preload containers if not at restore time
            if (containerManager.LoadStationsOccupancyFile != null)
                PreloadContainerStation(pickupObject);
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write((int)Status);
            outf.Write(GeneralVerticalOffset);
            MatrixExtension.SaveMatrix(outf, RelativeContainerPosition);
            int zero = 0;
            foreach (var stackLocation in StackLocations)
            {
                outf.Write(stackLocation.Usable);
                if (stackLocation.Containers == null || stackLocation.Containers.Count == 0)
                    outf.Write(zero);
                else
                {
                    outf.Write(stackLocation.Containers.Count);
                    foreach (var container in stackLocation.Containers)
                        container.Save(outf, fromContainerStation: true);
                }
            }
        }

        public void Restore(BinaryReader inf)
        {
            var status = (ContainerStationStatus)inf.ReadInt32();
            // in general start with preceding state
            switch (status)
            {
                case ContainerStationStatus.Idle:
                    Status = status;
                    break;
                default:
                    Status = ContainerStationStatus.Idle;
                    break;
            }
            GeneralVerticalOffset = inf.ReadSingle();
            RelativeContainerPosition = MatrixExtension.RestoreMatrix(inf);
            for (int stackLocationIndex = 0; stackLocationIndex < StackLocationsCount; stackLocationIndex++)
            {
                StackLocations[stackLocationIndex].Usable = inf.ReadBoolean();
                var nContainer = inf.ReadInt32();
                if (nContainer > 0)
                {
                    StackLocations[stackLocationIndex].Containers = new List<Container>();
                    for (int i = 0; i < nContainer; i++)
                    {
                        var container = new Container(inf, null, this, true, stackLocationIndex);
                        StackLocations[stackLocationIndex].Containers.Add(container);
                        Containers.Add(container);
                        containerManager.Containers.Add(container);
                    }
                }
            }
        }

        public bool Refill()
        {
            while (MSTSWagon.RefillProcess.OkToRefill)
                return true;
            if (!MSTSWagon.RefillProcess.OkToRefill)
                return false;
            return false;
        }

        public void PreloadContainerStation(PickupObject pickupObject)
        {
            // Search if ContainerStation present in file
            foreach (ContainerStationOccupancy loadStationOccupancy in containerManager.LoadStationsOccupancyFile.LoadStationsOccupancy)
            {
                var tileX = int.Parse(loadStationOccupancy.LoadStationId.WorldFile.Substring(1, 7));
                var tileZ = int.Parse(loadStationOccupancy.LoadStationId.WorldFile.Substring(8, 7));
                if (tileX == Location.TileX && tileZ == Location.TileZ && loadStationOccupancy.LoadStationId.UiD == pickupObject.UiD)
                {
                    string trainSetFolder = Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder;

                    foreach (LoadDataEntry loadDataEntry in loadStationOccupancy.LoadData)
                    {
                        string loadFilePath = Path.Combine(trainSetFolder, loadDataEntry.FolderName, Path.ChangeExtension(loadDataEntry.FileName, ".loa"));
                        if (!File.Exists(loadFilePath))
                        {
                            Trace.TraceWarning($"Ignored missing load {loadFilePath}");
                            continue;
                        }
                        Preload(loadFilePath, loadDataEntry.StackLocation);
                    }
                    break;
                }
            }
        }

        public void Preload(string loadFilePath, int stackLocationIndex)
        {
            Container container;
            container = new Container(null, loadFilePath, this);
            if (containerManager.LoadedContainers.ContainsKey(loadFilePath))
                container.Copy(containerManager.LoadedContainers[loadFilePath]);
            else
            {
                container.LoadFromContainerFile(loadFilePath, Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder);
                containerManager.LoadedContainers.Add(loadFilePath, container);
            }
            ContainerStackLocation stackLocation = StackLocations[stackLocationIndex];
            if (stackLocation.Containers?.Count >= stackLocation.MaxStackedContainers)
                Trace.TraceWarning("Stack Location {0} is full, can't lay down container", stackLocationIndex);
            else if (stackLocation.Containers?[0].LengthM != container.LengthM)
                Trace.TraceWarning("Stack Location {0} is occupied with containers of different length", stackLocationIndex);
            else if (stackLocation.Containers != null && stackLocation.Length + 0.01f < container.LengthM)
                Trace.TraceWarning("Stack Location {0} is too short for container {1}", stackLocationIndex, container.Name);
            stackLocation.Containers ??= new List<Container>();
            container.ComputeContainerStationContainerPosition(stackLocationIndex, stackLocation.Containers.Count);
            stackLocation.Containers.Add(container);
            Containers.Add(container);
            containerManager.Containers.Add(container);
            if (container.ContainerType != ContainerType.C20ft)
                StackLocations[stackLocationIndex + StackLocations.Length / 2].Usable = false;
        }

        public void Update()
        {
            var subMissionTerminated = false;
            if (!MoveX && !MoveY && !MoveZ)
                subMissionTerminated = true;

            switch (Status)
            {
                case ContainerStationStatus.Idle:
                    break;
                case ContainerStationStatus.LoadRaiseToPick:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        Status = ContainerStationStatus.LoadHorizontallyMoveToPick;
                        TargetX = StackLocations[SelectedStackLocationIndex].Position.X;
                        TargetZ = StackLocations[SelectedStackLocationIndex].Position.Z + StackLocations[SelectedStackLocationIndex].Containers[StackLocations[SelectedStackLocationIndex].Containers.Count - 1].LengthM * (StackLocations[SelectedStackLocationIndex].Flipped ? -1 : 1) / 2;
                        //                       TargetX = PickingSurfaceRelativeTopStartPosition.X;
                        //                       TargetZ = PickingSurfaceRelativeTopStartPosition.Z - RelativeContainerPosition.Translation.Z + HandledContainer.IntrinsicShapeZOffset;
                        //                       RelativeContainerPosition.M43 = HandledContainer.IntrinsicShapeZOffset;
                        MoveX = true;
                        MoveZ = true;
                    }
                    break;
                case ContainerStationStatus.LoadHorizontallyMoveToPick:
                    if (subMissionTerminated && !MoveGrabber)
                    {
                        MoveX = false;
                        MoveZ = false;
                        MoveGrabber = false;
                        Status = ContainerStationStatus.LoadLowerToPick;
                        //                       TargetY = HandledContainer.HeightM + HandledContainer.IntrinsicShapeYOffset - PickingSurfaceYOffset;
                        TargetY = ComputeTargetYBase(StackLocations[SelectedStackLocationIndex].Containers.Count - 1, SelectedStackLocationIndex) - PickingSurfaceYOffset;
                        RelativeContainerPosition.M42 = -TargetY + StackLocations[SelectedStackLocationIndex].Containers[StackLocations[SelectedStackLocationIndex].Containers.Count - 1].WorldPosition.XNAMatrix.M42 + InitialInvAnimationXNAMatrix.M42;
                        MoveY = true;
                    }
                    break;
                case ContainerStationStatus.LoadLowerToPick:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        if (DelayTimer == null)
                            DelayTimer = new Timer(this);
                        DelayTimer.Setup(UnloadingStartDelayS);
                        DelayTimer.Start();
                        Status = ContainerStationStatus.LoadWaitingForPick;
                    }
                    break;
                case ContainerStationStatus.LoadWaitingForPick:
                    if (DelayTimer.Triggered)
                    {
                        DelayTimer.Stop();
                        ContainerAttached = true;
                        TargetY = PickingSurfaceRelativeTopStartPosition.Y;
                        MoveY = true;
                        Status = ContainerStationStatus.LoadRaiseToLayOnWagon;
                        messageWritten = false;
                    }
                    break;
                case ContainerStationStatus.LoadRaiseToLayOnWagon:
                    if (subMissionTerminated || messageWritten)
                    {
                        if (Math.Abs(LinkedFreightAnimation.Wagon.SpeedMpS) < 0.01f)
                        {
                            WorldPosition animWorldPosition = LinkedFreightAnimation.Wagon.WorldPosition;
                            Matrix relativeAnimationPosition = Matrix.Multiply(animWorldPosition.XNAMatrix, InitialInvAnimationXNAMatrix);
                            if (!messageWritten)
                            {
                                MoveY = false;
                                TargetX = PickingSurfaceRelativeTopStartPosition.X;
                                // compute where within the free space to lay down the container
                                FreightAnimations freightAnims = LinkedFreightAnimation.FreightAnimations;
                                float offsetZ = LinkedFreightAnimation.Offset.Z;
                                if (Math.Abs(LinkedFreightAnimation.LoadingAreaLength - HandledContainer.LengthM) > 0.01)
                                {
                                    FreightAnimationDiscrete loadedFreightAnim = new FreightAnimationDiscrete(LinkedFreightAnimation, LinkedFreightAnimation.FreightAnimations);
                                    Vector3 offset = loadedFreightAnim.Offset;
                                    IntakePoint loadedIntakePoint = loadedFreightAnim.LinkedIntakePoint;
                                    if (!(HandledContainer.ContainerType == ContainerType.C20ft && LinkedFreightAnimation.LoadPosition == LoadPosition.Center &&
                                        LinkedFreightAnimation.LoadingAreaLength + 0.01f >= 12.19))
                                    {
                                        if (LinkedFreightAnimation.LoadingAreaLength == freightAnims.LoadingAreaLength && !freightAnims.DoubleStacker)
                                        {
                                            loadedFreightAnim.LoadPosition = LoadPosition.Rear;
                                            offset.Z = freightAnims.Offset.Z + (freightAnims.LoadingAreaLength - HandledContainer.LengthM) / 2;
                                            loadedFreightAnim.Offset = offset;
                                        }
                                        else if (loadedFreightAnim.LoadPosition != LoadPosition.Center && loadedFreightAnim.LoadPosition != LoadPosition.Above)
                                        {
                                            switch (loadedFreightAnim.LoadPosition)
                                            {
                                                case LoadPosition.Front:
                                                    offset.Z = freightAnims.Offset.Z - (freightAnims.LoadingAreaLength - HandledContainer.LengthM) / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                case LoadPosition.Rear:
                                                    offset.Z = freightAnims.Offset.Z + (freightAnims.LoadingAreaLength - HandledContainer.LengthM) / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                case LoadPosition.CenterFront:
                                                    offset.Z = freightAnims.Offset.Z - HandledContainer.LengthM / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                case LoadPosition.CenterRear:
                                                    offset.Z = freightAnims.Offset.Z + HandledContainer.LengthM / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    // don't lay down a short container in the middle of the wagon
                                    {
                                        if (LinkedFreightAnimation.LoadingAreaLength == freightAnims.LoadingAreaLength && !freightAnims.DoubleStacker)
                                        {
                                            loadedFreightAnim.LoadPosition = LoadPosition.Rear;
                                            offset.Z = freightAnims.Offset.Z + (freightAnims.LoadingAreaLength - HandledContainer.LengthM) / 2;
                                            loadedFreightAnim.Offset = offset;
                                        }
                                        else
                                        {
                                            loadedFreightAnim.LoadPosition = LoadPosition.CenterFront;
                                            offset.Z = freightAnims.Offset.Z - HandledContainer.LengthM / 2;
                                            loadedFreightAnim.Offset = offset;
                                        }
                                    }
                                    loadedFreightAnim.LoadingAreaLength = HandledContainer.LengthM;
                                    loadedIntakePoint.OffsetM = -loadedFreightAnim.Offset.Z;
                                    freightAnims.Animations.Add(loadedFreightAnim);
                                    loadedFreightAnim.Container = HandledContainer;
                                    freightAnims.UpdateEmptyFreightAnims(HandledContainer.LengthM);
                                    // Too early to have container on wagon
                                    loadedFreightAnim.Container = null;
                                    LinkedFreightAnimation = loadedFreightAnim;
                                }
                                else
                                {
                                    freightAnims.EmptyAnimations.Remove(LinkedFreightAnimation);
                                    freightAnims.Animations.Add(LinkedFreightAnimation);
                                    (freightAnims.Animations.Last() as FreightAnimationDiscrete).Container = HandledContainer;
                                    freightAnims.EmptyAbove();
                                    (freightAnims.Animations.Last() as FreightAnimationDiscrete).Container = null;


                                }
                            }
                            TargetZ = PickingSurfaceRelativeTopStartPosition.Z - relativeAnimationPosition.Translation.Z - LinkedFreightAnimation.Offset.Z *
                                (WagonFlipped ? -1 : 1);
                            if (Math.Abs(TargetZ) > MinZSpan)
                            {
                                if (!messageWritten)
                                {
                                    Simulator.Instance.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Wagon out of range: move wagon towards crane by {0} metres",
                                        Math.Abs(TargetZ) - MinZSpan));
                                    messageWritten = true;
                                }
                            }
                            else
                            {
                                MoveX = MoveZ = true;
                                Status = ContainerStationStatus.LoadHorizontallyMoveToLayOnWagon;
                            }
                        }
                    }
                    break;
                case ContainerStationStatus.LoadHorizontallyMoveToLayOnWagon:
                    if (subMissionTerminated)
                    {
                        MoveX = MoveZ = false;
                        TargetY = HandledContainer.HeightM + LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix.M42
                            + LinkedFreightAnimation.Offset.Y - WorldPosition.XNAMatrix.M42 - PickingSurfaceYOffset;
                        if (LinkedFreightAnimation.LoadPosition == LoadPosition.Above)
                        {
                            var addHeight = 0.0f;
                            foreach (var freightAnim in LinkedFreightAnimation.FreightAnimations.Animations)
                                if (freightAnim is FreightAnimationDiscrete discreteFreightAnim && discreteFreightAnim.LoadPosition != LoadPosition.Above)
                                {
                                    addHeight = discreteFreightAnim.Container.HeightM;
                                    break;
                                }
                            TargetY += addHeight;
                        }
                        MoveY = true;
                        Status = ContainerStationStatus.LoadLowerToLayOnWagon;
                    }
                    break;
                case ContainerStationStatus.LoadLowerToLayOnWagon:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        DelayTimer = new Timer(this);
                        DelayTimer.Setup(LoadingEndDelayS);
                        DelayTimer.Start();
                        Status = ContainerStationStatus.LoadWaitingForLayingOnWagon;
                        var invertedWagonMatrix = Matrix.Invert(LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix);
                        var freightAnim = LinkedFreightAnimation.Wagon.FreightAnimations.Animations.Last() as FreightAnimationDiscrete;
                        freightAnim.Container = HandledContainer;
                        freightAnim.Container.Wagon = LinkedFreightAnimation.Wagon;
                        freightAnim.Container.RelativeContainerMatrix = Matrix.Multiply(LinkedFreightAnimation.Container.WorldPosition.XNAMatrix, invertedWagonMatrix);
                        Containers.Remove(HandledContainer);
                        StackLocations[SelectedStackLocationIndex].Containers.Remove(HandledContainer);
                        if (HandledContainer.ContainerType == ContainerType.C20ft && StackLocations[SelectedStackLocationIndex].Containers.Count == 0 &&
                            StackLocations.Length + 0.01f > Container.Length40ftM)
                            if (SelectedStackLocationIndex < StackLocationsCount / 2 &&
                            (StackLocations[SelectedStackLocationIndex + StackLocationsCount / 2].Containers == null || StackLocations[SelectedStackLocationIndex + StackLocationsCount / 2].Containers.Count == 0))
                                StackLocations[SelectedStackLocationIndex + StackLocationsCount / 2].Usable = false;
                            else if (SelectedStackLocationIndex >= StackLocationsCount / 2 &&
                                (StackLocations[SelectedStackLocationIndex - StackLocationsCount / 2].Containers == null || StackLocations[SelectedStackLocationIndex - StackLocationsCount / 2].Containers.Count == 0))
                                StackLocations[SelectedStackLocationIndex].Usable = false;
                        HandledContainer.Wagon.UpdateLoadPhysics();
                        HandledContainer = null;
                        ContainerAttached = false;
                        freightAnim.Loaded = true;
                    }
                    break;
                case ContainerStationStatus.LoadWaitingForLayingOnWagon:
                    if (DelayTimer.Triggered)
                    {
                        DelayTimer.Stop();
                        TargetY = PickingSurfaceRelativeTopStartPosition.Y;
                        MoveY = true;
                        Status = ContainerStationStatus.RaiseToIdle;
                        messageWritten = false;
                    }
                    break;
                case ContainerStationStatus.UnloadRaiseToPick:
                    if (subMissionTerminated || messageWritten)
                        if (Math.Abs(LinkedFreightAnimation.Wagon.SpeedMpS) < 0.01f)
                        {
                            MoveY = false;
                            HandledContainer = LinkedFreightAnimation.Container;
                            TargetX = PickingSurfaceRelativeTopStartPosition.X;
                            TargetZ = PickingSurfaceRelativeTopStartPosition.Z - RelativeContainerPosition.Translation.Z - HandledContainer.IntrinsicShapeOffset.Z *
                            (ContainerFlipped ? -1 : 1);
                            Status = ContainerStationStatus.UnloadHorizontallyMoveToPick;
                            RelativeContainerPosition.M43 = HandledContainer.IntrinsicShapeOffset.Z * (ContainerFlipped ? 1 : -1);
                            MoveX = true;
                            MoveZ = true;
                            HandledContainer.ContainerStation = this;
                            Containers.Add(HandledContainer);
                        }
                    break;
                case ContainerStationStatus.UnloadHorizontallyMoveToPick:
                    if (subMissionTerminated && !MoveGrabber)
                    {
                        MoveX = false;
                        MoveZ = false;
                        MoveGrabber = false;
                        Status = ContainerStationStatus.UnloadLowerToPick;
                        TargetY = -PickingSurfaceYOffset + HandledContainer.HeightM + HandledContainer.IntrinsicShapeOffset.Y + GeneralVerticalOffset - PickingSurfaceYOffset;
                        RelativeContainerPosition.M42 = PickingSurfaceYOffset - (HandledContainer.HeightM + HandledContainer.IntrinsicShapeOffset.Y);
                        MoveY = true;
                    }
                    break;
                case ContainerStationStatus.UnloadLowerToPick:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        if (DelayTimer == null)
                            DelayTimer = new Timer(this);
                        DelayTimer.Setup(UnloadingStartDelayS);
                        DelayTimer.Start();
                        Status = ContainerStationStatus.UnloadWaitingForPick;
                    }
                    break;
                case ContainerStationStatus.UnloadWaitingForPick:
                    if (DelayTimer.Triggered)
                    {
                        LinkedFreightAnimation.Loaded = false;
                        LinkedFreightAnimation.Container = null;
                        FreightAnimations freightAnims = HandledContainer.Wagon.FreightAnimations;
                        if (LinkedFreightAnimation.LoadPosition == LoadPosition.Above)
                        {
                            LinkedFreightAnimation.Offset = new Vector3(LinkedFreightAnimation.Offset.X, freightAnims.Offset.Y, LinkedFreightAnimation.Offset.Z);
                            LinkedFreightAnimation.AboveLoadingAreaLength = freightAnims.AboveLoadingAreaLength;
                            freightAnims.EmptyAnimations.Add(LinkedFreightAnimation);
                        }
                        else
                        {
                            int discreteAnimCount = 0;
                            if (freightAnims.EmptyAnimations.Count > 0 && freightAnims.EmptyAnimations.Last().LoadPosition == LoadPosition.Above)
                            {
                                HandledContainer.Wagon.IntakePointList.Remove(freightAnims.EmptyAnimations.Last().LinkedIntakePoint);
                                freightAnims.EmptyAnimations.Remove(freightAnims.EmptyAnimations.Last());
                            }
                            foreach (FreightAnimation freightAnim in HandledContainer.Wagon.FreightAnimations.Animations)
                                if (freightAnim is FreightAnimationDiscrete discreteFreightAnim)
                                    if (discreteFreightAnim.LoadPosition != LoadPosition.Above)
                                        discreteAnimCount++;
                            if (discreteAnimCount == 1)
                            {
                                foreach (FreightAnimationDiscrete emptyAnim in freightAnims.EmptyAnimations)
                                    HandledContainer.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                                freightAnims.EmptyAnimations.Clear();
                                freightAnims.EmptyAnimations.Add(new FreightAnimationDiscrete(freightAnims, LoadPosition.Center));
                                HandledContainer.Wagon.IntakePointList.Remove(LinkedFreightAnimation.LinkedIntakePoint);
                            }
                            else
                            {
                                freightAnims.EmptyAnimations.Add(LinkedFreightAnimation);
                                LinkedFreightAnimation.Container = null;
                                LinkedFreightAnimation.Loaded = false;
                                freightAnims.MergeEmptyAnims();
                            }

                        }
                        freightAnims.Animations.Remove(LinkedFreightAnimation);
                        HandledContainer.Wagon.UpdateLoadPhysics();
                        LinkedFreightAnimation = null;
                        DelayTimer.Stop();
                        ContainerAttached = true;
                        TargetY = PickingSurfaceRelativeTopStartPosition.Y;
                        MoveY = true;
                        Status = ContainerStationStatus.UnloadRaiseToLayOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadRaiseToLayOnEarth:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        // Search first free position
                        SelectUnloadPosition();
                        MoveX = MoveZ = true;
                        Status = ContainerStationStatus.UnloadHorizontallyMoveToLayOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadHorizontallyMoveToLayOnEarth:
                    if (subMissionTerminated)
                    {
                        MoveX = MoveZ = false;
                        StackLocations[positionHorizontal].Containers.Add(HandledContainer);
                        TargetY = ComputeTargetYBase(freePositionVertical, positionHorizontal) - PickingSurfaceYOffset;
                        MoveY = true;
                        Status = ContainerStationStatus.UnloadLowerToLayOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadLowerToLayOnEarth:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        DelayTimer.Setup(LoadingEndDelayS);
                        DelayTimer.Start();
                        Status = ContainerStationStatus.UnloadWaitingForLayingOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadWaitingForLayingOnEarth:
                    if (DelayTimer.Triggered)
                    {
                        DelayTimer.Stop();
                        RelativeContainerPosition.M43 = 0;
                        ContainerAttached = false;
                        TargetY = PickingSurfaceRelativeTopStartPosition.Y;
                        MoveY = true;
                        Status = ContainerStationStatus.RaiseToIdle;
                    }
                    break;
                case ContainerStationStatus.RaiseToIdle:
                    if (subMissionTerminated)
                    {
                        MoveY = false;
                        Status = ContainerStationStatus.Idle;
                        MSTSWagon.RefillProcess.OkToRefill = false;
                    }
                    break;
                default:
                    break;
            }
        }

        public void PrepareForUnload(FreightAnimationDiscrete linkedFreightAnimation)
        {
            LinkedFreightAnimation = linkedFreightAnimation;
            RelativeContainerPosition = new Matrix();
            LinkedFreightAnimation.Wagon.WorldPosition.NormalizeTo(WorldPosition.TileX, WorldPosition.TileZ);
            var container = LinkedFreightAnimation.Container;
            RelativeContainerPosition = Matrix.Multiply(container.WorldPosition.XNAMatrix, InitialInvAnimationXNAMatrix);
            RelativeContainerPosition.M42 += PickingSurfaceYOffset;
            RelativeContainerPosition.M41 -= PickingSurfaceRelativeTopStartPosition.X;
            GeneralVerticalOffset = RelativeContainerPosition.M42;
            //            RelativeContainerPosition.Translation += LinkedFreightAnimation.Offset;
            ContainerFlipped = Math.Abs(InitialInvAnimationXNAMatrix.M11 - container.WorldPosition.XNAMatrix.M11) < 0.1f ? false : true;
            Status = ContainerStationStatus.UnloadRaiseToPick;
            TargetY = PickingSurfaceRelativeTopStartPosition.Y;
            MoveY = true;
            SetGrabbers(container);
        }

        public void PrepareForLoad(FreightAnimationDiscrete linkedFreightAnimation)
        {
            //           var invAnimationXNAMatrix = Matrix.Invert(InitialAnimationXNAMatrix);
            //           RelativeContainerPosition = new Matrix();
            //           RelativeContainerPosition = Matrix.Multiply(Containers.Last().WorldPosition.XNAMatrix, invAnimationXNAMatrix);
            LinkedFreightAnimation = linkedFreightAnimation;
            SelectedStackLocationIndex = SelectLoadPosition();
            if (SelectedStackLocationIndex == -1)
                return;
            HandledContainer = StackLocations[SelectedStackLocationIndex].Containers[StackLocations[SelectedStackLocationIndex].Containers.Count - 1];
            RelativeContainerPosition = Matrix.Multiply(HandledContainer.WorldPosition.XNAMatrix, InitialInvAnimationXNAMatrix);
            ContainerFlipped = Math.Abs(InitialInvAnimationXNAMatrix.M11 - HandledContainer.WorldPosition.XNAMatrix.M11) < 0.1f ? false : true;
            WagonFlipped = Math.Abs(InitialInvAnimationXNAMatrix.M11 - LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix.M11) < 0.1f ? false : true;
            RelativeContainerPosition.M41 = HandledContainer.IntrinsicShapeOffset.X * (ContainerFlipped ? 1 : -1);
            RelativeContainerPosition.M42 = HandledContainer.IntrinsicShapeOffset.Y * (ContainerFlipped ? 1 : -1);
            RelativeContainerPosition.M43 = HandledContainer.IntrinsicShapeOffset.Z * (ContainerFlipped ? 1 : -1);
            Status = ContainerStationStatus.LoadRaiseToPick;
            TargetY = PickingSurfaceRelativeTopStartPosition.Y;
            MoveY = true;
            SetGrabbers(HandledContainer);
        }

        public float ComputeTargetYBase(int positionVertical, int positionHorizontal = 0)
        {
            float retVal = StackLocations[positionHorizontal].Position.Y;
            for (var iPos = 0; iPos <= positionVertical; iPos++)
                retVal += StackLocations[positionHorizontal].Containers[iPos].HeightM;
            return retVal;
        }

        /// <summary>
        /// Move container together with container station
        /// </summary>
        /// 
        public void TransferContainer(Matrix animationXNAMatrix)
        {
            AnimationXNAMatrix = animationXNAMatrix;
            if (ContainerAttached)
            {
                // Move together also containers
                HandledContainer.SetWorldPosition(new WorldPosition(HandledContainer.WorldPosition.TileX, HandledContainer.WorldPosition.TileZ, MatrixExtension.Multiply(RelativeContainerPosition, AnimationXNAMatrix)));
            }
        }

        public void ReInitPositionOffset(Matrix animationXNAMatrix)
        {
            InitialInvAnimationXNAMatrix = Matrix.Invert(animationXNAMatrix);
        }

        public void PassSpanParameters(float z1Span, float z2Span, float grabber01Max, float grabber02Max)
        {
            MinZSpan = Math.Min(Math.Abs(z1Span), Math.Abs(z2Span));
            Grabber01Max = grabber01Max;
            Grabber02Max = grabber02Max;

        }

        private void SetGrabbers(Container container)
        {
            TargetGrabber01 = Math.Min(Grabber01Max, (container.LengthM - Container.Length20ftM) / GrabberArmsParts);
            TargetGrabber02 = Math.Max(Grabber02Max, (-container.LengthM + Container.Length20ftM) / GrabberArmsParts);
            MoveGrabber = true;
        }

        private void SelectUnloadPosition()
        {
            int checkLength = HandledContainer.LengthM > Container.Length20ftM + 0.01f && StackLocationsLength + 0.01f >= Container.Length40ftM ? StackLocationsCount / 2 : StackLocationsCount;
            double squaredDistanceToWagon = double.MaxValue;
            int eligibleLocationIndex = -1;
            for (int i = 0; i < checkLength; i++)
            {
                if (!StackLocations[i].Usable)
                    continue;
                if (StackLocations[i].Containers?.Count >= StackLocations[i].MaxStackedContainers)
                    continue;
                if (StackLocations[i].Length + 0.01f < HandledContainer.LengthM)
                    continue;
                if (StackLocations[i].Containers?.Count > 0 && StackLocations[i].Containers[0]?.LengthM != HandledContainer.LengthM)
                    continue;
                double thisDistanceToWagon = (ActualX - StackLocations[i].Position.X) * (ActualX - StackLocations[i].Position.X) +
                    (ActualZ - StackLocations[i].Position.Z) * (ActualZ - StackLocations[i].Position.Z);
                if (thisDistanceToWagon > squaredDistanceToWagon)
                    continue;
                eligibleLocationIndex = i;
                squaredDistanceToWagon = thisDistanceToWagon;
            }
            if (eligibleLocationIndex == -1)
            {
                Simulator.Instance.Confirmer.Message(ConfirmLevel.None, Simulator.Catalog.GetString("No suitable position to unload"));
                // add return on wagon
                return;
            }
            positionHorizontal = eligibleLocationIndex;
            if (StackLocations[eligibleLocationIndex].Containers == null)
                StackLocations[eligibleLocationIndex].Containers = new List<Container>();
            freePositionVertical = StackLocations[eligibleLocationIndex].Containers.Count;
            if (HandledContainer.ContainerType == ContainerType.C20ft && StackLocationsLength + 0.01f >= Container.Length40ftM && eligibleLocationIndex < StackLocationsCount / 2)
                StackLocations[eligibleLocationIndex + StackLocationsCount / 2].Usable = true;
            TargetX = StackLocations[eligibleLocationIndex].Position.X;
            TargetZ = StackLocations[eligibleLocationIndex].Position.Z + HandledContainer.LengthM * (StackLocations[eligibleLocationIndex].Flipped ? -1 : 1) / 2;
        }

        public bool CheckForEligibleStackPosition(Container container)
        {
            int checkLength = container.LengthM > Container.Length20ftM + 0.01f && StackLocationsLength + 0.01f >= Container.Length40ftM ? StackLocationsCount / 2 : StackLocationsCount;
            for (int i = 0; i < checkLength; i++)
            {
                if (!StackLocations[i].Usable)
                    continue;
                if (StackLocations[i].Length + 0.02 < container.LengthM)
                    continue;
                if (StackLocations[i].Containers?.Count >= StackLocations[i].MaxStackedContainers)
                    continue;
                if (StackLocations[i].Containers?.Count > 0 && StackLocations[i].Containers[0]?.LengthM != container.LengthM)
                    continue;
                return true;
            }
            return false;
        }

        private int SelectLoadPosition()
        {
            var squaredDistanceToWagon = float.MaxValue;
            int eligibleLocationIndex = -1;
            var relativeAnimationPosition = Matrix.Multiply(LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix, InitialInvAnimationXNAMatrix);
            var animationZ = PickingSurfaceRelativeTopStartPosition.Z - relativeAnimationPosition.Translation.Z - LinkedFreightAnimation.Offset.Z *
                (WagonFlipped ? -1 : 1);

            for (int i = 0; i < StackLocationsCount; i++)
                if (StackLocations[i].Containers?.Count > 0)
                {
                    if (!LinkedFreightAnimation.FreightAnimations.Validity(LinkedFreightAnimation.Wagon, StackLocations[i].Containers[StackLocations[i].Containers.Count - 1],
                        LinkedFreightAnimation.LoadPosition, LinkedFreightAnimation.Offset, LinkedFreightAnimation.LoadingAreaLength, out Vector3 offset))
                        continue;
                    // FixThis
                    var thisDistanceToWagon = (PickingSurfaceRelativeTopStartPosition.X - StackLocations[i].Position.X) * (PickingSurfaceRelativeTopStartPosition.X - StackLocations[i].Position.X) +
                        (animationZ - StackLocations[i].Position.Z) * (animationZ - StackLocations[i].Position.Z);
                    if (thisDistanceToWagon > squaredDistanceToWagon)
                        continue;
                    eligibleLocationIndex = i;
                    squaredDistanceToWagon = thisDistanceToWagon;
                }
            if (eligibleLocationIndex == -1)
            {
                Simulator.Instance.Confirmer.Message(ConfirmLevel.None, Simulator.Catalog.GetString("No suitable container to load"));
                // add return on wagon
                return eligibleLocationIndex;
            }
            return eligibleLocationIndex;
        }

    } // end Class ContainerHandlingItem

    public class ContainerStackLocation
    {
        // Fixed data
        public Vector3 Position { get; }
        public int MaxStackedContainers { get; }
        public float Length { get; }
        public bool Flipped { get; }

        // Variable data
        public List<Container> Containers { get; internal set; }
        public bool Usable { get; set; } = true;

        public ContainerStackLocation(PickupObject.StackLocation worldStackLocation)
        {
            ArgumentNullException.ThrowIfNull(worldStackLocation);
            Position = worldStackLocation.Position;
            MaxStackedContainers = worldStackLocation.MaxStackedContainers;
            Length = worldStackLocation.Length;
            Flipped = worldStackLocation.Flipped;
        }

        public ContainerStackLocation(ContainerStackLocation source)
        {
            ArgumentNullException.ThrowIfNull(source);
            MaxStackedContainers = source.MaxStackedContainers;
            Length = source.Length + 0.01f >= Container.Length40ftM ? Container.Length20ftM : 0;
            Flipped = source.Flipped;
            Position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z + 6.095f * (Flipped ? -1 : 1));
        }
    }
}

