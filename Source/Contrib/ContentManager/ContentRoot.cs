// COPYRIGHT 2014, 2015 by the Open Rails project.
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

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;

namespace Orts.ContentManager
{
    public class ContentRoot : ContentBase
    {
        public override ContentType Type => ContentType.Root;

        public ContentRoot()
            : base(null)
        {
            Name = "Content Manager";
            PathName = "";
        }

        public override IEnumerable<ContentBase> GetContent(ContentType type)
        {            
            if (type == ContentType.Package)
            {
                // TODO: Support OR content folders.
                foreach (FolderModel folder in ContentModel.None.ImportFolderSettings())
                {
                    if (ContentMSTSPackage.IsValid(folder.ContentPath))
                        yield return new ContentMSTSPackage(this, folder.Name, folder.ContentPath);
                    else
                        yield return new ContentMSTS(this, folder.Name, folder.ContentPath);
                }
            }
        }
    }
}
