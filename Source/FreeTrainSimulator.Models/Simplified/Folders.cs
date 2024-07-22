using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Simplified
{
    public class Folder : ContentBase
    {
        public string Name { get; private set; }
        public string Path { get; private set; }

        internal FolderStructure.ContentFolder ContentFolder { get; private set; }

        public Folder(string name, string path)
        {
            Name = name;
            Path = path;
            ContentFolder = FolderStructure.Content(path);
        }

        public override string ToString()
        {
            return Name;
        }

        public static async Task<IEnumerable<Folder>> GetFolders(Dictionary<string, string> folders)
        {
            ArgumentNullException.ThrowIfNull(folders);
            ConcurrentBag<Folder> results = new ConcurrentBag<Folder>();

            await Parallel.ForEachAsync(folders, (folderSource, cancellationToken) =>
            {
                Folder folder = new Folder(folderSource.Key, folderSource.Value);
                results.Add(folder);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }
    }
}
