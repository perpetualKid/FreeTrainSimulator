// COPYRIGHT 2012, 2013 by the Open Rails project.
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace Orts.Menu.Entities
{
    public class Consist: ContentBase
    {
        public string Name { get; private set; }
        public Locomotive Locomotive { get; private set; } = Locomotive.GetLocomotive("unknown");
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
                Name = consist.Name?.Trim();
            FilePath = fileName;
        }

        private Consist(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        public static Consist GetConsist(Folder folder, string name, bool reverseConsist = false)
        {
            string directory = System.IO.Path.Combine(folder.Path, "TRAINS", "CONSISTS");
            string file = System.IO.Path.Combine(directory, System.IO.Path.ChangeExtension(name, "con"));

            return GetConsist(file, folder, reverseConsist);
        }

        public static Consist GetConsist(string fileName, Folder folder, bool reverseConsist = false)
        {
            Consist result = null;

            if (File.Exists(fileName))
            {
                try
                {
                    ConsistFile conFile = new ConsistFile(fileName);
                    Locomotive locomotive = reverseConsist ? GetLocomotiveReverse(conFile, folder) : GetLocomotive(conFile, folder);
                    if (locomotive != null)
                    {
                        result = new Consist(conFile, locomotive, fileName);
                    }
                }
                catch
                {
                    result = new Consist($"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
                }
            }
            else
            {
                result = new Consist($"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
            }
            return result;
        }

        public static Task<List<Consist>> GetConsists(Folder folder, CancellationToken token)
        {
            SemaphoreSlim addItem = new SemaphoreSlim(1);
            List<Consist> consists = new List<Consist>();
            string directory = System.IO.Path.Combine(folder.Path, "TRAINS", "CONSISTS");
            if (Directory.Exists(directory))
            {
                try
                {
                    Parallel.ForEach(Directory.GetFiles(directory, "*.con"),
                        new ParallelOptions() { CancellationToken = token },
                        (consistFile, state) =>
                    {
                        try
                        {
                            Consist consist = GetConsist(consistFile, folder, false);
                            if (null != consist)
                            {
                                addItem.Wait(token);
                                consists.Add(consist);
                            }
                        }
                        catch { }
                        finally { addItem.Release(); }
                    });
                }
                catch (OperationCanceledException) { }
                if (token.IsCancellationRequested)
                    return Task.FromCanceled<List<Consist>>(token);
            }
            return Task.FromResult(consists);
        }

        private static Locomotive GetLocomotive(ConsistFile conFile, Folder folder)
        {
            foreach (var wagon in conFile.Train.TrainConfig.WagonList.Where(w => w.IsEngine))
            {
                try
                {
                    return Locomotive.GetLocomotive(System.IO.Path.Combine(folder.Path, "TRAINS", "TRAINSET", wagon.Folder, wagon.Name + ".eng"));
                }
                catch { }
            }
            return null;
        }

        private static Locomotive GetLocomotiveReverse(ConsistFile conFile, Folder folder)
        {
            foreach (var wagon in conFile.Train.TrainConfig.WagonList.Where(w => w.IsEngine))
            {
                try
                {
                    return Locomotive.GetLocomotive(System.IO.Path.Combine(folder.Path, "TRAINS", "TRAINSET", wagon.Folder, wagon.Name + ".eng"));
                }
                catch { }
            }
            return null;
        }

    }

    public class Locomotive: ContentBase
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string FilePath { get; private set; }

        public static Locomotive GetLocomotive(string fileName)
        {
            Locomotive result = null;
            if (string.IsNullOrEmpty(fileName))
            {
                result = new Locomotive(catalog.GetString("- Any Locomotive -"), fileName);
            }
            else if (File.Exists(fileName))
            {
                try
                {
                    EngineFile engFile = new EngineFile(fileName);
                    if (!string.IsNullOrEmpty(engFile.CabViewFile))
                        result = new Locomotive(engFile, fileName);
                }
                catch
                {
                    result = new Locomotive($"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
                }
            }
            else
            {
                result = new Locomotive($"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
            }
            return result;
        }

        private Locomotive(EngineFile engine, string fileName)
        {
            Name = engine.Name?.Trim();
            Description = engine.Description?.Trim();
            if (string.IsNullOrEmpty(Name))
                Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>";
            if (string.IsNullOrEmpty(Description))
                Description = null;
            FilePath = fileName;
        }

        private Locomotive(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            return obj is Locomotive && ((obj as Locomotive).Name == Name || (obj as Locomotive).FilePath == null || FilePath == null);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
