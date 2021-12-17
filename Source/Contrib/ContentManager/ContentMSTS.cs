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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Orts.ContentManager
{
    public class ContentMSTS : ContentBase
    {
        public override ContentType Type => ContentType.Collection;

        public ContentMSTS(ContentBase parent, string name, string path)
            : base(parent)
        {
            Name = name;
            PathName = path;
        }

        public override IEnumerable<ContentBase> GetContent(ContentType type)
        {
            if (type == ContentType.Package)
            {
                if (Directory.Exists(PathName))
                {
                    foreach (string item in Directory.GetDirectories(PathName))
                    {
                        if (ContentMSTSPackage.IsValid(item))
                            yield return new ContentMSTSPackage(this, Path.GetFileName(item), item);
                    }
                }
            }
        }
    }

    public class ContentMSTSPackage : ContentBase
    {
        public static bool IsValid(string pathName)
        {
            return Directory.Exists(Path.Combine(pathName, "ROUTES")) || Directory.Exists(Path.Combine(pathName, "TRAINS"));
        }

        public override ContentType Type => ContentType.Package;

        public ContentMSTSPackage(ContentBase parent, string name, string path)
            : base(parent)
        {
            Name = name;
            PathName = path;
        }

        public override IEnumerable<ContentBase> GetContent(ContentType type)
        {
            if (type == ContentType.Route)
            {
                string path = Path.Combine(PathName, "Routes");
                if (Directory.Exists(path))
                    foreach (string item in Directory.GetDirectories(path))
                        yield return new ContentMSTSRoute(this, Path.Combine(path, item));
            }
            else if (type == ContentType.Consist)
            {
                string path = Path.Combine(PathName, "Trains", "Consists");
                if (Directory.Exists(path))
                    foreach (string item in Directory.GetFiles(path, "*.con"))
                        yield return new ContentMSTSConsist(this, Path.Combine(path, item));
            }
        }

        public override ContentBase GetContent(string name, ContentType type)
        {
            if (type == ContentType.Car)
            {
                string pathEng = Path.Combine(PathName, "Trains", "Trainset", name + ".eng");
                if (File.Exists(pathEng))
                    return new ContentMSTSCar(this, pathEng);

                string pathWag = Path.Combine(PathName, "Trains", "Trainset", name + ".wag");
                if (File.Exists(pathWag))
                    return new ContentMSTSCar(this, pathWag);
            }
            return base.GetContent(name, type);
        }
    }

    public class ContentMSTSRoute : ContentBase
    {
        public override ContentType Type => ContentType.Route;

        public ContentMSTSRoute(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileName(path);
            PathName = path;
        }

        public override IEnumerable<ContentBase> GetContent(ContentType type)
        {
            if (type == ContentType.Activity)
            {
                string path = Path.Combine(PathName, "Activities");
                if (Directory.Exists(path))
                    foreach (string item in Directory.GetFiles(path, "*.act"))
                        yield return new ContentMSTSActivity(this, Path.Combine(path, item));

                string pathOR = Path.Combine(PathName, @"Activities\OpenRails");
                if (Directory.Exists(pathOR))
                    foreach (var item in Enumerable.Concat(Directory.GetFiles(pathOR, "*.timetable_or"), Directory.GetFiles(pathOR, "*.timetable-or")))
                        yield return new ContentORTimetableActivity(this, Path.Combine(pathOR, item));
            }
        }

        public override ContentBase GetContent(string name, ContentType type)
        {
            if (type == ContentType.Path)
            {
                string path = Path.Combine(PathName, "Paths", name + ".pat");
                if (File.Exists(path))
                    return new ContentMSTSPath(this, path);
            }
            return base.GetContent(name, type);
        }
    }

    public class ContentMSTSActivity : ContentBase
    {
        public override ContentType Type => ContentType.Activity;

        public ContentMSTSActivity(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }

        public override ContentBase GetContent(string name, ContentType type)
        {
            if (type == ContentType.Service)
            {
                string[] names = name?.Split('|') ?? throw new ArgumentNullException(nameof(name));
                if (names.Length >= 2 && names[0] == "Player")
                    return new ContentMSTSService(this, GetRelatedPath("Services", names[1], ".srv"));
                if (names.Length >= 4 && names[0] == "AI")
                    return new ContentMSTSService(this, GetRelatedPath("Services", names[1], ".srv"), GetRelatedPath("Traffic", names[2], ".trf"), int.Parse(names[3], CultureInfo.InvariantCulture));
            }
            return base.GetContent(name, type);
        }

        private string GetRelatedPath(string type, string name, string extension)
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(PathName)), type, name + extension);
        }
    }

    public class ContentMSTSService : ContentBase
    {
        public override ContentType Type => ContentType.Service;
        public bool IsPlayer { get; private set; }
        public string TrafficPathName { get; private set; }
        public int TrafficIndex { get; private set; }

        public ContentMSTSService(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
            IsPlayer = true;
        }

        public ContentMSTSService(ContentBase parent, string path, string traffic, int traffixIndex)
            : this(parent, path)
        {
            IsPlayer = false;
            TrafficPathName = traffic;
            TrafficIndex = traffixIndex;
        }
    }

    public class ContentMSTSPath : ContentBase
    {
        public override ContentType Type => ContentType.Path;

        public ContentMSTSPath(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }
    }

    public class ContentMSTSConsist : ContentBase
    {
        public override ContentType Type => ContentType.Consist;

        public ContentMSTSConsist(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }
    }

    public class ContentMSTSCar : ContentBase
    {
        public override ContentType Type => ContentType.Car;

        public ContentMSTSCar(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }
    }

    public class ContentMSTSCab : ContentBase
    {
        public override ContentType Type => ContentType.Cab;

        public ContentMSTSCab(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }

        public override IEnumerable<ContentBase> GetContent(ContentType type)
        {
            if (type == ContentType.Texture)
            {
                foreach (string item in Directory.EnumerateFiles(Path.GetDirectoryName(PathName), "*.ace"))
                    yield return new ContentMSTSTexture(this, Path.Combine(PathName, item));
            }
        }
    }

    public class ContentMSTSModel : ContentBase
    {
        public override ContentType Type => ContentType.Model;

        public ContentMSTSModel(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }
    }

    public class ContentMSTSTexture : ContentBase
    {
        public override ContentType Type => ContentType.Texture;

        public ContentMSTSTexture(ContentBase parent, string path)
            : base(parent)
        {
            Name = Path.GetFileNameWithoutExtension(path);
            PathName = path;
        }
    }
}
