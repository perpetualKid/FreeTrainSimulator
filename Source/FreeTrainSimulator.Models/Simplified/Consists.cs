using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Models.Simplified
{
    public class Consist : ContentBase
    {
        public static Consist Missing { get; } = new Consist($"<{catalog.GetString("missing:")} {Unknown}>", Unknown);

        public string Name { get; private set; }
        public Locomotive Locomotive { get; private set; } = Locomotive.Missing;
        public string FilePath { get; private set; }

        public override string ToString()
        {
            return Name;
        }

        private Consist(ConsistFile consist, Locomotive locomotive, string fileName)
        {
            Locomotive = locomotive;
            if (string.IsNullOrEmpty(consist.Name))
                Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>";
            else
                Name = consist.Name.Trim();
            FilePath = fileName;
        }

        private Consist(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        public static Consist GetConsist(Folder folder, string name, bool reverseConsist)
        {
            string file;
            if (null == folder || !File.Exists(file = folder.ContentFolder.ConsistFile(name)))
                return new Consist($"<{catalog.GetString("missing:")} {name}>", name);

            return FromFile(file, folder, reverseConsist);
        }

        internal static Consist FromFile(string fileName, Folder folder, bool reverseConsist)
        {
            Consist result;

            try
            {
                ConsistFile conFile = new ConsistFile(fileName);
                Locomotive locomotive = GetLocomotive(conFile, folder, reverseConsist);
                result = new Consist(conFile, locomotive, fileName);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                result = new Consist($"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
            }
            return result;
        }

        public static async Task<IEnumerable<Consist>> GetConsists(Folder folder, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(folder);

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<Consist> result = new List<Consist>();
                string consistsDirectory = folder.ContentFolder.ConsistsFolder;

                if (Directory.Exists(consistsDirectory))
                {
                    TransformBlock<string, Consist> inputBlock = new TransformBlock<string, Consist>
                        (consistFile =>
                        {
                            return FromFile(consistFile, folder, false);
                        },
                        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount, CancellationToken = token });


                    ActionBlock<Consist> actionBlock = new ActionBlock<Consist>
                        (async consist =>
                        {
                            if (null == consist.Locomotive)
                                return;
                            try
                            {
                                await addItem.WaitAsync(token).ConfigureAwait(false);
                                result.Add(consist);
                            }
                            finally
                            {
                                addItem.Release();
                            }
                        });

                    inputBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                    foreach (string consistFile in Directory.EnumerateFiles(consistsDirectory, "*.con"))
                        await inputBlock.SendAsync(consistFile).ConfigureAwait(false);

                    inputBlock.Complete();
                    await actionBlock.Completion.ConfigureAwait(false);
                }
                return result;
            }
        }

        private static Locomotive GetLocomotive(ConsistFile conFile, Folder folder, bool reverse)
        {
            Wagon wagon = reverse ? conFile.Train.Wagons.Where(w => w.IsEngine).LastOrDefault() : conFile.Train.Wagons.Where(w => w.IsEngine).FirstOrDefault();
            if (null != wagon)
                return Locomotive.GetLocomotive(folder.ContentFolder.EngineFile(wagon.Folder, wagon.Name));
            return null;
        }
    }
}
