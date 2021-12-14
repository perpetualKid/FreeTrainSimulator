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

namespace Orts.ContentManager
{
    // Root
    //   Collection
    //     Package
    //       Route
    //         Activity
    //         Service - the timetabled stops and other actions along a path
    //         Path - the physical route taken
    //         Scenery
    //         Model
    //         Texture
    //       Consist
    //       Car
    //         Model
    //         Texture
    //       Cab
    //         Texture
    public enum ContentType
    {
        Root,
        Collection,
        Package,
        Route,
        Activity,
        Service,
        Path,
        Consist,
        Car,
        Cab,
        Scenery,
        Model,
        Texture,
    }

    public abstract class ContentBase
    {
        public ContentBase Parent { get; set; }
        public abstract ContentType Type { get; }
        public string Name { get; protected set; }
        public string PathName { get; protected set; }

        public static bool operator ==(ContentBase left, ContentBase right)
        {
            return left?.Equals(right) ?? ReferenceEquals(left, right);
        }

        public static bool operator !=(ContentBase left, ContentBase right)
        {
            return !(left == right);
        }

        protected ContentBase(ContentBase parent)
        {
            Parent = parent;
        }

        public override bool Equals(object obj)
        {
            ContentBase content = obj as ContentBase;
            return content != null && Type == content.Type && Name == content.Name && PathName == content.PathName;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, PathName);
        }

        public override string ToString()
        {
            return $"{Type}({PathName})";
        }

        public virtual IEnumerable<ContentBase> GetContent(ContentType type)
        {
            return Array.Empty<ContentBase>();
        }

        public virtual ContentBase GetContent(string name, ContentType type)
        {
            // This is a very naive implementation which is meant only for prototyping and maybe as a final backstop.
            IEnumerable<ContentBase> children = GetContent(type);
            foreach (ContentBase child in children)
            {
                if (child.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
            // This is how Get(name, type) is meant to work: stepping up the hierarchy as needed.
            if (Parent != null)
                return Parent.GetContent(name, type);
            // We're at the top, sorry.
            return null;
        }
    }
}
