// COPYRIGHT 2010 - 2020 by the Open Rails project.
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
using System.Diagnostics;
using System.Drawing;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;

using Color = System.Drawing.Color;

namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    public partial class DispatchViewer
    {
        public void SetControls()
        {
            // Default is Timetable Tab, unless in Multi-Player mode
            if (tWindow.SelectedIndex == 1) // 0 for Dispatch Window, 1 for Timetable Window
            {
                // Default is All Trains, unless in Timetable mode
                rbShowActiveTrainLabels.Checked = simulator.TimetableMode;
                rbShowAllTrainLabels.Checked = !(rbShowActiveTrainLabels.Checked);

                ShowTimetableControls(true);
                ShowDispatchControls(false);
                SetTimetableMedia();
            }
            else
            {
                ShowTimetableControls(false);
                ShowDispatchControls(true);
                SetDispatchMedia();
            }
        }

        private void ShowDispatchControls(bool dispatchView)
        {
            var multiPlayer = MultiPlayerManager.IsMultiPlayer() && dispatchView;
            msgAll.Visible = multiPlayer;
            msgSelected.Visible = multiPlayer;
            composeMSG.Visible = multiPlayer;
            MSG.Visible = multiPlayer;
            messages.Visible = multiPlayer;
            AvatarView.Visible = multiPlayer;
            composeMSG.Visible = multiPlayer;
            reply2Selected.Visible = multiPlayer;
            chkShowAvatars.Visible = multiPlayer;
            chkAllowUserSwitch.Visible = multiPlayer;
            chkAllowNew.Visible = multiPlayer;
            chkBoxPenalty.Visible = multiPlayer;
            chkPreferGreen.Visible = multiPlayer;
            btnAssist.Visible = multiPlayer;
            btnNormal.Visible = multiPlayer;
            rmvButton.Visible = multiPlayer;

            if (multiPlayer)
            {
                chkShowAvatars.Checked = Simulator.Instance.Settings.ShowAvatar;
                pbCanvas.Location = new System.Drawing.Point(pbCanvas.Location.X, label1.Location.Y + 18);
                refreshButton.Text = "View Self";
            }

            btnSeeInGame.Visible = dispatchView;
            btnFollow.Visible = dispatchView;
            windowSizeUpDown.Visible = dispatchView;
            label1.Visible = dispatchView;
            resLabel.Visible = dispatchView;
            refreshButton.Visible = dispatchView;
        }

        private void SetDispatchMedia()
        {
            trainFont = new Font("Arial", 14, FontStyle.Bold);
            sidingFont = new Font("Arial", 12, FontStyle.Bold);
            trainBrush = new SolidBrush(Color.Red);
            sidingBrush = new SolidBrush(Color.Blue);
            pbCanvas.BackColor = Color.White;
        }

        private void ShowTimetableControls(bool timetableView)
        {
            lblSimulationTimeText.Visible = timetableView;
            lblSimulationTime.Visible = timetableView;
            lblShow.Visible = timetableView;
            cbShowPlatforms.Visible = timetableView;
            cbShowPlatformLabels.Visible = timetableView;
            cbShowSidings.Visible = timetableView;
            cbShowTrainLabels.Visible = timetableView;
            cbShowTrainState.Visible = timetableView;
            bTrainKey.Visible = timetableView;
            gbTrainLabels.Visible = timetableView;
            rbShowActiveTrainLabels.Visible = timetableView;
            rbShowAllTrainLabels.Visible = timetableView;
            lblDayLightOffsetHrs.Visible = timetableView;
            nudDaylightOffsetHrs.Visible = timetableView;
        }

        private void SetTimetableMedia()
        {
            Name = "Timetable Window";
            trainFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
            sidingFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
            PlatformFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
            trainBrush = new SolidBrush(Color.Red);
            inactiveTrainBrush = new SolidBrush(Color.DarkRed);
            sidingBrush = new SolidBrush(Color.Blue);
            platformBrush = new SolidBrush(Color.DarkBlue);
            pbCanvas.BackColor = Color.FromArgb(250, 240, 230);
        }

        private void AdjustControlLocations()
        {
            if (Height < 600 || Width < 800)
                return;

            if (oldHeight != Height || oldWidth != Width) //use the label "Res" as anchor point to determine the picture size
            {
                oldWidth = Width;
                oldHeight = Height;

                pbCanvas.Top = 50;
                pbCanvas.Width = label1.Left - 25;                  // 25 pixels found by trial and error
                pbCanvas.Height = Height - pbCanvas.Top - 45;  // 45 pixels found by trial and error

                if (pbCanvas.Image != null)
                    pbCanvas.Image.Dispose();
                pbCanvas.Image = new Bitmap(pbCanvas.Width, pbCanvas.Height);
            }
            if (firstShow)
            {
                // Center the view on the player's locomotive
                var pos = Simulator.Instance.PlayerLocomotive.WorldPosition;
                var ploc = new PointF(pos.TileX * 2048 + pos.Location.X, pos.TileZ * 2048 + pos.Location.Z);
                viewWindow.X = ploc.X - minX - viewWindow.Width / 2;
                viewWindow.Y = ploc.Y - minY - viewWindow.Width / 2;
                firstShow = false;
            }

            // Sufficient to accommodate the whole route plus 50%
            var xRange = maxX - minX;
            var yRange = maxY - minY;
            var maxSize = (int)(((xRange > yRange) ? xRange : yRange) * 1.5);
            windowSizeUpDown.Maximum = (decimal)maxSize;
        }

        private void PopulateItemLists()
        {
            foreach (var item in RuntimeData.Instance.TrackDB.TrackItems)
            {
                switch (item)
                {
                    case SignalItem signalItem:
                        if (signalItem.SignalObject >= 0 && signalItem.SignalObject < Simulator.Instance.SignalEnvironment.Signals.Count)
                        {
                            Signal s = Simulator.Instance.SignalEnvironment.Signals[signalItem.SignalObject];
                            if (s != null && s.IsSignal && s.SignalNormal())
                                signals.Add(new SignalWidget(signalItem, s));
                        }
                        break;
                    case SidingItem sidingItem:
                        // Sidings have 2 ends but are not always listed in pairs in the *.tdb file
                        // Neither are their names unique (e.g. Bernina Bahn).
                        // Find whether this siding is a new one or the other end of an old one.
                        // If other end, then find the right-hand one as the location for a single label.
                        // Note: Find() within a foreach() loop is O(n^2) but is only done at start.
                        var oldSidingIndex = sidings.FindIndex(r => r.LinkId == item.TrackItemId && r.Name == item.ItemName);
                        if (oldSidingIndex < 0)
                        {
                            var newSiding = new SidingWidget(item as SidingItem);
                            sidings.Add(newSiding);
                        }
                        else
                        {
                            var oldSiding = sidings[oldSidingIndex];
                            var oldLocation = oldSiding.Location;
                            var newLocation = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);

                            // Because these are structs, not classes, compiler won't let you overwrite them.
                            // Instead create a single item which replaces the 2 platform items.
                            var replacement = new SidingWidget(item as SidingItem)
                            {
                                Location = new PointF()
                                {
                                    X = (oldLocation.X + newLocation.X) / 2,
                                    Y = (oldLocation.Y + newLocation.Y) / 2
                                }
                            };
                            // Replace the old siding item with the replacement
                            sidings.RemoveAt(oldSidingIndex);
                            sidings.Add(replacement);
                        }
                        break;
                    case PlatformItem platformItem:
                        // Platforms have 2 ends but are not always listed in pairs in the *.tdb file
                        // Neither are their names unique (e.g. Bernina Bahn).
                        // Find whether this platform is a new one or the other end of an old one.
                        // If other end, then find the right-hand one as the location for a single label.
                        var oldPlatformIndex = platforms.FindIndex(r => r.LinkId == item.TrackItemId && r.Name == item.ItemName);
                        if (oldPlatformIndex < 0)
                        {
                            var newPlatform = new PlatformWidget(item as PlatformItem)
                            {
                                Extent1 = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z)
                            };
                            platforms.Add(newPlatform);
                        }
                        else
                        {
                            var oldPlatform = platforms[oldPlatformIndex];
                            var oldLocation = oldPlatform.Location;
                            var newLocation = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);

                            // Because these are structs, not classes, compiler won't let you overwrite them.
                            // Instead create a single item which replaces the 2 platform items.
                            var replacement = new PlatformWidget(item as PlatformItem)
                            {
                                Extent1 = oldLocation,
                                Extent2 = newLocation,
                                Location = (oldLocation.X > newLocation.X) ? oldLocation : newLocation
                            };

                            // Replace the old platform item with the replacement
                            platforms.RemoveAt(oldPlatformIndex);
                            platforms.Add(replacement);
                        }
                        break;

                    default:
                        break;
                }
            }

            foreach (var p in platforms)
                if (p.Extent1.IsEmpty || p.Extent2.IsEmpty)
                    Trace.TraceWarning("Platform '{0}' is incomplete as the two ends do not match. It will not show in full in the Timetable Tab of the Map Window", p.Name);
        }

        private void GenerateTimetableView(bool dragging)
        {
            AdjustControlLocations();
            ShowSimulationTime();

            if (pbCanvas.Image == null)
                InitImage();

            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(pbCanvas.Image))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(pbCanvas.BackColor);

                // Set scales. subX & subY give top-left location in meters from world origin.
                subX = minX + viewWindow.X;
                subY = minY + viewWindow.Y;

                // Get scale in pixels/meter
                xScale = pbCanvas.Width / viewWindow.Width;
                yScale = pbCanvas.Height / viewWindow.Height;
                // Make X and Y scales the same to maintain correct angles.
                xScale = yScale = Math.Max(xScale, yScale);

                // Set the default pen to represent 1 meter.
                var scale = (float)Math.Round((double)xScale);  // Round to nearest pixels/meter
                var penWidth = (int)MathHelper.Clamp(scale, 1, 4);  // Keep 1 <= width <= 4 pixels

                // Choose pens
                Pen p = grayPen;
                grayPen.Width = greenPen.Width = orangePen.Width = redPen.Width = penWidth;
                pathPen.Width = penWidth * 2;

                // First so track is drawn over the thicker platform line
                DrawPlatforms(g, penWidth);

                // Draw track
                PointF scaledA, scaledB;
                DrawTrack(g, p, out scaledA, out scaledB);

                if (dragging == false)
                {
                    // Draw trains and path
                    DrawTrains(g, scaledA, scaledB);

                    // Keep widgetWidth <= 15 pixels
                    var widgetWidth = Math.Min(penWidth * 6, 15);

                    // Draw signals on top of path so they are easier to see.
                    ShowSignals(g, scaledB, widgetWidth);

                    // Draw switches
                    switchItemsDrawn.Clear();
                    ShowSwitches(g, widgetWidth);

                    // Draw labels for sidings and platforms last so they go on top for readability
                    CleanTextCells();  // Empty the listing of labels ready for adding labels again
                    ShowPlatformLabels(g); // Platforms take priority over sidings and signal states
                    ShowSidingLabels(g);
                }
                DrawZoomTarget(g);
            }
            pbCanvas.Invalidate(); // Triggers a re-paint
        }

        /// <summary>
        /// Indicates the location around which the image is zoomed.
        /// If user drags an item of interest into this target box and zooms in, the item will remain in view.
        /// </summary>
        /// <param name="g"></param>
        private void DrawZoomTarget(System.Drawing.Graphics g)
        {
            if (dragging)
            {
                const int size = 24;
                var top = pbCanvas.Height / 2 - size / 2;
                var left = (int)(pbCanvas.Width / 2 - size / 2);
                g.DrawRectangle(grayPen, left, top, size, size);
            }
        }

        private void ShowSimulationTime()
        {
            var ct = TimeSpan.FromSeconds(Simulator.Instance.ClockTime);
            lblSimulationTime.Text = $"{ct:hh}:{ct:mm}:{ct:ss}";
        }

        private void DrawPlatforms(System.Drawing.Graphics g, int penWidth)
        {
            if (cbShowPlatforms.Checked)
            {
                // Platforms can be obtrusive, so draw in solid blue only when zoomed in and fade them as we zoom out
                switch (penWidth)
                {
                    case 1:
                        platformPen.Color = Color.FromArgb(0, 0, 255);
                        break;
                    case 2:
                        platformPen.Color = Color.FromArgb(150, 150, 255);
                        break;
                    default:
                        platformPen.Color = Color.FromArgb(200, 200, 255);
                        break;
                }

                var width = grayPen.Width * 3;
                platformPen.Width = width;
                foreach (var p in platforms)
                {
                    var scaledA = new PointF((p.Extent1.X - subX) * xScale, pbCanvas.Height - (p.Extent1.Y - subY) * yScale);
                    var scaledB = new PointF((p.Extent2.X - subX) * xScale, pbCanvas.Height - (p.Extent2.Y - subY) * yScale);

                    FixForBadData(width, ref scaledA, ref scaledB, p.Extent1, p.Extent2);
                    g.DrawLine(platformPen, scaledA, scaledB);
                }
            }
        }

        /// <summary>
        /// In case of missing X,Y values, just draw a blob at the non-zero end.
        /// </summary>
        private void FixForBadData(float width, ref PointF scaledA, ref PointF scaledB, PointF Extent1, PointF Extent2)
        {
            if (Extent1.X == 0 || Extent1.Y == 0)
            {
                scaledA.X = scaledB.X + width;
                scaledA.Y = scaledB.Y + width;
            }
            else if (Extent2.X == 0 || Extent2.Y == 0)
            {
                scaledB.X = scaledA.X + width;
                scaledB.Y = scaledA.Y + width;
            }
        }

        private void DrawTrack(System.Drawing.Graphics g, Pen p, out PointF scaledA, out PointF scaledB)
        {
            PointF[] points = new PointF[3];
            scaledA = new PointF(0, 0);
            scaledB = new PointF(0, 0);
            PointF scaledC = new PointF(0, 0);
            foreach (var line in segments)
            {
                scaledA = line.A.Scale(xScale, yScale, subX, subY);
                scaledA.Y = pbCanvas.Height - scaledA.Y;
                scaledB = line.B.Scale(xScale, yScale, subX, subY);
                scaledB.Y = pbCanvas.Height - scaledB.Y;

                if ((scaledA.X < 0 && scaledB.X < 0) || (scaledA.Y < 0 && scaledB.Y < 0))
                    continue;

                if (line.isCurved == true)
                {
                    scaledC.X = (line.C.Location.Location.X - subX) * xScale;
                    scaledC.Y = pbCanvas.Height - (line.C.Location.Location.Z - subY) * yScale;
                    points[0] = scaledA;
                    points[1] = scaledC;
                    points[2] = scaledB;
                    g.DrawCurve(p, points);
                }
                else
                    g.DrawLine(p, scaledA, scaledB);
            }
        }

        private void ShowSwitches(System.Drawing.Graphics g, float width)
        {
            for (var i = 0; i < switches.Count; i++)
            {
                SwitchWidget sw = switches[i];

                var x = (sw.Location.X - subX) * xScale;
                var y = pbCanvas.Height - (sw.Location.Y - subY) * yScale;
                if (x < 0 || y < 0)
                    continue;

                var scaledItem = new PointF() { X = x, Y = y };

                if (sw.Item.SelectedRoute == sw.main)
                    g.FillEllipse(Brushes.Black, DispatchViewer.GetRect(scaledItem, width));
                else
                    g.FillEllipse(Brushes.Gray, DispatchViewer.GetRect(scaledItem, width));

                sw.Location2D.X = scaledItem.X;
                sw.Location2D.Y = scaledItem.Y;
                switchItemsDrawn.Add(sw);
            }
        }

        private void ShowSignals(System.Drawing.Graphics g, PointF scaledB, float width)
        {
            foreach (var s in signals)
            {
                if (float.IsNaN(s.Location.X) || float.IsNaN(s.Location.Y))
                    continue;
                var x = (s.Location.X - subX) * xScale;
                var y = pbCanvas.Height - (s.Location.Y - subY) * yScale;
                if (x < 0 || y < 0)
                    continue;

                var scaledItem = new PointF() { X = x, Y = y };
                s.Location2D.X = scaledItem.X;
                s.Location2D.Y = scaledItem.Y;
                if (s.Signal.SignalNormal())
                {
                    var color = Brushes.Lime; // bright colour for readability
                    var pen = greenPen;
                    if (s.IsProceed == 0)
                    {
                    }
                    else if (s.IsProceed == 1)
                    {
                        color = Brushes.Yellow; // bright colour for readbility
                        pen = orangePen;
                    }
                    else
                    {
                        color = Brushes.Red;
                        pen = redPen;
                    }
                    g.FillEllipse(color, DispatchViewer.GetRect(scaledItem, width));
                    if (s.hasDir)
                    {
                        scaledB.X = (s.Dir.X - subX) * xScale;
                        scaledB.Y = pbCanvas.Height - (s.Dir.Y - subY) * yScale;
                        g.DrawLine(pen, scaledItem, scaledB);
                    }
                }
            }
        }

        private void ShowSidingLabels(System.Drawing.Graphics g)
        {
            if (cbShowSidings.CheckState == System.Windows.Forms.CheckState.Checked)
                foreach (var s in sidings)
                {
                    var scaledItem = new PointF();

                    scaledItem.X = (s.Location.X - subX) * xScale;
                    scaledItem.Y = GetUnusedYLocation(scaledItem.X, pbCanvas.Height - (s.Location.Y - subY) * yScale, s.Name);
                    if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
                        g.DrawString(s.Name, sidingFont, sidingBrush, scaledItem);
                }
        }

        private void ShowPlatformLabels(System.Drawing.Graphics g)
        {
            var platformMarginPxX = 5;

            if (cbShowPlatformLabels.CheckState == System.Windows.Forms.CheckState.Checked)
                foreach (var p in platforms)
                {
                    var scaledItem = new PointF();
                    scaledItem.X = (p.Location.X - subX) * xScale + platformMarginPxX;
                    var yPixels = pbCanvas.Height - (p.Location.Y - subY) * yScale;

                    // If track is close to horizontal, then start label search 1 row down to minimise overwriting platform line.
                    if (p.Extent1.X != p.Extent2.X
                        && Math.Abs((p.Extent1.Y - p.Extent2.Y) / (p.Extent1.X - p.Extent2.X)) < 0.1)
                        yPixels += DispatchViewer.spacing;

                    scaledItem.Y = GetUnusedYLocation(scaledItem.X, pbCanvas.Height - (p.Location.Y - subY) * yScale, p.Name);
                    if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
                        g.DrawString(p.Name, PlatformFont, platformBrush, scaledItem);
                }
        }

        private void DrawTrains(System.Drawing.Graphics g, PointF scaledA, PointF scaledB)
        {
            var margin = 30 * xScale;   //margins to determine if we want to draw a train
            var margin2 = 5000 * xScale;

            selectedTrainList.Clear();

            if (simulator.TimetableMode)
            {
                // Add the player's train
                if (simulator.PlayerLocomotive.Train is Orts.Simulation.AIs.AITrain)
                    selectedTrainList.Add(simulator.PlayerLocomotive.Train as Orts.Simulation.AIs.AITrain);

                // and all the other trains
                foreach (var train in simulator.AI.AITrains)
                    selectedTrainList.Add(train);
            }
            else
            {
                foreach (var train in simulator.Trains)
                    selectedTrainList.Add(train);
            }

            foreach (var train in selectedTrainList)
            {
                string trainName;
                WorldPosition worldPos;
                TrainCar locoCar = null;
                if (train.LeadLocomotive != null)
                {
                    trainName = Train.GetTrainName(train.LeadLocomotive.CarID);
                    locoCar = train.LeadLocomotive;
                }
                else if (train.Cars != null && train.Cars.Count > 0)
                {
                    trainName = Train.GetTrainName(train.Cars[0].CarID);
                    if (train.TrainType == TrainType.Ai)
                        trainName = $"{train.Number}:{train.Name}";

                    locoCar = train.Cars.Where(r => r is MSTSLocomotive).FirstOrDefault();

                    // Skip trains with no loco
                    if (locoCar == null)
                        continue;
                }
                else
                    continue;

                // Draw the path, then each car of the train, then maybe the name
                var loc = train.FrontTDBTraveller.WorldLocation;
                float x = (loc.TileX * 2048 + loc.Location.X - subX) * xScale;
                float y = pbCanvas.Height - (loc.TileZ * 2048 + loc.Location.Z - subY) * yScale;

                // If train out of view then skip it.
                if (x < -margin2
                    || y < -margin2)
                    continue;

                // pen | train | Values for a good presentation
                //  1		10
                //  2       12
                //  3       14
                //  4		16
                trainPen.Width = grayPen.Width * 6;

                foreach (var car in train.Cars)
                    DrawCar(g, train, car, locoCar, margin);

                worldPos = locoCar.WorldPosition;
                PointF scaledTrain = new PointF
                {
                    X = (worldPos.TileX * 2048 - subX + worldPos.Location.X) * xScale,
                    Y = -25 + pbCanvas.Height - (worldPos.TileZ * 2048 - subY + worldPos.Location.Z) * yScale
                };
                if (cbShowTrainLabels.Checked)
                    DrawTrainLabels(g, train, trainName, locoCar, scaledTrain);
            }
        }

        private void DrawCar(System.Drawing.Graphics g, Train train, TrainCar car, TrainCar locoCar, float margin)
        {
            var t = new Traveller(train.RearTDBTraveller);
            var worldPos = car.WorldPosition;
            var dist = t.DistanceTo(worldPos.WorldLocation);
            if (dist > -1)
            {
                var scaledTrain = new PointF();
                float x;
                float y;
                t.Move(dist + car.CarLengthM / 2); // Move along from centre of car to front of car
                x = (t.TileX * 2048 + t.Location.X - subX) * xScale;
                y = pbCanvas.Height - (t.TileZ * 2048 + t.Location.Z - subY) * yScale;

                // If car out of view then skip it.
                if (x < -margin || y < -margin)
                    return;

                t.Move(-car.CarLengthM + (1 / xScale)); // Move from front of car to rear less 1 pixel to create a visible gap
                scaledTrain.X = x;
                scaledTrain.Y = y;
                x = (t.TileX * 2048 + t.Location.X - subX) * xScale;
                y = pbCanvas.Height - (t.TileZ * 2048 + t.Location.Z - subY) * yScale;

                // If car out of view then skip it.
                if (x < -margin || y < -margin)
                    return;

                SetTrainColor(train, locoCar, car);
                g.DrawLine(trainPen, new PointF(x, y), scaledTrain);
            }
        }

        private void SetTrainColor(Train t, TrainCar locoCar, TrainCar car)
        {
            // Draw train in green with locos in brown
            // HSL values
            // Saturation: 100/100
            // Hue: if loco then H=50/360 else H=120/360
            // Lightness: if active then L=40/100 else L=30/100
            // RGB values
            // active loco: RGB 204,170,0
            // inactive loco: RGB 153,128,0
            // active car: RGB 0,204,0
            // inactive car: RGB 0,153,0
            if (t.IsActive)
                if (car is MSTSLocomotive)
                    trainPen.Color = (car == locoCar) ? Color.FromArgb(204, 170, 0) : Color.FromArgb(153, 128, 0);
                else
                    trainPen.Color = Color.FromArgb(0, 204, 0);
            else
                if (car is MSTSLocomotive)
                trainPen.Color = Color.FromArgb(153, 128, 0);
            else
                trainPen.Color = Color.FromArgb(0, 153, 0);

            // Draw player train with loco in red
            if (t.TrainType == TrainType.Player && car == locoCar)
                trainPen.Color = Color.Red;
        }

        private void DrawTrainLabels(System.Drawing.Graphics g, Train t, string trainName, TrainCar firstCar, PointF scaledTrain)
        {
            WorldPosition worldPos = firstCar.WorldPosition;
            scaledTrain.X = (worldPos.TileX * 2048 - subX + worldPos.Location.X) * xScale;
            scaledTrain.Y = -25 + pbCanvas.Height - (worldPos.TileZ * 2048 - subY + worldPos.Location.Z) * yScale;
            if (rbShowActiveTrainLabels.Checked)
            {
                if (t.IsActive)
                    ShowTrainNameAndState(g, scaledTrain, t, trainName);
            }
            else
            {
                ShowTrainNameAndState(g, scaledTrain, t, trainName);
            }
        }

        private void ShowTrainNameAndState(System.Drawing.Graphics g, PointF scaledItem, Train t, string trainName)
        {
            if (simulator.TimetableMode)
            {
                var tTTrain = t as Orts.Simulation.Timetables.TTTrain;
                if (tTTrain != null)
                {
                    // Remove name of timetable, e.g.: ":SCE"
                    var lastPos = trainName.LastIndexOf(":");
                    var shortName = (lastPos > 0) ? trainName.Substring(0, lastPos) : trainName;

                    if (t.IsActive)
                    {
                        if (cbShowTrainState.Checked)
                        {
                            // 4:AI mode, 6:Mode, 7:Auth, 9:Signal, 12:Path
                            var status = tTTrain.GetStatus(Viewer.MilepostUnitsMetric);

                            // Add in fields 4 and 7
                            status = tTTrain.AddMovementState(status, Viewer.MilepostUnitsMetric);

                            var statuses = $"{status[4]} {status[6]} {status[7]} {status[9]}";

                            // Add path if it contains any deadlock information
                            if (ContainsDeadlockIndicators(status[12]))
                                statuses += status[12];

                            g.DrawString($"{shortName} {statuses}", trainFont, trainBrush, scaledItem);
                        }
                        else
                            g.DrawString(shortName, trainFont, trainBrush, scaledItem);
                    }
                    else
                        g.DrawString(shortName, trainFont, inactiveTrainBrush, scaledItem);
                }
            }
            else
                g.DrawString(trainName, trainFont, trainBrush, scaledItem);
        }

        /*
		 * # section is claimed by a train which is waiting for a signal.
		 * & section is occupied by more than one train.
		 * deadlock info (always linked to a switch node):
		 * · * possible deadlock location - start of a single track section shared with a train running in opposite direction.
		 * · ^ active deadlock - train from opposite direction is occupying or has reserved at least part of the common
		 *     single track section. Train will be stopped at this location – generally at the last signal ahead of this node.
		 * · ~ active deadlock at that location for other train - can be significant as this other train can block this
		 *     train’s path.
		*/
        private static readonly char[] DeadlockIndicators = "#&*^~".ToCharArray();

        private static bool ContainsDeadlockIndicators(string text)
        {
            return text.IndexOfAny(DeadlockIndicators) >= 0;
        }

        // The canvas is split into equally pitched rows. 
        // Each row has an array of 4 slots with StartX, EndX positions and a count of how many slots have been filled.
        // Arrays are used instead of lists to avoid delays for memory management.
        private void CleanTextCells()
        {
            if (alignedTextY == null || alignedTextY.Length != pbCanvas.Height / spacing) //first time to put text, or the text height has changed
            {
                alignedTextY = new Vector2[pbCanvas.Height / spacing][];
                alignedTextNum = new int[pbCanvas.Height / spacing];
                for (var i = 0; i < pbCanvas.Height / spacing; i++)
                    alignedTextY[i] = new Vector2[5]; //each line has at most 5 slots
            }
            for (var i = 0; i < pbCanvas.Height / spacing; i++)
                alignedTextNum[i] = 0;
        }

        // Returns a vertical position for the text that doesn't clash or returns -1
        // If the preferred space for text is occupied, then the slot above (-ve Y) is tested, then 2 sltos above, then 1 below.
        private float GetUnusedYLocation(float startX, float wantY, string name)
        {
            const float noFreeSlotFound = -1f;

            var desiredPositionY = (int)(wantY / spacing);  // The positionY of the ideal row for the text.
            var endX = startX + name.Length * trainFont.Size;
            //out of drawing area
            if (endX < 0)
                return noFreeSlotFound;

            int positionY = desiredPositionY;
            while (positionY >= 0 && positionY < alignedTextY.Length)
            {
                //if the line contains no text yet, put it there
                if (alignedTextNum[positionY] == 0)
                    return SaveLabelLocation(startX, endX, positionY);

                bool conflict = false;

                //check if it intersects with any labels already in this row
                for (var col = 0; col < alignedTextNum[positionY]; col++)
                {
                    var v = alignedTextY[positionY][col];
                    //check conflict with a text, v.X is the start of the text, v.Y is the end of the text
                    if ((endX >= v.X && startX <= v.Y))
                    {
                        conflict = true;
                        break;
                    }
                }

                if (conflict)
                {
                    positionY--; // Try a different row: -1, -2, +2, +1

                    if (positionY - desiredPositionY <= -2) // Cannot move up (-ve Y), so try to move it down (+ve Y)
                        positionY = desiredPositionY + 2;   // Try +2 then +1

                    if (positionY == desiredPositionY) // Back to original position again
                        return noFreeSlotFound;
                }
                else
                {
                    // Check that row has an unused column in its fixed size array
                    if (alignedTextNum[positionY] >= alignedTextY[positionY].Length)
                        return noFreeSlotFound;

                    return SaveLabelLocation(startX, endX, positionY);
                }
            }
            return noFreeSlotFound;
        }

        private float SaveLabelLocation(float startX, float endX, int positionY)
        {
            // add start and end location for the new label
            alignedTextY[positionY][alignedTextNum[positionY]] = new Vector2 { X = startX, Y = endX };

            alignedTextNum[positionY]++;

            return positionY * spacing;
        }
    }
}
