using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent.Environment;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader
{
    public static class FileResolver
    {
        private const string RootPath = "Content";

        private static readonly ConcurrentDictionary<string, ContentFolderResolver> contentFolders = new ConcurrentDictionary<string, ContentFolderResolver>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, ContentProfileModel> contentProfiles = new ConcurrentDictionary<string, ContentProfileModel>(StringComparer.OrdinalIgnoreCase);

        public static string ContentProfileFile(string contentProfile) => Path.Combine(RuntimeInfo.UserDataFolder, RootPath, contentProfile + ContentProfileModel.FileExtension);

        public static string ContentProfileDirectory(string contentProfile) => Path.Combine(RuntimeInfo.UserDataFolder, RootPath, contentProfile);

        public static ContentFolderResolver ContentFolderResolver(ContentFolderModel contentFolder)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));
            if (!contentFolders.TryGetValue(contentFolder.Name, out ContentFolderResolver resolver))
            {
                resolver = new ContentFolderResolver(contentFolder);
                _ = contentFolders.TryAdd(contentFolder.Name, resolver);
            }
            return resolver;
        }

        public static async ValueTask<ContentProfileModel> ContentProfile(string name)
        {
            if (!contentProfiles.TryGetValue(name, out ContentProfileModel resolver))
            {
                resolver = await ContentProfileLoader.Load(name, CancellationToken.None).ConfigureAwait(false);
                _ = contentProfiles.TryAdd(name, resolver);
            }
            return resolver;
        }
    }

    public sealed class ContentFolderResolver
    { 
        public ContentFolderModel ContentFolder { get; }

        public ContentFolderResolver(ContentFolderModel contentFolderModel) 
        { 
            ContentFolder = contentFolderModel;
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
