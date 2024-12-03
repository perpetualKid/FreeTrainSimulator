using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record RouteModelCore : ModelBase<RouteModelCore>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".route";
        }

        private readonly WorldLocation routeStart;

        public override FolderModel Parent => (this as IFileResolve).Container as FolderModel;
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
