﻿using System.Collections.Frozen;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record WagonSetModel : ModelBase<WagonSetModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".wagonset";
            subFolder = ".TrainSets";
        }

        public override FolderModel Parent => _parent as FolderModel;

        //Speed and an acceleration factor.
        //First number is the actual max speed (in meters per second) based on TE/tonnage;
        //Second number is some kind of multiplier that determines what speed the AI train will slow to on grades and curves.
        public float MaximumSpeed { get; init; }
        public float AccelerationFactor { get; init; }
        public float Durability { get; init; }

        public FrozenSet<WagonReferenceModel> TrainCars { get; init; } = FrozenSet<WagonReferenceModel>.Empty;
        public WagonReferenceModel Locomotive { get; init; }

        public override void Initialize(string file, IFileResolve parent)
        {
            foreach (WagonReferenceModel wagonReference in TrainCars)
            {
                wagonReference.Initialize(null, this);
            }
            base.Initialize(file, parent);
        }
    }
}