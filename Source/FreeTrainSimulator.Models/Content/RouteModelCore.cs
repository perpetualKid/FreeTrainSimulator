using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record RouteModelCore : ModelBase, IFileResolve
    {
#pragma warning disable CA1033 // Interface methods should be callable by child types
        static string IFileResolve.SubFolder => string.Empty;
        static string IFileResolve.DefaultExtension => ".route";
#pragma warning restore CA1033 // Interface methods should be callable by child types

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
