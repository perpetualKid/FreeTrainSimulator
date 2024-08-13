using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable]
    public sealed partial record ContentProfileModel : ModelBase<ContentProfileModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".contentprofile";
        }

        [MemoryPackConstructor]
        public ContentProfileModel(FrozenSet<ContentFolderModel> contentFolders)
        {
            ArgumentNullException.ThrowIfNull(contentFolders, nameof(contentFolders));
            ContentFolders = contentFolders;
        }

        [MemoryPackInclude]
        public FrozenSet<ContentFolderModel> ContentFolders { get; init; } = FrozenSet<ContentFolderModel>.Empty;

        public ContentProfileModel(string name) : base(name, null)
        {
        }

        public override void Initialize(string file, IFileResolve parent)
        {
            foreach (ContentFolderModel folder in ContentFolders)
            { 
                folder.Initialize(null, this);
            }
            base.Initialize(file, parent);
        }

        public bool Equals(ContentProfileModel other)
        {
            return other != null && other.Name == Name && other.Version == Version;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Version);
        }
    }
}
