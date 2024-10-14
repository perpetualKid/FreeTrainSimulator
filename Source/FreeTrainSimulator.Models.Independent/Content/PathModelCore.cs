using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record PathModelCore : ModelBase<PathModelCore>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".path";
        }

        public override RouteModelCore Parent => _parent as RouteModelCore;
        /// <summary>Start location of the path</summary>
        public string Start { get; init; }
        /// <summary>Destination location of the path</summary>
        public string End { get; init; }
        /// <summary>Is the path a player path or not</summary>
        public bool PlayerPath { get; init; }


    }
}
