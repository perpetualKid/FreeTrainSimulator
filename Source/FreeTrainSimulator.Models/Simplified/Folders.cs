// COPYRIGHT 2011, 2012, 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

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
