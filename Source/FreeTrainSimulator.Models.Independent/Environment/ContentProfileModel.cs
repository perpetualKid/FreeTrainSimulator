using System;
using System.Collections;
using System.Collections.Generic;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Environment
{
    [MemoryPackable]
    public sealed partial class ContentProfileModel : ModelBase<ContentProfileModel>, ICollection<ContentFolderModel>, IEnumerable<ContentFolderModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".contentprofile";
        }

        public ContentProfileModel() { }

        [MemoryPackConstructor]
        public ContentProfileModel(List<ContentFolderModel> contentFolders)
        {
            ArgumentNullException.ThrowIfNull(contentFolders, nameof(contentFolders));
            this.contentFolders = contentFolders;
        }

        public static ContentProfileModel Default { get; } = new ContentProfileModel("default");

        [MemoryPackInclude]
        private readonly List<ContentFolderModel> contentFolders = new List<ContentFolderModel>();

        public ContentProfileModel(string name) : base(name)
        {
        }

        #region ICollection<ContentFolderModel> implementation
        public int Count => contentFolders.Count;

        public bool IsReadOnly => false;

        public IEnumerator<ContentFolderModel> GetEnumerator()
        {
            return contentFolders.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(ContentFolderModel item)
        {
            contentFolders.Add(item);
        }

        public void Clear()
        {
            contentFolders.Clear();
        }

        public bool Contains(ContentFolderModel item)
        {
            return contentFolders.Contains(item);
        }

        public void CopyTo(ContentFolderModel[] array, int arrayIndex)
        {
            contentFolders.CopyTo(array, arrayIndex);
        }

        public bool Remove(ContentFolderModel item)
        {
            return contentFolders.Remove(item);
        }
        #endregion
    }
}
