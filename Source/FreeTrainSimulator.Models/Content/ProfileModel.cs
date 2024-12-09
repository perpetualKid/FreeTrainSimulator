using System;
using System.Collections.Frozen;
using System.Diagnostics;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ProfileModel : ModelBase, IFileResolve
    {
        static string IFileResolve.DefaultExtension => ".profile";

        static string IFileResolve.SubFolder => string.Empty;

        public override ProfileModel Parent => null; // Profile is root and does not implement a parent
        
        [MemoryPackIgnore]
        public static ProfileModel None { get; } = default(ProfileModel);

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

        public override void Initialize(ModelBase parent)
        {
            foreach (FolderModel folder in ContentFolders)
            { 
                folder.Initialize(this);
            }
            base.Initialize(parent);
        }

        public bool Equals(ProfileModel other)
        {
            return other != null && other.Name == Name && other.Version == Version;
        }

        [DebuggerStepThrough]
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Version);
        }
    }
}
