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
        public string Name { get; }
        public string StartName { get; }
        public string EndName { get; }

        public IEnumerable<PathNode> Nodes { get; }

        public Path(ContentBase content)
        {
            Debug.Assert(content?.Type == ContentType.Path);
            if (System.IO.Path.GetExtension(content.PathName).Equals(".pat", StringComparison.OrdinalIgnoreCase))
            {
                PathFile file = new PathFile(content.PathName);
                Name = file.Name;
                StartName = file.Start;
                EndName = file.End;

                List<PathNode> nodes = new List<PathNode>(file.PathNodes.Count);
                List<List<PathNode>> nodeNexts = new List<List<PathNode>>(file.PathNodes.Count);

                foreach (Formats.Msts.Models.PathNode node in file.PathNodes)
                {
                    List<PathNode> next = new List<PathNode>();
                    nodes.Add(new PathNode(node.Location.ToString(), node.NodeType, node.WaitTime, next));
                    nodeNexts.Add(next);
                }
                for (int i = 0; i < file.PathNodes.Count; i++)
                {
                    if (file.PathNodes[i].NextMainNode > -1)
                        nodeNexts[i].Add(nodes[file.PathNodes[i].NextMainNode]);
                    if (file.PathNodes[i].NextSidingNode > -1)
                        nodeNexts[i].Add(nodes[file.PathNodes[i].NextSidingNode]);
                }
                Nodes = nodes;
            }
        }
    }
    public class PathNode
    {
        public string Location { get; }
        public PathNodeType NodeType { get; }
        public int WaitTime { get; }
        public IEnumerable<PathNode> Next { get; }

        internal PathNode(string location, PathNodeType nodeType, int waitTime, IEnumerable<PathNode> next)
        {
            Location = location;
            NodeType = nodeType;
            WaitTime = waitTime;
            Next = next;
        }
    }

}
