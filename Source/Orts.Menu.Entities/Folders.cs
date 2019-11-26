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

using Orts.Formats.Msts;
using Orts.Settings;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Orts.Menu.Entities
{
    public class Folder: ContentBase
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

        public static async Task<IEnumerable<Folder>> GetFolders(UserSettings settings)
        {
            return await Task.Run(() =>
            {
                return settings.Folders.Folders.Select((folder) => new Folder(folder.Key, folder.Value));
            }).ConfigureAwait(false);
        }

        public static void SetFolders(UserSettings settings, List<Folder> folders)
        {
            settings.Folders.Folders.Clear();
            foreach (var folder in folders)
                settings.Folders.Folders[folder.Name] = folder.Path;
            settings.Folders.Save();
        }
    }
}
