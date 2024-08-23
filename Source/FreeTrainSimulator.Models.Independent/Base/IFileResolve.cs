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
        public abstract string DirectoryPath { get; }
        /// <summary>
        /// Name of the Directory where the <see cref="FileName"/> file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string DirectoryName { get; }
        /// <summary>
        /// Name of the File in the <see cref="DirectoryName"/> folder where the file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FileName { get; }
        /// <summary>
        /// Reference to the parent <seealso cref="ModelBase{T}"/> container instance
        /// </summary>
        public IFileResolve Container { get; }
    }
}
