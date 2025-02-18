﻿// COPYRIGHT 2014, 2015 by the Open Rails project.
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
using System.Windows.Controls;
using System.Windows.Forms.Integration;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

using ORTS.TrackViewer.Drawing;
using ORTS.TrackViewer.Editing;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// Interaction logic for StatusBarControl.xaml
    /// The statusbar at the bottom of the screen shows informational things like location, track items and possibly
    /// details on the track section, the path, ....
    /// Most of the items have a dedicated place in the statusbar. For flexibility the last item is simply a string
    /// that can contain various items (depending on the users setting/choice as well as for debug during development
    /// </summary>
    public sealed partial class StatusBarControl : UserControl, IDisposable
    {
        /// <summary>Height of the statusbar in pixels</summary>
        public int StatusbarHeight { get; private set; }
        private readonly ElementHost elementHost;

        /// <summary>
        /// Constructor for the statusbar
        /// </summary>
        /// <param name="trackViewer">Track viewer object that contains all the information we want to show the status for</param>
        internal StatusBarControl(TrackViewer trackViewer)
        {
            InitializeComponent();

            StatusbarHeight = (int) tvStatusbar.Height;

            //ElementHost object helps us to connect a WPF User Control.
            elementHost = new ElementHost
            {
                Location = new System.Drawing.Point(0, 0),
                TabIndex = 1,
                Child = this
            };
            System.Windows.Forms.Control.FromHandle(trackViewer.Window.Handle).Controls.Add(elementHost);

        }

        /// <summary>
        /// set the size of the statusbar control (also after rescaling)
        /// </summary>
        /// <param name="width">Width of the statusbar</param>
        /// <param name="height">Height of the statusbar</param>
        /// <param name="yBottom">Y-value in screen pixels at the bottom of the statusbar</param>
        public void SetScreenSize(int width, int height, int yBottom)
        {
            elementHost.Location = new System.Drawing.Point(0, yBottom-height);
            elementHost.Size = new System.Drawing.Size(width, height);
        }

        /// <summary>
        /// Update the various elements in the statusbar
        /// </summary>
        /// <param name="trackViewer">trackViewer object that contains all relevant data</param>
        /// <param name="mouseLocation">The Worldlocation of the mouse pointer</param>
        internal void Update(TrackViewer trackViewer, in WorldLocation mouseLocation)
        {
            ResetAdditionalText();

            SetMouseLocationStatus(mouseLocation);

            SetTrackIndexStatus(trackViewer);

            SetTrackItemStatus(trackViewer);

            AddFPS(trackViewer);
            AddVectorSectionStatus(trackViewer);
            AddPATfileStatus(trackViewer);
            AddTrainpathStatus(trackViewer);
            AddTerrainStatus(trackViewer);
        }

        /// <summary>
        /// Update the status of the track index
        /// </summary>
        /// <param name="trackViewer"></param>
        private void SetTrackIndexStatus(TrackViewer trackViewer)
        {
            Drawing.CloseToMouseTrack closestTrack = trackViewer.DrawTrackDB.ClosestTrack;
            if (closestTrack == null) return;
            TrackNode tn = closestTrack.TrackNode;
            if (tn == null) return;
            statusTrIndex.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0} ", tn.Index);
            //debug: statusAdditional.Text += Math.Sqrt((double)trackViewer.drawTrackDB.closestTrack.ClosestMouseDistanceSquared);
        }

        /// <summary>
        /// Reset (is clear) the additionalText line
        /// </summary>
        private void ResetAdditionalText()
        {
            statusAdditional.Text = string.Empty;
        }

        /// <summary>
        /// Set the status of the closest trackItem (junction or other item)
        /// </summary>
        /// <param name="trackViewer"></param>
        private void SetTrackItemStatus(TrackViewer trackViewer)
        {
            // Track items: clear first
            statusTrItemType.Text = statusTrItemIndex.Text =
                statusTrItemLocationX.Text = statusTrItemLocationZ.Text = string.Empty;

            ORTS.TrackViewer.Drawing.CloseToMouseItem closestItem = trackViewer.DrawTrackDB.ClosestTrackItem;
            ORTS.TrackViewer.Drawing.CloseToMouseJunctionOrEnd closestJunction = trackViewer.DrawTrackDB.ClosestJunctionOrEnd;
            ORTS.TrackViewer.Drawing.CloseToMousePoint closestPoint;
            if (closestItem != null && closestItem.IsCloserThan(closestJunction))
            {
                closestPoint = closestItem;
            }
            else if (closestJunction.JunctionOrEndNode != null)
            {
                closestPoint = closestJunction;
            }
            else
            {
                closestPoint = null;
            }

            if (closestPoint != null)
            {
                statusTrItemType.Text = closestPoint.Description;
                statusTrItemIndex.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0} ", closestPoint.Index);
                statusTrItemLocationX.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,3:F3} ", closestPoint.X);
                statusTrItemLocationZ.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    "{0,3:F3} ", closestPoint.Z);
                AddSignalStatus(closestPoint.Description, closestPoint.Index);
                AddNamesStatus(closestPoint.Description, closestPoint.Index);
            }
        }

        /// <summary>
        /// Put the mouse location in the statusbar
        /// </summary>
        /// <param name="mouseLocation"></param>
        private void SetMouseLocationStatus(in WorldLocation mouseLocation)
        {
            tileXZ.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,-7} {1,-7}", mouseLocation.Tile.X, mouseLocation.Tile.Z);
            LocationX.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,3:F3} ", mouseLocation.Location.X);
            LocationZ.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,3:F3} ", mouseLocation.Location.Z);
        }

        /// <summary>
        /// Add information about the closest vector section
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddVectorSectionStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowVectorSections)
            {
                TrackVectorSection tvs = trackViewer.DrawTrackDB.ClosestTrack.VectorSection;
                if (tvs == null) return;
                int shapeIndex = tvs.ShapeIndex;
                string shapeName = "Unknown:" + shapeIndex.ToString(System.Globalization.CultureInfo.CurrentCulture);
                try
                {
                    // Try to find a fixed track
                    TrackShape shape = RuntimeData.Instance.TSectionDat.TrackShapes[shapeIndex];
                    shapeName = shape.FileName;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // try to find a dynamic track
                    try
                    {
                        TrackPath trackPath = RuntimeData.Instance.TSectionDat.TrackSectionIndex[tvs.ShapeIndex];
                        shapeName = "<dynamic ?>";
                        foreach (int trackSection in trackPath.TrackSections)
                        {
                            if (trackSection == tvs.SectionIndex)
                            {
                                shapeName = "<dynamic>";
                            }
                            // For some reason I do not undestand the (route) section.tdb. trackpaths are not consistent tracksections
                            // so this foreach loop will not always find a combination
                        }
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                    }
                }
                statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    " VectorSection ({3}/{4}) filename={2} Index={0} shapeIndex={1}",
                    tvs.SectionIndex, shapeIndex, shapeName,
                        trackViewer.DrawTrackDB.ClosestTrack.TrackVectorSectionIndex + 1,
                        (trackViewer.DrawTrackDB.ClosestTrack.TrackNode as TrackVectorNode).TrackVectorSections.Length);
            }
        }

        /// <summary>
        /// Add information from Trainpaths
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddTrainpathStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowTrainpath && (trackViewer.PathEditor != null))
            {
                if (trackViewer.PathEditor.HasValidPath)
                {
                    //gather some info on path status
                    List<string> statusItems = new List<string>();
                    
                    if (trackViewer.PathEditor.HasEndingPath) statusItems.Add("good end");
                    if (trackViewer.PathEditor.HasBrokenPath) statusItems.Add("broken");
                    if (trackViewer.PathEditor.HasModifiedPath) statusItems.Add("modified");
                    if (trackViewer.PathEditor.HasStoredTail) statusItems.Add("stored tail");
                    
                    string pathStatus = string.Join(", ", statusItems.ToArray());
                    
                    ORTS.TrackViewer.Editing.TrainpathNode curNode = trackViewer.PathEditor.CurrentNode;
                    
                    statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                        " {0} ({4}): TVNs=[{1} {2}] (type={3})",
                        trackViewer.PathEditor.FileName, curNode.NextMainTvnIndex, curNode.NextSidingTvnIndex,
                        curNode.NodeType, pathStatus);

                    if (curNode.IsBroken)
                    {
                        statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                            " Broken: {0} ", curNode.BrokenStatusString());
                    }
                    if (curNode is TrainpathVectorNode curVectorNode && curNode.NodeType == TrainpathNodeType.Stop)
                    {
                        statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                            " (wait-time={0}s)",
                            curVectorNode.WaitTimeS);
                    }

                }
                else
                {
                    statusAdditional.Text += "Invalid path";
                }
            }
        }

        /// <summary>
        /// Add information of the basic MSTS PATfile
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddPATfileStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowPATfile && (trackViewer.DrawPATfile != null))
            {
                PathNode curNode = trackViewer.DrawPATfile.CurrentNode;
                statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    " {7}: {3}, {4} [{1} {2}] [{5} {6}] <{0}>",
                    curNode.NodeType, curNode.NextMainNode, curNode.NextSidingNode,
                    curNode.Location.Location.X, curNode.Location.Location.Z, (curNode.NodeType & FreeTrainSimulator.Common.PathNodeType.Junction) == FreeTrainSimulator.Common.PathNodeType.Junction, (curNode.NodeType & FreeTrainSimulator.Common.PathNodeType.Invalid) == FreeTrainSimulator.Common.PathNodeType.Invalid, trackViewer.DrawPATfile.FileName);
            }
        }

        /// <summary>
        /// Add the FPS to the statusbar (Frames Per Second)
        /// </summary>
        private void AddFPS(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowFPS)
            {
                statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    " FPS={0:F1} ", trackViewer.FrameRate.SmoothedValue);
            }
        }

        /// <summary>
        /// Add information from terrain
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddTerrainStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowTerrain && (trackViewer.drawTerrain != null))
            {
                statusAdditional.Text += trackViewer.drawTerrain.StatusInformation;
            }
        }

        /// <summary>
        /// Add information from signal
        /// </summary>
        /// <param name="trackViewer">The trackviewer we need to find the trackDB</param>
        /// <param name="description">The description of the item we might want to show, needed to make sure it is a proper item</param>
        /// <param name="index">The index of the item to show</param>
        private void AddSignalStatus(string description, int index)
        {
            if (!Properties.Settings.Default.statusShowSignal) 
                return;
            if (!string.Equals(description, "signal", StringComparison.OrdinalIgnoreCase)) 
                return;
            statusAdditional.Text += "signal shape = ";
            statusAdditional.Text += RouteData.GetSignalFilename(index);
        }

        /// <summary>
        /// Add information from platform and station name
        /// </summary>
        /// <param name="trackViewer">The trackviewer we need to find the trackDB</param>
        /// <param name="description">The description of the item we might want to show, needed to make sure it is a proper item</param>
        /// <param name="index">The index of the item to show</param>
        private void AddNamesStatus(string description, int index)
        {
            if (!Properties.Settings.Default.statusShowNames) 
                return;
            if (!string.Equals(description, "platform", StringComparison.OrdinalIgnoreCase)) 
                return;

            if (!(RuntimeData.Instance.TrackDB.TrackItems[index] is PlatformItem platform))
                return;
            statusAdditional.Text += string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0} ({1})", platform.Station, platform.ItemName);
        }

        #region IDisposable
        private bool disposed;
        /// <summary>
        /// Implementing IDisposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true; // to prevent infinite loop. Probably elementHost should not be part of this class
                if (disposing)
                {
                    // Dispose managed resources.
                    elementHost.Dispose();
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            disposed = true;
        }
        #endregion
    }
}
