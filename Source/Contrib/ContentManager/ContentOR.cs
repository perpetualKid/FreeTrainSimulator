// COPYRIGHT 2015 by the Open Rails project.
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
using System.IO;

namespace Orts.ContentManager
{
    public class ContentORTimetableActivity : ContentBase
    {
        public override ContentType Type => ContentType.Activity;

        public ContentORTimetableActivity(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }

        public override ContentBase GetContent(string name, ContentType type)
        {
            if (type == ContentType.Service)
            {
                return new ContentORTimetableService(this, name);
            }
            return base.GetContent(name, type);
        }
    }

    public class ContentORTimetableService : ContentBase
    {
        public override ContentType Type => ContentType.Service;

        public ContentORTimetableService(ContentBase parent, string serviceName)
            : base(parent ?? throw new ArgumentNullException(nameof(parent)))
        {
            Name = serviceName;
            PathName = parent.PathName;
        }
    }
}
