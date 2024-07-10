using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

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

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {

                List<Folder> result = new List<Folder>();
                //https://stackoverflow.com/questions/11564506/nesting-await-in-parallel-foreach?rq=1
                ActionBlock<KeyValuePair<string, string>> actionBlock = new ActionBlock<KeyValuePair<string, string>>
                    (async folderName =>
                    {
                        try
                        {
                            Folder folder = new Folder(folderName.Key, folderName.Value);
                            await addItem.WaitAsync().ConfigureAwait(false);
                            result.Add(folder);
                        }
                        finally
                        {
                            addItem.Release();
                        }
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

                foreach (KeyValuePair<string, string> folder in folders)
                    await actionBlock.SendAsync(folder).ConfigureAwait(false);

                actionBlock.Complete();
                await actionBlock.Completion.ConfigureAwait(false);

                return result;
            }
        }
    }
}
