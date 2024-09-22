using System;
using System.Collections.Frozen;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ProfileModel : ModelBase<ProfileModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".profile";
        }

        [MemoryPackConstructor]
        public ProfileModel(string name, FrozenSet<FolderModel> contentFolders): base(name, null)
        {
            ArgumentNullException.ThrowIfNull(contentFolders, nameof(contentFolders));
            ContentFolders = contentFolders;
        }

        public FrozenSet<FolderModel> ContentFolders { get; init; } = FrozenSet<FolderModel>.Empty;

        public ProfileModel(string name) : base(name, null)
        {
        }

        public override void Initialize(string file, IFileResolve parent)
        {
            foreach (FolderModel folder in ContentFolders)
            { 
                folder.Initialize(null, this);
            }
            base.Initialize(file, parent);
        }

        public bool Equals(ProfileModel other)
        {
            return other != null && other.Name == Name && other.Version == Version;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Version);
        }
    }
}
