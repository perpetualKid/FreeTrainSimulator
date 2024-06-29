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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Models;
using Orts.Models.State;
using Orts.Scripting.Api;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.World
{
    public class ContainerHandlingStation : FuelPickupItem, IWorldPosition, ISaveStateApi<ContainerStationSaveState>
    {
        private static readonly ContainerManager containerManager = Simulator.Instance.ContainerManager;
        private readonly WorldPosition worldPosition;

        private static int activeOperations;

        public static bool ActiveOperations => activeOperations > 0;

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

        public ContainerHandlingStation(TrackNode trackNode, TrackItem trItem)
            : base(trackNode, trItem)
        {

        }

        public ContainerHandlingStation(WorldPosition shapePosition, FuelPickupItem item, PickupObject pickupObject) :
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


        public async ValueTask<ContainerStationSaveState> Snapshot()
        {
            ContainerStackItem[] containerStacks = new ContainerStackItem[StackLocations.Length];

            await Parallel.ForAsync(0, StackLocations.Length - 1, async (i, cancellationToken) =>
            {
                containerStacks[i] = new ContainerStackItem(StackLocations[i].Usable, StackLocations[i].Containers.Count);
                await Parallel.ForAsync(0, StackLocations[i].Containers.Count -1, async (j, cancellationToken) =>
                {
                    containerStacks[i].Containers[j] = await StackLocations[i].Containers[j].Snapshot().ConfigureAwait(false);
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return new ContainerStationSaveState()
            {
                ContainerStationStatus = Status,
                ContainerPosition = relativeContainerPosition,
                VerticalOffset = GeneralVerticalOffset,
                ContainerStacks = new Collection<ContainerStackItem>(containerStacks),
            };
        }

        public async ValueTask Restore(ContainerStationSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            Status = saveState.ContainerStationStatus;
            GeneralVerticalOffset = saveState.VerticalOffset;
            relativeContainerPosition = saveState.ContainerPosition;

            using (System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1))
            {
                await Parallel.ForAsync(0, StackLocations.Length - 1, async (i, cancellationToken) =>
                {
                    StackLocations[i].Usable = saveState.ContainerStacks[i].Usable;

                    for (int j = 0; j < StackLocations[i].Containers.Count; j++)
                    {
                        Container container = new Container(this, i);
                        await container.Restore(saveState.ContainerStacks[i].Containers[j]).ConfigureAwait(false);
                        StackLocations[i].Containers.Add(container);
                        try
                        {
                            await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                            Containers.Add(container);
                            containerManager.Containers.Add(container);
                        }
                        finally
                        {
                            semaphoreSlim.Release();
                        }
                    }
                }).ConfigureAwait(false);
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
                Tile tile = new Tile (int.Parse(loadStationPopulation.LoadStationId.WorldFile.Substring(1, 7)), int.Parse(loadStationPopulation.LoadStationId.WorldFile.Substring(8, 7)));
                if (tile == Location.Tile && loadStationPopulation.LoadStationId.UiD == pickupObject.UiD)
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
            if (containerManager.LoadedContainers.TryGetValue(loadFilePath, out Container value))
                container.Copy(value);
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
                        activeOperations--;
                        if (activeOperations < 0)
                            activeOperations = 0;
                    }
                    break;
                default:
                    break;
            }
        }

        public void PrepareForUnload(FreightAnimationDiscrete linkedFreightAnimation)
        {
            activeOperations++;
            LinkedFreightAnimation = linkedFreightAnimation;
            relativeContainerPosition = new Matrix();
            LinkedFreightAnimation.Wagon.UpdateWorldPosition(LinkedFreightAnimation.Wagon.WorldPosition.NormalizeTo(WorldPosition.Tile));
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
            activeOperations++;
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
                handledContainer.SetWorldPosition(new WorldPosition(handledContainer.WorldPosition.Tile, MatrixExtension.Multiply(relativeContainerPosition, this.animationXNAMatrix)));
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
}

