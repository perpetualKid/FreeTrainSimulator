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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Scripting.Api;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.World
{
    public class Container : IWorldPosition
    {
        public const float Length20ftM = 6.095f;
        public const float Length40ftM = 12.19f;
        public static EnumArray<float, ContainerType> DefaultEmptyMassKG { get; } = new EnumArray<float, ContainerType>(new float[] { 0, 2160, 3900, 4100, 4500, 4700, 4900, 5040 });
        public static EnumArray<float, ContainerType> DefaultMaxMassWhenLoadedKG { get; } = new EnumArray<float, ContainerType>(new float[] { 0, 24000, 30500, 30500, 30500, 30500, 30500, 30500 });

        private static readonly char[] directorySeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        public string LoadFilePath { get; private set; }

        private WorldPosition worldPosition;
        public string Name { get; private set; }
        public string ShapeFileName { get; private set; }
        public string BaseShapeFileFolderSlash { get; private set; }
        public float MassKG { get; private set; } = 2000;
        public float EmptyMassKG { get; private set; }
        public float MaxMassWhenLoadedKG { get; private set; }
        public float WidthM { get; private set; } = 2.44f;
        public float LengthM { get; private set; } = 12.19f;
        public float HeightM { get; private set; } = 2.59f;
        public ContainerType ContainerType { get; private set; } = ContainerType.C40ft;
        private bool flipped;

        public ref readonly WorldPosition WorldPosition => ref worldPosition;  // current position of the container

        public Vector3 IntrinsicShapeOffset { get; private set; }
        public ContainerHandlingItem ContainerStation { get; internal set; }
        public Matrix RelativeContainerMatrix { get; set; } = Matrix.Identity;
        public MSTSWagon Wagon { get; internal set; }

        public bool Visible { get; set; } = true;

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
            flipped = source.flipped;
            MassKG = source.MassKG;
            LoadFilePath = source.LoadFilePath;
            EmptyMassKG = source.EmptyMassKG;
            MaxMassWhenLoadedKG = source.MaxMassWhenLoadedKG;
        }

        public Container(BinaryReader inf, FreightAnimationDiscrete freightAnimDiscrete, ContainerHandlingItem containerStation, bool fromContainerStation, int stackLocationIndex = 0)
        {
            Name = inf.ReadString();
            BaseShapeFileFolderSlash = inf.ReadString();
            ShapeFileName = inf.ReadString();
            LoadFilePath = inf.ReadString();
            IntrinsicShapeOffset = new Vector3(inf.ReadSingle(), inf.ReadSingle(), inf.ReadSingle());
            ContainerType = (ContainerType)inf.ReadInt32();
            ComputeDimensions();
            flipped = inf.ReadBoolean();
            MassKG = inf.ReadSingle();
            EmptyMassKG = inf.ReadSingle();
            MaxMassWhenLoadedKG = inf.ReadSingle();
            if (fromContainerStation)
            {
                ArgumentNullException.ThrowIfNull(containerStation);
                ContainerStation = containerStation;
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
                worldPosition = containerStation.WorldPosition.SetTranslation(totalOffset);
            }
            else
            {
                ArgumentNullException.ThrowIfNull(freightAnimDiscrete);
                Wagon = freightAnimDiscrete.Wagon;
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
                    break;
                case ContainerType.C45ftHC:
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
            Vector3 mstsOffset = IntrinsicShapeOffset;
            mstsOffset.Z *= -1;
            ContainerStackLocation stackLocation = ContainerStation.StackLocations[stackLocationIndex];
            Vector3 totalOffset = stackLocation.Position - mstsOffset;
            totalOffset.Z += LengthM * (stackLocation.Flipped ? -1 : 1) / 2;
            totalOffset.Y += stackLocation.Containers.Sum(c => c.HeightM);
            totalOffset.Z *= -1;
            totalOffset = Vector3.Transform(totalOffset, ContainerStation.WorldPosition.XNAMatrix);
            worldPosition = ContainerStation.WorldPosition.SetTranslation(totalOffset);
        }

        //public void Update()
        //{

        //}

        public void Save(BinaryWriter outf, bool fromContainerStation = false)
        {
            outf.Write(Name);
            outf.Write(BaseShapeFileFolderSlash);
            outf.Write(ShapeFileName);
            outf.Write(LoadFilePath);
            outf.Write(IntrinsicShapeOffset.X);
            outf.Write(IntrinsicShapeOffset.Y);
            outf.Write(IntrinsicShapeOffset.Z);
            outf.Write((int)ContainerType);
            outf.Write(flipped);
            outf.Write(MassKG);
            outf.Write(EmptyMassKG);
            outf.Write(MaxMassWhenLoadedKG);
            if (!fromContainerStation)
                MatrixExtension.SaveMatrix(outf, RelativeContainerMatrix);
        }

        public void LoadFromContainerFile(string loadFilePath, string baseFolder)
        {
            ContainerFile containerFile = new ContainerFile(loadFilePath);
            ContainerParameters containerParameters = containerFile.ContainerParameters;
            Name = containerParameters.Name;

            ShapeFileName = @"..\" + containerParameters.ShapeFileName;
            if (Enum.TryParse(containerParameters.ContainerType, out ContainerType containerType))
                ContainerType = containerType;
            string root = containerParameters.ShapeFileName[..containerParameters.ShapeFileName.IndexOfAny(directorySeparators)];
            BaseShapeFileFolderSlash = Path.Combine(baseFolder, root) + Path.DirectorySeparatorChar;
            ComputeDimensions();
            IntrinsicShapeOffset = containerParameters.IntrinsicShapeOffset.XnaVector();
            EmptyMassKG = containerParameters.EmptyMassKG != -1 ? containerParameters.EmptyMassKG : DefaultEmptyMassKG[ContainerType];
            MaxMassWhenLoadedKG = containerParameters.MaxMassWhenLoadedKG != -1 ? containerParameters.MaxMassWhenLoadedKG : DefaultMaxMassWhenLoadedKG[ContainerType];
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

        public void ComputeLoadWeight(LoadState loadState)
        {
            switch (loadState)
            {
                case LoadState.Empty:
                    MassKG = EmptyMassKG;
                    break;
                case LoadState.Loaded:
                    MassKG = MaxMassWhenLoadedKG;
                    break;
                case LoadState.Random:
                    int loadPercent = StaticRandom.Next(101);
                    MassKG = loadPercent < 30 ? EmptyMassKG : MaxMassWhenLoadedKG * loadPercent / 100f;
                    break;
            }
        }
    }

    public class ContainerManager
    {
        private readonly Simulator simulator;

        internal bool FreightAnimNeedsInitialization = true;

        internal LoadStationsPopulationFile LoadStationsPopulationFile { get; private set; }
        public Dictionary<int, ContainerHandlingItem> ContainerHandlingItems { get; } = new Dictionary<int, ContainerHandlingItem>();
        public Collection<Container> Containers { get; } = new Collection<Container>();
        public Dictionary<string, Container> LoadedContainers { get; } = new Dictionary<string, Container>();
        public static int ActiveOperationsCounter;

        public ContainerManager(Simulator simulator)
        {
            this.simulator = simulator;
        }

        public void LoadPopulationFromFile(string fileName)
        {
            LoadStationsPopulationFile = new LoadStationsPopulationFile(fileName);
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
        private readonly WorldPosition worldPosition;

        public Collection<Container> Containers { get; } = new Collection<Container>();
        public ref readonly WorldPosition WorldPosition => ref worldPosition;
        public int MaxStackedContainers { get; private set; }
        public ContainerStackLocation[] StackLocations { get; private set; }
        private float stackLocationsLength = 12.19f;
        public int StackLocationsCount { get; private set; }
        private float pickingSurfaceYOffset;
        public Vector3 PickingSurfaceRelativeTopStartPosition { get; private set; }
        public EnumArray<float, VectorDirection> Target { get; } = new EnumArray<float, VectorDirection>();
        public float TargetGrabber01 { get; private set; }
        public float TargetGrabber02 { get; private set; }
        public double ActualX { get; set; }
        public double ActualY { get; set; }
        public double ActualZ { get; set; }
        public double ActualGrabber01 { get; set; }
        public double ActualGrabber02 { get; set; }
        public EnumArray<bool, VectorDirection> Movement { get; } = new EnumArray<bool, VectorDirection>();
        public bool MoveGrabber { get; set; }
        private int freePositionVertical;
        private int positionHorizontal;
        private Container handledContainer;
        private Matrix relativeContainerPosition;
        private Matrix initialInvAnimationXNAMatrix = Matrix.Identity;
        private Matrix animationXNAMatrix = Matrix.Identity;
        private float GeneralVerticalOffset;
        public float MinZSpan { get; private set; }
        private float grabber01Max;
        private float grabber02Max;
        private int grabberArmsParts;
        private FreightAnimationDiscrete LinkedFreightAnimation;
        public float LoadingEndDelayS { get; protected set; } = 3f;
        public float UnloadingStartDelayS { get; protected set; } = 2f;

        private Timer DelayTimer;

        private bool messageWritten;
        private bool containerFlipped;
        private bool wagonFlipped;
        private int SelectedStackLocationIndex = -1;

        public ContainerStationStatus Status { get; private set; } = ContainerStationStatus.Idle;
        public bool ContainerAttached { get; private set; }

        public double TimerStartTime { get; set; }

        public ContainerHandlingItem(TrackNode trackNode, TrackItem trItem)
            : base(trackNode, trItem)
        {

        }

        public ContainerHandlingItem(WorldPosition shapePosition, FuelPickupItem item, PickupObject pickupObject) :
            base(item?.TrackNode ?? throw new ArgumentNullException(nameof(item)), item.Location)
        {
            ArgumentNullException.ThrowIfNull(pickupObject);

            worldPosition = shapePosition;
            MaxStackedContainers = pickupObject.MaxStackedContainers;
            stackLocationsLength = pickupObject.StackLocationsLength;
            StackLocationsCount = pickupObject.StackLocations.Locations.Count;
            int stackLocationsCount = StackLocationsCount;
            if (stackLocationsLength + 0.01f > Container.Length40ftM)  // locations can be double if loaded with 20ft containers
                StackLocationsCount *= 2;
            StackLocations = new ContainerStackLocation[StackLocationsCount];
            int i = 0;
            foreach (PickupObject.StackLocation worldStackLocation in pickupObject.StackLocations.Locations)
            {
                ContainerStackLocation stackLocation = new ContainerStackLocation(worldStackLocation);
                StackLocations[i] = stackLocation;
                if (stackLocationsLength + 0.01f > Container.Length40ftM)
                {
                    StackLocations[i + stackLocationsCount] = new ContainerStackLocation(stackLocation)
                    {
                        Usable = false
                    };
                }
                i++;
            }
            pickingSurfaceYOffset = pickupObject.PickingSurfaceYOffset;
            PickingSurfaceRelativeTopStartPosition = pickupObject.PickingSurfaceRelativeTopStartPosition;
            grabberArmsParts = pickupObject.GrabberArmsParts;
            DelayTimer = new Timer(Simulator.Instance);
            // preload containers if not at restore time
            if (containerManager.LoadStationsPopulationFile != null)
                PreloadContainerStation(pickupObject);
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write((int)Status);
            outf.Write(GeneralVerticalOffset);
            MatrixExtension.SaveMatrix(outf, relativeContainerPosition);
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
            relativeContainerPosition = MatrixExtension.RestoreMatrix(inf);
            for (int stackLocationIndex = 0; stackLocationIndex < StackLocationsCount; stackLocationIndex++)
            {
                StackLocations[stackLocationIndex].Usable = inf.ReadBoolean();
                int containerIndex = inf.ReadInt32();
                if (containerIndex > 0)
                {
                    StackLocations[stackLocationIndex].Containers = new ContainerStack();
                    for (int i = 0; i < containerIndex; i++)
                    {
                        Container container = new Container(inf, null, this, true, stackLocationIndex);
                        StackLocations[stackLocationIndex].Containers.Add(container);
                        Containers.Add(container);
                        containerManager.Containers.Add(container);
                    }
                }
            }
        }

        public bool Refill()
        {
            return MSTSWagon.RefillProcess.OkToRefill;
        }

        public void PreloadContainerStation(PickupObject pickupObject)
        {
            ArgumentNullException.ThrowIfNull(pickupObject);

            // Search if ContainerStation present in file
            foreach (ContainerStationPopulation loadStationPopulation in containerManager.LoadStationsPopulationFile.LoadStationsPopulation)
            {
                var tileX = int.Parse(loadStationPopulation.LoadStationId.WorldFile.Substring(1, 7));
                var tileZ = int.Parse(loadStationPopulation.LoadStationId.WorldFile.Substring(8, 7));
                if (tileX == Location.TileX && tileZ == Location.TileZ && loadStationPopulation.LoadStationId.UiD == pickupObject.UiD)
                {
                    string trainSetFolder = Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder;

                    foreach (LoadDataEntry loadDataEntry in loadStationPopulation.LoadData)
                    {
                        string loadFilePath = Path.Combine(trainSetFolder, loadDataEntry.FolderName, Path.ChangeExtension(loadDataEntry.FileName, ".load-or"));
                        if (!File.Exists(loadFilePath))
                        {
                            Trace.TraceWarning($"Ignored missing load {loadFilePath}");
                            continue;
                        }
                        Preload(loadFilePath, loadDataEntry.StackLocation, loadDataEntry.LoadState);
                    }
                    break;
                }
            }
        }

        public void Preload(string loadFilePath, int stackLocationIndex, LoadState loadState)
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
            container.ComputeLoadWeight(loadState);

            ContainerStackLocation stackLocation = StackLocations[stackLocationIndex];
            stackLocation.Containers ??= new ContainerStack();
            if (stackLocation.Containers?.Count >= stackLocation.MaxStackedContainers)
                Trace.TraceWarning("Stack Location {0} is full, can't lay down container", stackLocationIndex);
            else if (stackLocation.Containers.Count > 0 && stackLocation.Containers[0].LengthM != container.LengthM)
                Trace.TraceWarning("Stack Location {0} is occupied with containers of different length", stackLocationIndex);
            else if (stackLocation.Length + 0.01f < container.LengthM)
                Trace.TraceWarning("Stack Location {0} is too short for container {1}", stackLocationIndex, container.Name);
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
            if (!Movement[VectorDirection.X] && !Movement[VectorDirection.Y] && !Movement[VectorDirection.Z])
                subMissionTerminated = true;

            switch (Status)
            {
                case ContainerStationStatus.Idle:
                    break;
                case ContainerStationStatus.LoadRaiseToPick:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        Status = ContainerStationStatus.LoadHorizontallyMoveToPick;
                        Target[VectorDirection.X] = StackLocations[SelectedStackLocationIndex].Position.X;
                        Target[VectorDirection.Z] = StackLocations[SelectedStackLocationIndex].Position.Z + StackLocations[SelectedStackLocationIndex].Containers[StackLocations[SelectedStackLocationIndex].Containers.Count - 1].LengthM * (StackLocations[SelectedStackLocationIndex].Flipped ? -1 : 1) / 2;
                        Movement[VectorDirection.X] = true;
                        Movement[VectorDirection.Z] = true;
                    }
                    break;
                case ContainerStationStatus.LoadHorizontallyMoveToPick:
                    if (subMissionTerminated && !MoveGrabber)
                    {
                        Movement[VectorDirection.X] = false;
                        Movement[VectorDirection.Z] = false;
                        MoveGrabber = false;
                        Status = ContainerStationStatus.LoadLowerToPick;
                        Target[VectorDirection.Y] = ComputeTargetYBase(StackLocations[SelectedStackLocationIndex].Containers.Count - 1, SelectedStackLocationIndex) - pickingSurfaceYOffset;
                        relativeContainerPosition.M42 = -Target[VectorDirection.Y] + StackLocations[SelectedStackLocationIndex].Containers[^1].WorldPosition.XNAMatrix.M42 + initialInvAnimationXNAMatrix.M42;
                        Movement[VectorDirection.Y] = true;
                    }
                    break;
                case ContainerStationStatus.LoadLowerToPick:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        DelayTimer ??= new Timer(Simulator.Instance);
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
                        Target[VectorDirection.Y] = PickingSurfaceRelativeTopStartPosition.Y;
                        Movement[VectorDirection.Y] = true;
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
                            Matrix relativeAnimationPosition = Matrix.Multiply(animWorldPosition.XNAMatrix, initialInvAnimationXNAMatrix);
                            if (!messageWritten)
                            {
                                Movement[VectorDirection.Y] = false;
                                Target[VectorDirection.X] = PickingSurfaceRelativeTopStartPosition.X;
                                // compute where within the free space to lay down the container
                                FreightAnimations freightAnims = LinkedFreightAnimation.FreightAnimations;
                                if (Math.Abs(LinkedFreightAnimation.LoadingAreaLength - handledContainer.LengthM) > 0.01)
                                {
                                    FreightAnimationDiscrete loadedFreightAnim = new FreightAnimationDiscrete(LinkedFreightAnimation, LinkedFreightAnimation.FreightAnimations);
                                    Vector3 offset = loadedFreightAnim.Offset;
                                    IntakePoint loadedIntakePoint = loadedFreightAnim.LinkedIntakePoint;
                                    if (!(handledContainer.ContainerType == ContainerType.C20ft && LinkedFreightAnimation.LoadPosition == LoadPosition.Center &&
                                        LinkedFreightAnimation.LoadingAreaLength + 0.01f >= 12.19))
                                    {
                                        if (LinkedFreightAnimation.LoadingAreaLength == freightAnims.LoadingAreaLength && !freightAnims.DoubleStacker)
                                        {
                                            loadedFreightAnim.LoadPosition = LoadPosition.Rear;
                                            offset.Z = freightAnims.Offset.Z + (freightAnims.LoadingAreaLength - handledContainer.LengthM) / 2;
                                            loadedFreightAnim.Offset = offset;
                                        }
                                        else if (loadedFreightAnim.LoadPosition != LoadPosition.Center && loadedFreightAnim.LoadPosition != LoadPosition.Above)
                                        {
                                            switch (loadedFreightAnim.LoadPosition)
                                            {
                                                case LoadPosition.Front:
                                                    offset.Z = freightAnims.Offset.Z - (freightAnims.LoadingAreaLength - handledContainer.LengthM) / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                case LoadPosition.Rear:
                                                    offset.Z = freightAnims.Offset.Z + (freightAnims.LoadingAreaLength - handledContainer.LengthM) / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                case LoadPosition.CenterFront:
                                                    offset.Z = freightAnims.Offset.Z - handledContainer.LengthM / 2;
                                                    loadedFreightAnim.Offset = offset;
                                                    break;
                                                case LoadPosition.CenterRear:
                                                    offset.Z = freightAnims.Offset.Z + handledContainer.LengthM / 2;
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
                                            offset.Z = freightAnims.Offset.Z + (freightAnims.LoadingAreaLength - handledContainer.LengthM) / 2;
                                            loadedFreightAnim.Offset = offset;
                                        }
                                        else
                                        {
                                            loadedFreightAnim.LoadPosition = LoadPosition.CenterFront;
                                            offset.Z = freightAnims.Offset.Z - handledContainer.LengthM / 2;
                                            loadedFreightAnim.Offset = offset;
                                        }
                                    }
                                    loadedFreightAnim.LoadingAreaLength = handledContainer.LengthM;
                                    loadedIntakePoint.OffsetM = -loadedFreightAnim.Offset.Z;
                                    freightAnims.Animations.Add(loadedFreightAnim);
                                    loadedFreightAnim.Container = handledContainer;
                                    freightAnims.UpdateEmptyFreightAnims(handledContainer.LengthM);
                                    // Too early to have container on wagon
                                    loadedFreightAnim.Container = null;
                                    LinkedFreightAnimation = loadedFreightAnim;
                                }
                                else
                                {
                                    freightAnims.EmptyAnimations.Remove(LinkedFreightAnimation);
                                    freightAnims.Animations.Add(LinkedFreightAnimation);
                                    (freightAnims.Animations.Last() as FreightAnimationDiscrete).Container = handledContainer;
                                    freightAnims.EmptyAbove();
                                    (freightAnims.Animations.Last() as FreightAnimationDiscrete).Container = null;


                                }
                            }
                            Target[VectorDirection.Z] = PickingSurfaceRelativeTopStartPosition.Z - relativeAnimationPosition.Translation.Z - LinkedFreightAnimation.Offset.Z *
                                (wagonFlipped ? -1 : 1);
                            if (Math.Abs(Target[VectorDirection.Z]) > MinZSpan)
                            {
                                if (!messageWritten)
                                {
                                    Simulator.Instance.Confirmer.Message(ConfirmLevel.Information, Simulator.Catalog.GetString("Wagon out of range: move wagon towards crane by {0} metres",
                                        Math.Abs(Target[VectorDirection.Z]) - MinZSpan));
                                    messageWritten = true;
                                }
                            }
                            else
                            {
                                Movement[VectorDirection.X] = Movement[VectorDirection.Z] = true;
                                Status = ContainerStationStatus.LoadHorizontallyMoveToLayOnWagon;
                            }
                        }
                    }
                    break;
                case ContainerStationStatus.LoadHorizontallyMoveToLayOnWagon:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.X] = Movement[VectorDirection.Z] = false;
                        Target[VectorDirection.Y] = handledContainer.HeightM + LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix.M42
                            + LinkedFreightAnimation.Offset.Y - WorldPosition.XNAMatrix.M42 - pickingSurfaceYOffset;
                        if (LinkedFreightAnimation.LoadPosition == LoadPosition.Above)
                        {
                            var addHeight = 0.0f;
                            foreach (var freightAnim in LinkedFreightAnimation.FreightAnimations.Animations)
                                if (freightAnim is FreightAnimationDiscrete discreteFreightAnim && discreteFreightAnim.LoadPosition != LoadPosition.Above)
                                {
                                    addHeight = discreteFreightAnim.Container.HeightM;
                                    break;
                                }
                            Target[VectorDirection.Y] += addHeight;
                        }
                        Movement[VectorDirection.Y] = true;
                        Status = ContainerStationStatus.LoadLowerToLayOnWagon;
                    }
                    break;
                case ContainerStationStatus.LoadLowerToLayOnWagon:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        DelayTimer = new Timer(Simulator.Instance);
                        DelayTimer.Setup(LoadingEndDelayS);
                        DelayTimer.Start();
                        Status = ContainerStationStatus.LoadWaitingForLayingOnWagon;
                        var invertedWagonMatrix = Matrix.Invert(LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix);
                        var freightAnim = LinkedFreightAnimation.Wagon.FreightAnimations.Animations.Last() as FreightAnimationDiscrete;
                        freightAnim.Container = handledContainer;
                        freightAnim.Container.Wagon = LinkedFreightAnimation.Wagon;
                        freightAnim.Container.RelativeContainerMatrix = Matrix.Multiply(LinkedFreightAnimation.Container.WorldPosition.XNAMatrix, invertedWagonMatrix);
                        Containers.Remove(handledContainer);
                        StackLocations[SelectedStackLocationIndex].Containers.Remove(handledContainer);
                        if (handledContainer.ContainerType == ContainerType.C20ft && StackLocations[SelectedStackLocationIndex].Containers.Count == 0 &&
                            StackLocations.Length + 0.01f > Container.Length40ftM)
                            if (SelectedStackLocationIndex < StackLocationsCount / 2 &&
                            (StackLocations[SelectedStackLocationIndex + StackLocationsCount / 2].Containers == null || StackLocations[SelectedStackLocationIndex + StackLocationsCount / 2].Containers.Count == 0))
                                StackLocations[SelectedStackLocationIndex + StackLocationsCount / 2].Usable = false;
                            else if (SelectedStackLocationIndex >= StackLocationsCount / 2 &&
                                (StackLocations[SelectedStackLocationIndex - StackLocationsCount / 2].Containers == null || StackLocations[SelectedStackLocationIndex - StackLocationsCount / 2].Containers.Count == 0))
                                StackLocations[SelectedStackLocationIndex].Usable = false;
                        handledContainer.Wagon.UpdateLoadPhysics();
                        handledContainer = null;
                        ContainerAttached = false;
                        freightAnim.Loaded = true;
                    }
                    break;
                case ContainerStationStatus.LoadWaitingForLayingOnWagon:
                    if (DelayTimer.Triggered)
                    {
                        DelayTimer.Stop();
                        Target[VectorDirection.Y] = PickingSurfaceRelativeTopStartPosition.Y;
                        Movement[VectorDirection.Y] = true;
                        Status = ContainerStationStatus.RaiseToIdle;
                        messageWritten = false;
                    }
                    break;
                case ContainerStationStatus.UnloadRaiseToPick:
                    if (subMissionTerminated || messageWritten)
                        if (Math.Abs(LinkedFreightAnimation.Wagon.SpeedMpS) < 0.01f)
                        {
                            Movement[VectorDirection.Y] = false;
                            handledContainer = LinkedFreightAnimation.Container;
                            Target[VectorDirection.X] = PickingSurfaceRelativeTopStartPosition.X;
                            Target[VectorDirection.Z] = PickingSurfaceRelativeTopStartPosition.Z - relativeContainerPosition.Translation.Z - handledContainer.IntrinsicShapeOffset.Z *
                            (containerFlipped ? -1 : 1);
                            Status = ContainerStationStatus.UnloadHorizontallyMoveToPick;
                            relativeContainerPosition.M43 = handledContainer.IntrinsicShapeOffset.Z * (containerFlipped ? 1 : -1);
                            Movement[VectorDirection.X] = true;
                            Movement[VectorDirection.Z] = true;
                            handledContainer.ContainerStation = this;
                            Containers.Add(handledContainer);
                        }
                    break;
                case ContainerStationStatus.UnloadHorizontallyMoveToPick:
                    if (subMissionTerminated && !MoveGrabber)
                    {
                        Movement[VectorDirection.X] = false;
                        Movement[VectorDirection.Z] = false;
                        MoveGrabber = false;
                        Status = ContainerStationStatus.UnloadLowerToPick;
                        Target[VectorDirection.Y] = -pickingSurfaceYOffset + handledContainer.HeightM + handledContainer.IntrinsicShapeOffset.Y + GeneralVerticalOffset - pickingSurfaceYOffset;
                        relativeContainerPosition.M42 = pickingSurfaceYOffset - (handledContainer.HeightM + handledContainer.IntrinsicShapeOffset.Y);
                        Movement[VectorDirection.Y] = true;
                    }
                    break;
                case ContainerStationStatus.UnloadLowerToPick:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        DelayTimer ??= new Timer(Simulator.Instance);
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
                        FreightAnimations freightAnims = handledContainer.Wagon.FreightAnimations;
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
                                handledContainer.Wagon.IntakePointList.Remove(freightAnims.EmptyAnimations.Last().LinkedIntakePoint);
                                freightAnims.EmptyAnimations.Remove(freightAnims.EmptyAnimations.Last());
                            }
                            foreach (FreightAnimation freightAnim in handledContainer.Wagon.FreightAnimations.Animations)
                                if (freightAnim is FreightAnimationDiscrete discreteFreightAnim)
                                    if (discreteFreightAnim.LoadPosition != LoadPosition.Above)
                                        discreteAnimCount++;
                            if (discreteAnimCount == 1)
                            {
                                foreach (FreightAnimationDiscrete emptyAnim in freightAnims.EmptyAnimations)
                                    handledContainer.Wagon.IntakePointList.Remove(emptyAnim.LinkedIntakePoint);
                                freightAnims.EmptyAnimations.Clear();
                                freightAnims.EmptyAnimations.Add(new FreightAnimationDiscrete(freightAnims, LoadPosition.Center));
                                handledContainer.Wagon.IntakePointList.Remove(LinkedFreightAnimation.LinkedIntakePoint);
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
                        handledContainer.Wagon.UpdateLoadPhysics();
                        LinkedFreightAnimation = null;
                        DelayTimer.Stop();
                        ContainerAttached = true;
                        Target[VectorDirection.Y] = PickingSurfaceRelativeTopStartPosition.Y;
                        Movement[VectorDirection.Y] = true;
                        Status = ContainerStationStatus.UnloadRaiseToLayOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadRaiseToLayOnEarth:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        // Search first free position
                        SelectUnloadPosition();
                        Movement[VectorDirection.X] = Movement[VectorDirection.Z] = true;
                        Status = ContainerStationStatus.UnloadHorizontallyMoveToLayOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadHorizontallyMoveToLayOnEarth:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.X] = Movement[VectorDirection.Z] = false;
                        StackLocations[positionHorizontal].Containers.Add(handledContainer);
                        Target[VectorDirection.Y] = ComputeTargetYBase(freePositionVertical, positionHorizontal) - pickingSurfaceYOffset;
                        Movement[VectorDirection.Y] = true;
                        Status = ContainerStationStatus.UnloadLowerToLayOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadLowerToLayOnEarth:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        DelayTimer.Setup(LoadingEndDelayS);
                        DelayTimer.Start();
                        Status = ContainerStationStatus.UnloadWaitingForLayingOnEarth;
                    }
                    break;
                case ContainerStationStatus.UnloadWaitingForLayingOnEarth:
                    if (DelayTimer.Triggered)
                    {
                        DelayTimer.Stop();
                        relativeContainerPosition.M43 = 0;
                        ContainerAttached = false;
                        Target[VectorDirection.Y] = PickingSurfaceRelativeTopStartPosition.Y;
                        Movement[VectorDirection.Y] = true;
                        Status = ContainerStationStatus.RaiseToIdle;
                    }
                    break;
                case ContainerStationStatus.RaiseToIdle:
                    if (subMissionTerminated)
                    {
                        Movement[VectorDirection.Y] = false;
                        Status = ContainerStationStatus.Idle;
                        MSTSWagon.RefillProcess.OkToRefill = false;
                        ContainerManager.ActiveOperationsCounter--;
                        if (ContainerManager.ActiveOperationsCounter < 0)
                            ContainerManager.ActiveOperationsCounter = 0;
                    }
                    break;
                default:
                    break;
            }
        }

        public void PrepareForUnload(FreightAnimationDiscrete linkedFreightAnimation)
        {
            ContainerManager.ActiveOperationsCounter++;
            LinkedFreightAnimation = linkedFreightAnimation;
            relativeContainerPosition = new Matrix();
            LinkedFreightAnimation.Wagon.UpdateWorldPosition(LinkedFreightAnimation.Wagon.WorldPosition.NormalizeTo(WorldPosition.TileX, WorldPosition.TileZ));
            var container = LinkedFreightAnimation.Container;
            relativeContainerPosition = Matrix.Multiply(container.WorldPosition.XNAMatrix, initialInvAnimationXNAMatrix);
            relativeContainerPosition.M42 += pickingSurfaceYOffset;
            relativeContainerPosition.M41 -= PickingSurfaceRelativeTopStartPosition.X;
            GeneralVerticalOffset = relativeContainerPosition.M42;
            //            RelativeContainerPosition.Translation += LinkedFreightAnimation.Offset;
            containerFlipped = Math.Abs(initialInvAnimationXNAMatrix.M11 - container.WorldPosition.XNAMatrix.M11) >= 0.1f;
            Status = ContainerStationStatus.UnloadRaiseToPick;
            Target[VectorDirection.Y] = PickingSurfaceRelativeTopStartPosition.Y;
            Movement[VectorDirection.Y] = true;
            SetGrabbers(container);
        }

        public void PrepareForLoad(FreightAnimationDiscrete linkedFreightAnimation)
        {
            ContainerManager.ActiveOperationsCounter++;
            LinkedFreightAnimation = linkedFreightAnimation;
            SelectedStackLocationIndex = SelectLoadPosition();
            if (SelectedStackLocationIndex == -1)
                return;
            handledContainer = StackLocations[SelectedStackLocationIndex].Containers[^1];
            relativeContainerPosition = Matrix.Multiply(handledContainer.WorldPosition.XNAMatrix, initialInvAnimationXNAMatrix);
            containerFlipped = Math.Abs(initialInvAnimationXNAMatrix.M11 - handledContainer.WorldPosition.XNAMatrix.M11) >= 0.1f;
            wagonFlipped = Math.Abs(initialInvAnimationXNAMatrix.M11 - LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix.M11) >= 0.1f;
            relativeContainerPosition.M41 = handledContainer.IntrinsicShapeOffset.X * (containerFlipped ? 1 : -1);
            relativeContainerPosition.M42 = handledContainer.IntrinsicShapeOffset.Y * (containerFlipped ? 1 : -1);
            relativeContainerPosition.M43 = handledContainer.IntrinsicShapeOffset.Z * (containerFlipped ? 1 : -1);
            Status = ContainerStationStatus.LoadRaiseToPick;
            Target[VectorDirection.Y] = PickingSurfaceRelativeTopStartPosition.Y;
            Movement[VectorDirection.Y] = true;
            SetGrabbers(handledContainer);
        }

        public float ComputeTargetYBase(int positionVertical, int positionHorizontal = 0)
        {
            float result = StackLocations[positionHorizontal].Position.Y;
            for (int i = 0; i <= positionVertical; i++)
                result += StackLocations[positionHorizontal].Containers[i].HeightM;
            return result;
        }

        /// <summary>
        /// Move container together with container station
        /// </summary>
        /// 
        public void TransferContainer(Matrix animationXNAMatrix)
        {
            this.animationXNAMatrix = animationXNAMatrix;
            if (ContainerAttached)
            {
                // Move together also containers
                handledContainer.SetWorldPosition(new WorldPosition(handledContainer.WorldPosition.TileX, handledContainer.WorldPosition.TileZ, MatrixExtension.Multiply(relativeContainerPosition, this.animationXNAMatrix)));
            }
        }

        public void ReInitPositionOffset(Matrix animationXNAMatrix)
        {
            initialInvAnimationXNAMatrix = Matrix.Invert(animationXNAMatrix);
        }

        public void PassSpanParameters(float z1Span, float z2Span, float grabber01Max, float grabber02Max)
        {
            MinZSpan = Math.Min(Math.Abs(z1Span), Math.Abs(z2Span));
            this.grabber01Max = grabber01Max;
            this.grabber02Max = grabber02Max;

        }

        private void SetGrabbers(Container container)
        {
            TargetGrabber01 = Math.Min(grabber01Max, (container.LengthM - Container.Length20ftM) / grabberArmsParts);
            TargetGrabber02 = Math.Max(grabber02Max, (-container.LengthM + Container.Length20ftM) / grabberArmsParts);
            MoveGrabber = true;
        }

        private void SelectUnloadPosition()
        {
            int checkLength = handledContainer.LengthM > Container.Length20ftM + 0.01f && stackLocationsLength + 0.01f >= Container.Length40ftM ? StackLocationsCount / 2 : StackLocationsCount;
            double squaredDistanceToWagon = double.MaxValue;
            int eligibleLocationIndex = -1;
            for (int i = 0; i < checkLength; i++)
            {
                if (!StackLocations[i].Usable)
                    continue;
                if (StackLocations[i].Containers?.Count >= StackLocations[i].MaxStackedContainers)
                    continue;
                if (StackLocations[i].Length + 0.01f < handledContainer.LengthM)
                    continue;
                if (StackLocations[i].Containers?.Count > 0 && StackLocations[i].Containers[0]?.LengthM != handledContainer.LengthM)
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
                StackLocations[eligibleLocationIndex].Containers = new ContainerStack();
            freePositionVertical = StackLocations[eligibleLocationIndex].Containers.Count;
            if (handledContainer.ContainerType == ContainerType.C20ft && stackLocationsLength + 0.01f >= Container.Length40ftM && eligibleLocationIndex < StackLocationsCount / 2)
                StackLocations[eligibleLocationIndex + StackLocationsCount / 2].Usable = true;
            Target[VectorDirection.X] = StackLocations[eligibleLocationIndex].Position.X;
            Target[VectorDirection.Z] = StackLocations[eligibleLocationIndex].Position.Z + handledContainer.LengthM * (StackLocations[eligibleLocationIndex].Flipped ? -1 : 1) / 2;
        }

        public bool CheckForEligibleStackPosition(Container container)
        {
            ArgumentNullException.ThrowIfNull(container);

            int checkLength = container.LengthM > Container.Length20ftM + 0.01f && stackLocationsLength + 0.01f >= Container.Length40ftM ? StackLocationsCount / 2 : StackLocationsCount;
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
            var relativeAnimationPosition = Matrix.Multiply(LinkedFreightAnimation.Wagon.WorldPosition.XNAMatrix, initialInvAnimationXNAMatrix);
            var animationZ = PickingSurfaceRelativeTopStartPosition.Z - relativeAnimationPosition.Translation.Z - LinkedFreightAnimation.Offset.Z *
                (wagonFlipped ? -1 : 1);

            for (int i = 0; i < StackLocationsCount; i++)
            {
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

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public class ContainerStack : List<Container>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    { }

    public class ContainerStackLocation
    {
        private readonly Vector3 position;
        // Fixed data
        public ref readonly Vector3 Position => ref position;
        public int MaxStackedContainers { get; }
        public float Length { get; }
        public bool Flipped { get; }

        // Variable data
        public ContainerStack Containers { get; internal set; }
        public bool Usable { get; set; } = true;

        public ContainerStackLocation(PickupObject.StackLocation worldStackLocation)
        {
            ArgumentNullException.ThrowIfNull(worldStackLocation);
            position = worldStackLocation.Position;
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
            position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z + 6.095f * (Flipped ? -1 : 1));
        }
    }
}

