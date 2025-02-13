﻿// COPYRIGHT 2014, 2018 by the Open Rails project.
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
using System.Collections.ObjectModel;
using System.Linq;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

using ORTS.TrackViewer.Editing;

namespace ORTS.TrackViewer.Drawing
{
    /// <summary>
    /// Class to Draw multiple paths (all coming from MSTS .pat files). 
    /// No editing, just plain single-color drawing.
    /// Paths will be drawn in slightly different colors to make it (a bit) easier to distinguish between them.
    /// </summary>
    internal sealed class DrawMultiplePaths
    {

        /// <summary>For each path name, store the full file name of the .pat file</summary>
        private readonly Dictionary<string, string> fullPathNames;

        /// <summary>The paths that have already been loaded (.pat file has been read and parsed)</summary>
        private readonly Dictionary<string, Trainpath> loadedPaths;

        /// <summary>For each trainpath we have it is own DrawPath (each with their own color)</summary>
        private readonly Dictionary<Trainpath, DrawPath> drawPaths;

        /// <summary>List of trainpaths that have been selected</summary>
        private readonly List<Trainpath> selectedTrainpaths;
    
        private readonly TrackDB trackDB;
        private readonly TrackSectionsFile tsectionDat;

        /// <summary>
        /// Constructor
        /// </summary>
        public DrawMultiplePaths (Collection<PathModelHeader> paths)
        {
            trackDB = RuntimeData.Instance.TrackDB;
            tsectionDat = RuntimeData.Instance.TSectionDat;
            fullPathNames = new Dictionary<string, string>();
            loadedPaths = new Dictionary<string, Trainpath>();
            selectedTrainpaths = new List<Trainpath>();
            drawPaths = new Dictionary<Trainpath, DrawPath>();

            foreach (PathModelHeader path in paths)
            {
                string pathName = UserInterface.MenuControl.MakePathMenyEntryName(path);
                fullPathNames[pathName] = path.SourceFile();
            }
        }

        /// <summary>
        /// Returns the list of all available path names (names to describe the path both to user as to program)
        /// </summary>
        public string[] PathNames()
        {
            return fullPathNames.Keys.ToArray();
        }

        /// <summary>
        /// Return the Trainpath given a pathName. This will load the path if it has not been loaded before.
        /// </summary>
        /// <param name="pathName">The name of the path</param>
        private Trainpath TrainpathFromName(string pathName)
        {
            if (!loadedPaths.TryGetValue(pathName, out Trainpath newTrainpath))
            {
                newTrainpath = new Trainpath(trackDB, tsectionDat, fullPathNames[pathName]);
                loadedPaths[pathName] = newTrainpath;
                drawPaths[newTrainpath] = new DrawPath(trackDB, tsectionDat);
            }
            return newTrainpath;

        }

        /// <summary>
        /// Select or deselect one of the paths (select to be drawn)
        /// </summary>
        /// <param name="pathName">Name identifying the path</param>
        /// <param name="enable">set the selection state</param>
        public void SetSelection(string pathName, bool enable)
        {
            Trainpath trainpath = TrainpathFromName(pathName);
            if (enable)
            {
                if (!selectedTrainpaths.Contains(trainpath))
                {
                    selectedTrainpaths.Add(trainpath);
                }
            }
            else
            {
                selectedTrainpaths.Remove(trainpath);
            }
            ReColorAll();
        }

        /// <summary>
        /// Clear all selected trainpaths: no paths should be drawn now.
        /// </summary>
        public void ClearAll()
        {
            selectedTrainpaths.Clear();
            ReColorAll();
        }

        /// <summary>
        /// Recalculate and store the colors of all paths.
        /// Coloring is done automatically depending on the order and amount of selected paths.
        /// </summary>
        private void ReColorAll()
        {
            int count = selectedTrainpaths.Count;
            for (int index = 0; index < count; index++)
            {
                ColorScheme shadedColor = DrawColors.ShadeColor(DrawColors.otherPathsReferenceColor, index, count);
                Trainpath trainpath = selectedTrainpaths[index];
                drawPaths[trainpath].ColorSchemeMain = shadedColor;
                drawPaths[trainpath].ColorSchemeSiding = shadedColor;
                drawPaths[trainpath].ColorSchemeLast = shadedColor;
            }
        }
 
        /// <summary>
        /// Draws the paths that have been selected
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        public void Draw(DrawArea drawArea)
        {
            foreach (Trainpath trainpath in selectedTrainpaths)
            {
                drawPaths[trainpath].Draw(drawArea, trainpath.FirstNode);
            }
        }

        /// <summary>
        /// Return the color used for drawing the the path when selected, or null when not selected
        /// </summary>
        /// <param name="pathName"></param>
        /// <returns></returns>
        public Color? ColorOf(string pathName)
        {
            loadedPaths.TryGetValue(pathName, out Trainpath trainPath);
            if (trainPath == null || !selectedTrainpaths.Contains(trainPath))
            {
                return null;
            }
            return drawPaths[trainPath].ColorSchemeMain.TrackStraight;
        }
    }
}
