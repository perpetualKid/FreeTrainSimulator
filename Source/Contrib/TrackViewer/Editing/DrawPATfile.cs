// COPYRIGHT 2014, 2018 by the Open Rails project.
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
using System.Linq;
using Orts.Common;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Files;
using Orts.Menu.Entities;
using ORTS.TrackViewer.Drawing;

namespace ORTS.TrackViewer.Editing
{
    /// <summary>
    /// Class to draw a raw path from PATfile. With raw here is meant that there is no link whatsoever to the trackdatabase
    /// So only locations as defined in .pat file are used. Obviously for broken paths there can be quite a mismatch between 
    /// locations in the .pat file and those of the tracks.
    /// The drawing here is not updated during editing.
    /// 
    /// Main method is Draw.
    /// 
    /// The amount of points that are drawn can be varied, such that it is easier to follow the path (especially in 
    /// complicated cases.
    /// </summary>
    public class DrawPATfile
    {
        /// <summary>the parsed .pat file information</summary>
        private PathFile patFile;
        /// <summary>The filename of the .pat file</summary>
        public string FileName { get; private set; }

        /// <summary>Number of nodes that will be drawn. Start with a few</summary>
        private int numberToDraw = int.MaxValue / 2; // large, but not close to overflow
        
        /// <summary>Index of the last main node that has been drawn</summary>
        private int currentMainNodeIndex;

        /// <summary>Return the last drawn node</summary>
        public PathNode CurrentNode { get { return patFile.PathNodes[currentMainNodeIndex]; } }
        /// <summary>return the (Path Data Point?) belonging to the last drawn node</summary>
        public PathDataPoint CurrentPdp { get { return patFile.DataPoints[(int)CurrentNode.PathDataPoint]; } }
        /// <summary>Return the location of the last drawn node</summary>

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Contains the information (mainly filepath) needed for loading the .pat file</param>
        public DrawPATfile (Path path)
        {
            FileName = path.FilePath.Split('\\').Last();
            patFile = new PathFile(path.FilePath);
        }

        /// <summary>
        /// Draw the actual path coded in the PATfile (for a number of nodes that can be extended or reduced)
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        public void Draw(DrawArea drawArea)
        {
            
            //draw actual path
            currentMainNodeIndex = 0; // starting point
            int currentSidingNodeIndex = -1; // we start without siding path
            for (int i = 0; i < Math.Min(patFile.PathNodes.Count - 1, numberToDraw); i++)
            {

                // If we have a current siding track, we draw it step to the next main line first.
                if (currentSidingNodeIndex > 0)
                {
                    //while tracking a siding, it has its own main node
                    int nextNodeIndexOnSiding = (int)patFile.PathNodes[currentSidingNodeIndex].NextSidingNode;
                    if (nextNodeIndexOnSiding > 0) // because also this path can run off at the end
                    {
                        PathNode curNode = patFile.PathNodes[currentSidingNodeIndex];
                        ref readonly WorldLocation curLoc = ref patFile.DataPoints[(int)curNode.PathDataPoint].Location;
                        PathNode nextNode = patFile.PathNodes[nextNodeIndexOnSiding];
                        ref readonly WorldLocation nextLoc = ref patFile.DataPoints[(int)nextNode.PathDataPoint].Location;

                        drawArea.DrawLine(1, DrawColors.colorsPathSiding.TrackStraight, curLoc, nextLoc);
                    }
                    currentSidingNodeIndex = nextNodeIndexOnSiding;
                }

                PathNode curMainNode = patFile.PathNodes[currentMainNodeIndex];
                ref readonly WorldLocation curMainLoc = ref patFile.DataPoints[(int)curMainNode.PathDataPoint].Location;
                
                // from this main line point to the next siding node.
                // If there is a next siding node, we also reset the currentSidingNodeIndex
                // but probably it is not allowed to have siding
                int nextSidingNodeIndex = (int)curMainNode.NextSidingNode;             
                if (nextSidingNodeIndex >= 0)
                {
                    // draw the start of a siding path
                    PathNode nextNode = patFile.PathNodes[nextSidingNodeIndex];
                    ref readonly WorldLocation nextLoc = ref patFile.DataPoints[(int)nextNode.PathDataPoint].Location;

                    drawArea.DrawLine(1, DrawColors.colorsPathSiding.TrackStraight, curMainLoc, nextLoc);
                    currentSidingNodeIndex = nextSidingNodeIndex;
                }

                // From this main line point to the next
                int nextMainNodeIndex = (int)curMainNode.NextMainNode; 
                if (nextMainNodeIndex >= 0)
                {
                    PathNode nextNode = patFile.PathNodes[nextMainNodeIndex];
                    ref readonly WorldLocation nextLoc = ref patFile.DataPoints[(int)nextNode.PathDataPoint].Location;

                    drawArea.DrawLine(1, DrawColors.colorsPathMain.TrackStraight, curMainLoc, nextLoc);
                    currentMainNodeIndex = nextMainNodeIndex;
                }
            }
 
        }

        /// <summary>
        /// Draw more sections of the path
        /// </summary>
        public void ExtendPath()
        {
            int maxNumber = patFile.PathNodes.Count-1;
            if (++numberToDraw > maxNumber) numberToDraw = maxNumber;
        }

        /// <summary>
        /// Draw the full (complete) path
        /// </summary>
        public void ExtendPathFull()
        {
            numberToDraw = patFile.PathNodes.Count - 1;
        }

        /// <summary>
        /// Draw less sections of the path
        /// </summary>
        public void ReducePath()
        {
            if (--numberToDraw < 0) numberToDraw = 0;
        }

        /// <summary>
        /// Go to initial node and draw no path sections
        /// </summary>
        public void ReducePathFull()
        {
            numberToDraw = 0;
        }
    }
}
