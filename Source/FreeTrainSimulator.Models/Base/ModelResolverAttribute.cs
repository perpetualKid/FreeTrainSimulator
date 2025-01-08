using System;

namespace FreeTrainSimulator.Models.Base
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModelResolverAttribute : Attribute
    {
        public static ModelResolverAttribute Empty { get; } = new ModelResolverAttribute(string.Empty, string.Empty);

        public string Folder { get; }
        public string FileExtension { get; }
        public ModelResolverAttribute(string folder, string fileExtension)
        {
            Folder = folder;
            FileExtension = fileExtension;
        }
    }
}
