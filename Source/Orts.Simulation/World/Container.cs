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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;
using FreeTrainSimulator.Models.State;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;
using Orts.Formats.OR.Files;
using Orts.Formats.OR.Models;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.World
{
    public class Container : IWorldPosition, ISaveStateApi<ContainerSaveState>
    {
        public const float Length20ftM = 6.095f;
        public const float Length40ftM = 12.19f;

        public static EnumArray<float, ContainerType> DefaultEmptyMassKG { get; } = new EnumArray<float, ContainerType>(new float[] { 0, 2160, 3900, 4100, 4500, 4700, 4900, 5040 });
        public static EnumArray<float, ContainerType> DefaultMaxMassWhenLoadedKG { get; } = new EnumArray<float, ContainerType>(new float[] { 0, 24000, 30500, 30500, 30500, 30500, 30500, 30500 });

        private readonly int stackLocation;

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
        public ContainerHandlingStation ContainerStation { get; internal set; }
        public Matrix RelativeContainerMatrix { get; set; } = Matrix.Identity;
        public MSTSWagon Wagon { get; internal set; }

        public bool Visible { get; set; } = true;

        public Container(ContainerHandlingStation containerStation, int stackLocation)
        { 
            ArgumentNullException.ThrowIfNull(containerStation, nameof(containerStation));
            ContainerStation = containerStation;
            this.stackLocation = stackLocation;
        }

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

            worldPosition = new WorldPosition(Wagon.WorldPosition.Tile, MatrixExtension.Multiply(translation, Wagon.WorldPosition.XNAMatrix));

            RelativeContainerMatrix = MatrixExtension.Multiply(WorldPosition.XNAMatrix, Matrix.Invert(freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix));
        }

        public Container(MSTSWagon wagon, string loadFilePath, ContainerHandlingStation containerStation = null)
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

        public ValueTask<ContainerSaveState> Snapshot()
        {
            return ValueTask.FromResult(new ContainerSaveState()
            {
                Name = Name,
                ShapeFileFolder = BaseShapeFileFolderSlash,
                ShapeFileName = ShapeFileName,
                LoadFilePath = LoadFilePath,
                ShapeOffset = IntrinsicShapeOffset,
                ContainerType = ContainerType,
                Flipped = flipped,
                Mass = MassKG,
                EmptyMass = EmptyMassKG,
                MaxMass = MaxMassWhenLoadedKG,
                RelativeStationPosition = RelativeContainerMatrix
            });
        }

        public ValueTask Restore(ContainerSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            Name = saveState.Name;
            BaseShapeFileFolderSlash = saveState.LoadFilePath;
            ShapeFileName = saveState.ShapeFileName;
            LoadFilePath = saveState.LoadFilePath;
            IntrinsicShapeOffset = saveState.ShapeOffset;
            ContainerType = saveState.ContainerType;
            ComputeDimensions();
            flipped = saveState.Flipped;
            MassKG = saveState.Mass;
            EmptyMassKG = saveState.EmptyMass;
            MaxMassWhenLoadedKG = saveState.MaxMass;
            if (ContainerStation != null)
            {
                // compute WorldPosition starting from offsets and position of container station
                int containersCount = ContainerStation.StackLocations[stackLocation].Containers.Count;
                Vector3 mstsOffset = IntrinsicShapeOffset;
                mstsOffset.Z *= -1;
                Vector3 totalOffset = ContainerStation.StackLocations[stackLocation].Position - mstsOffset;
                totalOffset.Z += LengthM * (ContainerStation.StackLocations[stackLocation].Flipped ? -1 : 1) / 2;
                if (containersCount != 0)
                    for (int i = containersCount - 1; i >= 0; i--)
                        totalOffset.Y += ContainerStation.StackLocations[stackLocation].Containers[i].HeightM;
                totalOffset.Z *= -1;
                totalOffset = Vector3.Transform(totalOffset, ContainerStation.WorldPosition.XNAMatrix);
                worldPosition = ContainerStation.WorldPosition.SetTranslation(totalOffset);
            }
            else
            {
                ArgumentNullException.ThrowIfNull(Wagon);
                RelativeContainerMatrix = saveState.RelativeStationPosition;
                worldPosition = new WorldPosition(Wagon.WorldPosition.Tile, MatrixExtension.Multiply(RelativeContainerMatrix, Wagon.WorldPosition.XNAMatrix));
            }
            return ValueTask.CompletedTask;
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

        internal void ComputeWorldPosition(FreightAnimationDiscrete freightAnimDiscrete)
        {
            Vector3 offset = freightAnimDiscrete.Offset;
            //            if (freightAnimDiscrete.Container != null) offset.Y += freightAnimDiscrete.Container.HeightM;
            Matrix translation = Matrix.CreateTranslation(offset - IntrinsicShapeOffset);
            worldPosition = new WorldPosition(freightAnimDiscrete.Wagon.WorldPosition.Tile,
                MatrixExtension.Multiply(translation, freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix));
            Matrix invWagonMatrix = Matrix.Invert(freightAnimDiscrete.Wagon.WorldPosition.XNAMatrix);
            RelativeContainerMatrix = Matrix.Multiply(WorldPosition.XNAMatrix, invWagonMatrix);
        }

        internal void ComputeLoadWeight(LoadState loadState)
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

