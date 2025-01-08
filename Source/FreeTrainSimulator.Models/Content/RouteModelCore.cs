using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".route")]
    public partial record RouteModelCore : ModelBase
    {
        private readonly WorldLocation routeStart;

        public override FolderModel Parent => _parent as FolderModel;
        public string Description { get; init; }
        public ref readonly WorldLocation RouteStart => ref routeStart;
        public bool MetricUnits { get; init; }
        public EnumArray<string, GraphicType> Graphics { get; init; }

        [MemoryPackConstructor]
        protected RouteModelCore(in WorldLocation routeStart)
        {
            this.routeStart = routeStart;
        }
    }
}
