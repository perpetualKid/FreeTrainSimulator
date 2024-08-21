using System.Collections.Frozen;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record RouteModelCore: ModelBase<RouteModelCore>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".contentroute";
        }

        private readonly WorldLocation routeStart;

        public string RouteId { get; init; }
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }
        public EnumArray<string, GraphicType> Graphics { get; init; }

        [MemoryPackIgnore]
        public FrozenSet<PathModelCore> TrainPaths { get; private set; }

        [MemoryPackConstructor]
        protected RouteModelCore(in WorldLocation routeStart)
        { 
            this.routeStart = routeStart;
            initializationRequired = true;
        }

        public void InitializeWith(FrozenSet<PathModelCore> paths)
        {
            TrainPaths = paths;
            childsInitialized = true;
        }

        public void InitializeWith(RouteModelCore routeModelCore)
        {
            TrainPaths = routeModelCore.TrainPaths;
            childsInitialized = routeModelCore.childsInitialized;
        }
    }
}
