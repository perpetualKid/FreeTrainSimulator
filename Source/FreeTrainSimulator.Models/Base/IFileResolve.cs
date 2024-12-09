namespace FreeTrainSimulator.Models.Base
{
    public interface IFileResolve
    {
        /// <summary>
        /// Default File Extension
        /// </summary>
        public static abstract string DefaultExtension { get; }
        /// <summary>
        /// Common subfolder for this file type
        /// </summary>
        public static abstract string SubFolder { get; }
    }
}
