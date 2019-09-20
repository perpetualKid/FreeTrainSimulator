// COPYRIGHT 2014 by the Open Rails project.
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
using System.Diagnostics;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace Orts.ContentManager.Models
{
    public class Path
    {
        public readonly string Name;
        public readonly string StartName;
        public readonly string EndName;

        public readonly IEnumerable<Node> Nodes;

        public Path(Content content)
        {
            Debug.Assert(content.Type == ContentType.Path);
            if (System.IO.Path.GetExtension(content.PathName).Equals(".pat", StringComparison.OrdinalIgnoreCase))
            {
                var file = new PathFile(content.PathName);
                Name = file.Name;
                StartName = file.Start;
                EndName = file.End;

                var nodes = new List<Node>(file.PathNodes.Count);
                var nodeNexts = new List<List<Node>>(file.PathNodes.Count);
                foreach (var node in file.PathNodes)
                {
                    var pdp = file.DataPoints[(int)node.fromPDP];
                    var next = new List<Node>();
                    nodes.Add(new Node(pdp.Location.ToString(), node.PathFlags, node.WaitTime, next));
                    nodeNexts.Add(next);
                }
                for (var i = 0; i < file.PathNodes.Count; i++)
                {
                    if (file.PathNodes[i].HasNextMainNode)
                        nodeNexts[i].Add(nodes[(int)file.PathNodes[i].NextMainNode]);
                    if (file.PathNodes[i].HasNextSidingNode)
                        nodeNexts[i].Add(nodes[(int)file.PathNodes[i].NextSidingNode]);
                }
                Nodes = nodes;
            }
        }

        public class Node
        {
            public readonly string Location;
            public readonly PathFlags Flags;
            public readonly int WaitTime;
            public readonly IEnumerable<Node> Next;

            internal Node(string location, PathFlags flags, int waitTime, IEnumerable<Node> next)
            {
                Location = location;
                Flags = (PathFlags)((int)flags & 0xFFFF);
                WaitTime = waitTime;
                Next = next;
            }
        }
    }
}
