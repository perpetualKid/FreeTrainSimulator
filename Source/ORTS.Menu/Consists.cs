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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GNU.Gettext;
using Orts.Formats.Msts;

namespace ORTS.Menu
{
    public class Consist
    {
        public string Name { get; private set; }
        public Locomotive Locomotive { get; private set; } = new Locomotive("unknown");
        public string FilePath { get; private set; }

        GettextResourceManager catalog = new GettextResourceManager("ORTS.Menu");

        internal Consist(string filePath, Folder folder)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    ConsistFile conFile = new ConsistFile(filePath);
                    Name = conFile.Name.Trim();
                    Locomotive = GetLocomotive(conFile, folder);
                }
                catch
                {
                    Name = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
                if (Locomotive == null)
                    throw new InvalidDataException("Consist '" + filePath + "' is excluded.");
                if (string.IsNullOrEmpty(Name))
                    Name = "<" + catalog.GetString("unnamed:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            else
            {
                Name = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            FilePath = filePath;
        }

        internal Consist(string filePath, Folder folder, bool reverseConsist)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var conFile = new ConsistFile(filePath);
                    Name = conFile.Name.Trim();
                    Locomotive = reverseConsist ? GetLocomotiveReverse(conFile, folder) : GetLocomotive(conFile, folder);
                }
                catch
                {
                    Name = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
                if (Locomotive == null) throw new InvalidDataException("Consist '" + filePath + "' is excluded.");
                if (string.IsNullOrEmpty(Name)) Name = "<" + catalog.GetString("unnamed:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            else
            {
                Name = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            FilePath = filePath;
        }

        public override string ToString()
        {
            return Name;
        }

        public static Task<List<Consist>> GetConsists(Folder folder, CancellationToken token)
        {
            TaskCompletionSource<List<Consist>> tcs = new TaskCompletionSource<List<Consist>>();
            List<Consist> consists = new List<Consist>();
            string directory = System.IO.Path.Combine(folder.Path, "TRAINS", "CONSISTS");
            if (Directory.Exists(directory))
            {
                foreach (var consist in Directory.GetFiles(directory, "*.con"))
                {
                    if (token.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        break;
                    }
                    try
                    {
                        consists.Add(new Consist(consist, folder));
                    }
                    catch { }
                }
            }
            tcs.TrySetResult(consists);
            return tcs.Task;
        }

        public static Consist GetConsist(Folder folder, string name)
        {
            return GetConsist(folder, name, false);
        }

        public static Consist GetConsist(Folder folder, string name, bool reverseConsist)
        {
            Consist consist = null;
            string directory = System.IO.Path.Combine(folder.Path, "TRAINS", "CONSISTS");
            string file = System.IO.Path.Combine(directory, System.IO.Path.ChangeExtension(name, "con"));

            try
            {
                consist = new Consist(file, folder, reverseConsist);
            }
            catch { }

            return consist;
        }

        static Locomotive GetLocomotive(ConsistFile conFile, Folder folder)
        {
            foreach (var wagon in conFile.Train.TrainCfg.WagonList.Where(w => w.IsEngine))
            {
                var filePath = System.IO.Path.Combine(folder.Path, "TRAINS", "TRAINSET", wagon.Folder, wagon.Name + ".eng");
                try
                {
                    return new Locomotive(filePath);
                }
                catch { }
            }
            return null;
        }

        static Locomotive GetLocomotiveReverse(ConsistFile conFile, Folder folder)
        {
            Locomotive newLocomotive = null;

            foreach (var wagon in conFile.Train.TrainCfg.WagonList.Where(w => w.IsEngine))
            {
                var filePath = System.IO.Path.Combine(folder.Path, "TRAINS", "TRAINSET", wagon.Folder, wagon.Name + ".eng");
                try
                {
                    newLocomotive = new Locomotive(filePath);
                }
                catch { }
            }
            return (newLocomotive);
        }

    }

    public class Locomotive
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string FilePath { get; private set; }

        GettextResourceManager catalog = new GettextResourceManager("ORTS.Menu");

        public Locomotive()
            : this(null)
        {
        }

        internal Locomotive(string filePath)
        {
            if (filePath == null)
            {
                Name = catalog.GetString("- Any Locomotive -");
            }
            else if (File.Exists(filePath))
            {
                bool showInList = true;
                try
                {
                    EngineFile engFile = new EngineFile(filePath);
                    showInList = !string.IsNullOrEmpty(engFile.CabViewFile);
                    Name = engFile.Name.Trim();
                    Description = engFile.Description?.Trim();
                }
                catch
                {
                    Name = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
                if (!showInList)
                    throw new InvalidDataException(catalog.GetStringFmt("Locomotive '{0}' is excluded.", filePath));
                if (string.IsNullOrEmpty(Name))
                    Name = "<" + catalog.GetString("unnamed:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                if (string.IsNullOrEmpty(Description))
                    Description = null;
            }
            else
            {
                Name = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
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
