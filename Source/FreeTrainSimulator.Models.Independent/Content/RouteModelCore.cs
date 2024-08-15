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

        [MemoryPackConstructor]
        protected RouteModelCore(in WorldLocation routeStart)
        { 
            this.routeStart = routeStart;
        }

        public sealed override string ToString()
        {
            return Name;
        }
    }
}
