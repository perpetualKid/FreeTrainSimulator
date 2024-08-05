using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader
{
    public static class FileResolver
    {
        private const string RootPath = "Content";

        private static readonly ConcurrentDictionary<string, ContentFolderResolver> contentResolvers = new ConcurrentDictionary<string, ContentFolderResolver>(StringComparer.OrdinalIgnoreCase);

        public static string ContentProfileFile(string contentProfile) => Path.Combine(RuntimeInfo.UserDataFolder, RootPath, contentProfile + ContentProfileModel.FileExtension);
        public static string ContentFolderFile(string contentProfile, string contentFolder) => Path.Combine(ContentProfileDirectory(contentProfile), contentFolder + ContentFolderModel.FileExtension);

        public static string ContentProfileDirectory(string contentProfile) => Path.Combine(RuntimeInfo.UserDataFolder, RootPath, contentProfile);

        public static ContentFolderResolver ContentFolderResolver(ContentFolderModel contentFolder)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));
            if (!contentResolvers.TryGetValue(contentFolder.Name, out ContentFolderResolver resolver))
            {
                resolver = new ContentFolderResolver(contentFolder);
                _ = contentResolvers.TryAdd(contentFolder.Name, resolver);
            }
            return resolver;
        }
    }

    public sealed class ContentFolderResolver
    { 
        public ContentFolderModel ContentFolder { get; }

        public FolderStructure.ContentFolder MstsContentFolder { get; }

        public ContentFolderResolver(ContentFolderModel contentFolderModel) 
        { 
            ContentFolder = contentFolderModel;
            MstsContentFolder = FolderStructure.Content(contentFolderModel.ContentPath);
        }
    }

    //public class RouteFolderResolver : RouteFolderModel
    //{
    //    public RouteFolderResolver(string name) : base()
    //    { 
    //        Name = name;
    //    }
    //}
}
