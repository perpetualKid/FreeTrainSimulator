using System.Collections.Frozen;
using System.Collections.Generic;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record FolderModel : ModelBase<FolderModel>
    {
        public string ContentPath { get; init; }

        [MemoryPackIgnore]
        public FrozenSet<RouteModelCore> Routes { get; private set; }

        static partial void StaticConstructor()
        {
            fileExtension = ".contentfolder";
        }

        [MemoryPackConstructor]
        private FolderModel() : base()
        { }

        public FolderModel(string name, string path, ProfileModel parent) : base(name, parent)
        {
            ContentPath = path;
        }

        public override string ToString()
        {
            return Name;
        }

        public void SetRoutes(IEnumerable<RouteModelCore> routes) => Routes = routes?.ToFrozenSet();

    }
}
