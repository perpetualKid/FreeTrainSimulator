namespace FreeTrainSimulator.Models.Independent.Base
{
    /// <summary>
    /// Interface implemented by all <seealso cref="ModelBase{T}"/> instances to support additional file system lookup scenarios
    /// </summary>
    public interface IFileResolve
    {
        /// <summary>
        /// Default File Extension
        /// </summary>
        public static abstract string DefaultExtension { get; }
        /// <summary>
        /// Full Directory Path where the file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FilePath { get; }
        /// <summary>
        /// Name of the Folder where the <see cref="FileName"/> file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FolderName { get; }
        /// <summary>
        /// Name of the File in the <see cref="FolderName"/> folder where the file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FileName { get; }
        /// <summary>
        /// Reference to the parent <seealso cref="ModelBase{T}"/> instance
        /// </summary>
        public IFileResolve Parent { get; }
    }
}
