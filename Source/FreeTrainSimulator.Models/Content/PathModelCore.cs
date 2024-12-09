using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record PathModelCore : ModelBase, IFileResolve
    {
#pragma warning disable CA1033 // Interface methods should be callable by child types
        static string IFileResolve.SubFolder => "TrainPaths";
        static string IFileResolve.DefaultExtension => ".path";
#pragma warning restore CA1033 // Interface methods should be callable by child types

        public override RouteModelCore Parent => _parent as RouteModelCore;
        /// <summary>Start location of the path</summary>
        public string Start { get; init; }
        /// <summary>Destination location of the path</summary>
        public string End { get; init; }
        /// <summary>Is the path a player path or not</summary>
        public bool PlayerPath { get; init; }


    }
}
