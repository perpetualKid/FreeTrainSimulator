// COPYRIGHT 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

// Principal Author:
//     Author: Charlie Salts / Signalsoft Rail Consultancy Ltd.
// Contributor:
//    Richard Plokhaar / Signalsoft Rail Consultancy Ltd.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

using GetText.WindowsForms;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Popups;
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
using Control = System.Windows.Forms.Control;
using Image = System.Drawing.Image;

namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    /// <summary>
    /// Defines an external window for use as a debugging viewer 
    /// when using Open Rails 
    /// </summary>
    public partial class DispatchViewer : Form
    {

        /// <summary>
        /// Reference to the main simulator object.
        /// </summary>
        private readonly Simulator simulator;

        private int IM_Width = 720;
        private int IM_Height = 720;

        /// <summary>
        /// True when the user is dragging the route view
        /// </summary>
        private bool dragging;
        private WorldPosition worldPos;
        private float xScale = 1; // pixels / metre
        private float yScale = 1; // pixels / metre 

        private string name = "";
        private List<SwitchWidget> switchItemsDrawn;

        public SwitchWidget SwitchPickedItem { get; }
        public SignalWidget SignalPickedItem { get; }
        private ImageList imageList1;
        private List<Train> selectedTrainList;
        /// <summary>
        /// contains the last position of the mouse
        /// </summary>
        private System.Drawing.Point LastCursorPosition;
        private Pen redPen = new Pen(Color.Red);
        private Pen greenPen = new Pen(Color.Green);
        private Pen orangePen = new Pen(Color.Orange);
        private Pen trainPen = new Pen(Color.DarkGreen);
        private Pen pathPen = new Pen(Color.DeepPink);
        private Pen grayPen = new Pen(Color.Gray);
        private Pen platformPen = new Pen(Color.Blue);

        //the train selected by leftclicking the mouse
        public Train PickedTrain;

        // Note +ve pixels draw down from top, but +ve metres draw up from the bottom
        //
        // |-------- subX ---------->|           ViewWindow                
        //                           +--------------------------------+             
        //                           |                                |      
        //                           |                                |           
        //                           |                                |         
        //                           |                                |       
        //                  ==========                                |                -----   
        //                  ===========\         Track Extent         |                   ^
        //                  ====================================================          |
        //                           |                       \        |                   |
        //                           |                        ==================   ----   |
        //                           |                                |               ^   |
        //                           |                                |               |
        //                           |                                |               | maxY
        //                           +--------------------------------+                
        //                                                                           minY |
        // |----- minX --->|<- VW.X->|<------------ VW.Width -------->|                   |
        //                                                                            |   |
        // |------------------------------ maxX ------------------------------->|     |   |
        //                                                                            |   |
        // + 0,0 World origin                                                     ----------

        /// <summary>
        /// Defines the area to view, in meters. The left edge is meters from the leftmost extent of the route.
        /// </summary>
        private RectangleF viewWindow;

        /// <summary>
        /// Used to periodically check if we should shift the view when the
        /// user is holding down a "shift view" button.
        /// </summary>
        private Timer UITimer;
        private bool loaded;

        // Extents of the route in meters measured from the World origin
        private float minX = float.MaxValue;
        private float minY = float.MaxValue;
        private float maxX = float.MinValue;
        private float maxY = float.MinValue;

        private Viewer Viewer;
        /// <summary>
        /// Creates a new DebugViewerForm.
        /// </summary>
        /// <param name="simulator"></param>
        /// /// <param name="viewer"></param>
        public DispatchViewer(Viewer viewer)
        {
            InitializeComponent();

            simulator = Simulator.Instance;
            this.Viewer = viewer;

            // initialise the timer used to handle user input
            UITimer = new Timer();
            UITimer.Interval = 100;
            UITimer.Tick += new System.EventHandler(UITimer_Tick);
            UITimer.Start();

            viewWindow = new RectangleF(0, 0, 5000f, 5000f);
            windowSizeUpDown.Accelerations.Add(new NumericUpDownAcceleration(1, 100));
            chkAllowUserSwitch.Checked = false;
            selectedTrainList = new List<Train>();
            if (MultiPlayerManager.IsMultiPlayer())
            { MultiPlayerManager.Instance().AllowedManualSwitch = false; }

            InitData(RuntimeData.Instance.TrackDB.TrackNodes);
            InitImage();

            MultiPlayerManager.Instance().ServerChanged += (sender, e) =>
            {
                firstShow = true;
            };

            MultiPlayerManager.Instance().AvatarUpdated += (sender, e) =>
            {
                AddAvatar(e.User, e.Url);
            };

            MultiPlayerManager.Instance().MessageReceived += (sender, e) =>
            {
                AddNewMessage(e.Timestamp, e.Message);
            };

            tWindow.SelectedIndex = (MultiPlayerManager.IsMultiPlayer()) ? 0 : 1;
            SetControls();
        }

        private Font trainFont;
        private Font sidingFont;
        private Font PlatformFont;
        private SolidBrush trainBrush;
        private SolidBrush sidingBrush;
        private SolidBrush platformBrush;
        private SolidBrush inactiveTrainBrush;

        private double lastUpdateTime;

        /// <summary>
        /// When the user holds down the  "L", "R", "U", "D" buttons,
        /// shift the view. Avoids the case when the user has to click
        /// buttons like crazy.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UITimer_Tick(object sender, EventArgs e)
        {
            if (Viewer.DebugViewerEnabled == false)
            { this.Visible = false; firstShow = true; return; }
            else
                this.Visible = true;

            if (simulator.GameTime - lastUpdateTime < 1)
                return;
            lastUpdateTime = simulator.GameTime;

            GenerateView();
        }

        #region initData
        private void InitData(List<TrackNode> nodes)
        {
            if (!loaded)
            {
                // do this only once
                loaded = true;
                Localizer.Localize(this, Viewer.Catalog);
            }

            switchItemsDrawn = new List<SwitchWidget>();
            switches = new List<SwitchWidget>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is TrackEndNode)
                {
                    //buffers.Add(new PointF(currNode.UiD.TileX * 2048 + currNode.UiD.X, currNode.UiD.TileZ * 2048 + currNode.UiD.Z));
                }
                else if (nodes[i] is TrackVectorNode trackVectorNode)
                {

                    if (trackVectorNode.TrackVectorSections.Length > 1)
                    {
                        AddSegments(trackVectorNode.TrackVectorSections, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    else
                    {
                        TrackVectorSection s = trackVectorNode.TrackVectorSections[0];

                        foreach (TrackPin pin in trackVectorNode.TrackPins)
                        {

                            TrackNode connectedNode = nodes[pin.Link];

                            DebugVector A = new DebugVector(s.Location);
                            DebugVector B = new DebugVector(connectedNode.UiD.Location);
                            segments.Add(new LineSegment(A, B, null));
                        }


                    }
                }
                else if (nodes[i] is TrackJunctionNode trackJunctionNode)
                {
                    foreach (TrackPin pin in trackJunctionNode.TrackPins)
                    {
                        TrackVectorSection item = null;
                        TrackVectorNode vectorNode = nodes[pin.Link] as TrackVectorNode;
                        try
                        {
                            if (vectorNode == null || vectorNode.TrackVectorSections.Length < 1)
                                continue;
                            if (pin.Direction == TrackDirection.Reverse)
                                item = vectorNode.TrackVectorSections.First();
                            else
                                item = vectorNode.TrackVectorSections.Last();
                        }
                        catch { continue; }
                        DebugVector A = new DebugVector(trackJunctionNode.UiD.Location);
                        DebugVector B = new DebugVector(item.Location);
                        var x = DebugVector.DistanceSqr(A, B);
                        if (x < 0.1)
                            continue;
                        segments.Add(new LineSegment(B, A, item));
                    }
                    switches.Add(new SwitchWidget(trackJunctionNode));
                }
            }

            var maxsize = maxX - minX > maxY - minY ? maxX - minX : maxY - minY;
            // Take up to next 100
            maxsize = (int)(maxsize / 100 + 1) * 100;
            windowSizeUpDown.Maximum = (decimal)maxsize;
            Inited = true;

            if (RuntimeData.Instance.TrackDB?.TrackItems == null)
                return;

            PopulateItemLists();
        }

        private bool Inited;
        private List<LineSegment> segments = new List<LineSegment>();
        private List<SwitchWidget> switches;
        private List<SignalWidget> signals = new List<SignalWidget>();
        private List<SidingWidget> sidings = new List<SidingWidget>();
        private List<PlatformWidget> platforms = new List<PlatformWidget>();

        /// <summary>
        /// Initialises the picturebox and the image it contains. 
        /// </summary>
        public void InitImage()
        {
            pbCanvas.Width = IM_Width;
            pbCanvas.Height = IM_Height;

            if (pbCanvas.Image != null)
            {
                pbCanvas.Image.Dispose();
            }

            pbCanvas.Image = new Bitmap(pbCanvas.Width, pbCanvas.Height);
            imageList1 = new ImageList();
            this.AvatarView.View = System.Windows.Forms.View.LargeIcon;
            imageList1.ImageSize = new Size(64, 64);
            this.AvatarView.LargeImageList = this.imageList1;

        }

        #endregion

        #region avatar
        private Dictionary<string, Image> avatarList;
        public void AddAvatar(string name, string url)
        {
            if (avatarList == null)
                avatarList = new Dictionary<string, Image>();
            bool FindDefault = false;
            try
            {
                if (!simulator.Settings.ShowAvatar)
                    return;
                FindDefault = true;
                var request = WebRequest.Create(url);
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    Image newImage = Image.FromStream(stream);
                    avatarList[name] = newImage;
                }
            }
            catch
            {
                if (FindDefault)
                {
                    byte[] imageBytes = Convert.FromBase64String(imagestring);
                    using (MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
                    {

                        // Convert byte[] to Image
                        ms.Write(imageBytes, 0, imageBytes.Length);
                        Image newImage = Image.FromStream(ms, true);
                        avatarList[name] = newImage;
                    }
                }
                else
                {
                    avatarList[name] = null;
                }
            }
        }

        private int LostCount;//how many players in the lost list (quit)
        public void CheckAvatar()
        {
            if (!MultiPlayerManager.IsMultiPlayer() || MultiPlayerManager.OnlineTrains == null || MultiPlayerManager.OnlineTrains.Players == null)
                return;
            var player = MultiPlayerManager.OnlineTrains.Players;
            var username = MultiPlayerManager.GetUserName();
            player = player.Concat(MultiPlayerManager.Instance().lostPlayer).ToDictionary(x => x.Key, x => x.Value);
            if (avatarList == null)
                avatarList = new Dictionary<string, Image>();
            if (avatarList.Count == player.Count + 1 && LostCount == MultiPlayerManager.Instance().lostPlayer.Count)
                return;

            LostCount = MultiPlayerManager.Instance().lostPlayer.Count;
            //add myself
            if (!avatarList.ContainsKey(username))
            {
                AddAvatar(username, simulator.Settings.AvatarURL);
            }

            foreach (var p in player)
            {
                if (avatarList.ContainsKey(p.Key))
                    continue;
                AddAvatar(p.Key, p.Value.AvatarUrl);
            }

            Dictionary<string, Image> tmplist = null;
            foreach (var a in avatarList)
            {
                if (player.ContainsKey(a.Key) || a.Key == username)
                    continue;
                if (tmplist == null)
                    tmplist = new Dictionary<string, Image>();
                tmplist.Add(a.Key, a.Value);
            }

            if (tmplist != null)
            {
                foreach (var t in tmplist)
                    avatarList.Remove(t.Key);
            }
            imageList1.Images.Clear();
            AvatarView.Items.Clear();
            var i = 0;
            if (!simulator.Settings.ShowAvatar)
            {
                this.AvatarView.View = System.Windows.Forms.View.List;
                foreach (var pair in avatarList)
                {
                    if (pair.Key != username)
                        continue;
                    AvatarView.Items.Add(pair.Key);
                }
                i = 1;
                foreach (var pair in avatarList)
                {
                    if (pair.Key == username)
                        continue;
                    if (MultiPlayerManager.Instance().aiderList.Contains(pair.Key))
                    {
                        AvatarView.Items.Add(pair.Key + " (H)");
                    }
                    else if (MultiPlayerManager.Instance().lostPlayer.ContainsKey(pair.Key))
                    {
                        AvatarView.Items.Add(pair.Key + " (Q)");
                    }
                    else
                        AvatarView.Items.Add(pair.Key);
                    i++;
                }
            }
            else
            {
                this.AvatarView.View = System.Windows.Forms.View.LargeIcon;
                AvatarView.LargeImageList = imageList1;
                foreach (var pair in avatarList)
                {
                    if (pair.Key != username)
                        continue;

                    if (pair.Value == null)
                        AvatarView.Items.Add(pair.Key).ImageIndex = -1;
                    else
                    {
                        AvatarView.Items.Add(pair.Key).ImageIndex = 0;
                        imageList1.Images.Add(pair.Value);
                    }
                }

                i = 1;
                foreach (var pair in avatarList)
                {
                    if (pair.Key == username)
                        continue;
                    var text = pair.Key;
                    if (MultiPlayerManager.Instance().aiderList.Contains(pair.Key))
                        text = pair.Key + " (H)";

                    if (pair.Value == null)
                        AvatarView.Items.Add(name).ImageIndex = -1;
                    else
                    {
                        AvatarView.Items.Add(text).ImageIndex = i;
                        imageList1.Images.Add(pair.Value);
                        i++;
                    }
                }
            }
        }

        #endregion

        #region Draw
        private bool firstShow = true;
        private bool followTrain;
        private float subX, subY;
        private float oldWidth;
        private float oldHeight;

        //determine locations of buttons and boxes
        private void DetermineLocations()
        {
            if (this.Height < 600 || this.Width < 800)
                return;
            if (oldHeight != this.Height || oldWidth != label1.Left)//use the label "Res" as anchor point to determine the picture size
            {
                oldWidth = label1.Left;
                oldHeight = this.Height;
                IM_Width = label1.Left - 20;
                IM_Height = this.Height - pbCanvas.Top;
                pbCanvas.Width = IM_Width;
                //pictureBox1.Height = IM_Height;
                pbCanvas.Height = this.Height - pbCanvas.Top - 40;
                if (pbCanvas.Image != null)
                {
                    pbCanvas.Image.Dispose();
                }

                pbCanvas.Image = new Bitmap(pbCanvas.Width, pbCanvas.Height);

                if (btnAssist.Left - 10 < composeMSG.Right)
                {
                    var size = composeMSG.Width;
                    composeMSG.Left = msgAll.Left = msgSelected.Left = reply2Selected.Left = btnAssist.Left - 10 - size;
                    MSG.Width = messages.Width = composeMSG.Left - 20;
                }
                firstShow = true;
            }
        }

        /// <summary>
        /// Regenerates the 2D view. At the moment, examines the track network
        /// each time the view is drawn. Later, the traversal and drawing can be separated.
        /// </summary>
        public void GenerateView(bool dragging = false)
        {
            if (!Inited)
                return;

            if (tWindow.SelectedIndex == 1)
            {
                GenerateTimetableView(dragging);
                return;
            }

            if (pbCanvas.Image == null)
                InitImage();
            DetermineLocations();

            if (firstShow)
            {
                if (!MultiPlayerManager.IsServer())
                {
                    this.chkAllowUserSwitch.Visible = false;
                    this.chkAllowUserSwitch.Checked = false;
                    this.rmvButton.Visible = false;
                    this.btnAssist.Visible = false;
                    this.btnNormal.Visible = false;
                    this.msgAll.Text = "MSG to Server";
                }
                else
                {
                    this.msgAll.Text = "MSG to All";
                }
                if (MultiPlayerManager.IsServer())
                { rmvButton.Visible = true; chkAllowNew.Visible = true; chkAllowUserSwitch.Visible = true; }
                else
                { rmvButton.Visible = false; chkAllowNew.Visible = false; chkAllowUserSwitch.Visible = false; chkBoxPenalty.Visible = false; chkPreferGreen.Visible = false; }
            }
            if (firstShow || followTrain)
            {
                WorldPosition pos;
                //see who should I look at:
                //if the player is selected in the avatar list, show the player, otherwise, show the one with the lowest index
                if (simulator.PlayerLocomotive != null)
                    pos = simulator.PlayerLocomotive.WorldPosition;
                else
                    pos = simulator.Trains[0].Cars[0].WorldPosition;
                bool hasSelectedTrain = false;
                if (AvatarView.SelectedIndices.Count > 0 && !AvatarView.SelectedIndices.Contains(0))
                {
                    try
                    {
                        var i = 10000;
                        foreach (var index in AvatarView.SelectedIndices)
                        {
                            if ((int)index < i)
                                i = (int)index;
                        }
                        var name = AvatarView.Items[i].Text.Split(' ')[0].Trim();
                        if (MultiPlayerManager.OnlineTrains.Players.ContainsKey(name))
                        {
                            pos = MultiPlayerManager.OnlineTrains.Players[name].Train.Cars[0].WorldPosition;
                        }
                        else if (MultiPlayerManager.Instance().lostPlayer.ContainsKey(name))
                        {
                            pos = MultiPlayerManager.Instance().lostPlayer[name].Train.Cars[0].WorldPosition;
                        }
                        hasSelectedTrain = true;
                    }
                    catch { }
                }
                if (hasSelectedTrain == false && PickedTrain != null && PickedTrain.Cars != null && PickedTrain.Cars.Count > 0)
                {
                    pos = PickedTrain.Cars[0].WorldPosition;
                }
                var ploc = new PointF(pos.TileX * 2048 + pos.Location.X, pos.TileZ * 2048 + pos.Location.Z);
                viewWindow.X = ploc.X - minX - viewWindow.Width / 2;
                viewWindow.Y = ploc.Y - minY - viewWindow.Width / 2;
                firstShow = false;
            }

            try
            {
                CheckAvatar();
            }
            catch { } //errors for avatar, just ignore
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(pbCanvas.Image))
            {
                subX = minX + viewWindow.X;
                subY = minY + viewWindow.Y;
                g.Clear(Color.White);

                xScale = pbCanvas.Width / viewWindow.Width;
                yScale = pbCanvas.Height / viewWindow.Height;

                PointF[] points = new PointF[3];
                Pen p = grayPen;

                p.Width = MathHelper.Clamp(xScale, 1, 3);
                greenPen.Width = orangePen.Width = redPen.Width = p.Width;
                pathPen.Width = 2 * p.Width;
                trainPen.Width = p.Width * 6;
                var forwardDist = 100 / xScale;
                if (forwardDist < 5)
                    forwardDist = 5;
                //if (xScale > 3) p.Width = 3f;
                //else if (xScale > 2) p.Width = 2f;
                //else p.Width = 1f;
                PointF scaledA = new PointF(0, 0);
                PointF scaledB = new PointF(0, 0);
                PointF scaledC = new PointF(0, 0);

                foreach (var line in segments)
                {

                    scaledA = line.A.Scale(xScale, yScale, subX, subY);
                    scaledA.Y = pbCanvas.Height - scaledA.Y;
                    scaledB = line.B.Scale(xScale, yScale, subX, subY);
                    scaledB.Y = pbCanvas.Height - scaledB.Y;

                    if ((scaledA.X < 0 && scaledB.X < 0) || (scaledA.X > IM_Width && scaledB.X > IM_Width) || (scaledA.Y > IM_Height && scaledB.Y > IM_Height) || (scaledA.Y < 0 && scaledB.Y < 0))
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

                switchItemsDrawn.Clear();
                PointF scaledItem = new PointF();
                var width = 6f * p.Width;
                if (width > 15)
                    width = 15;//not to make it too large
                for (var i = 0; i < switches.Count; i++)
                {
                    SwitchWidget sw = switches[i];

                    scaledItem = new PointF((sw.Location.X - subX) * xScale, pbCanvas.Height - (sw.Location.Y - subY) * yScale);

                    if (scaledItem.X < 0 || scaledItem.X > IM_Width || scaledItem.Y > IM_Height || scaledItem.Y < 0)
                        continue;

                    if (sw.Item.SelectedRoute == sw.main)
                        g.FillEllipse(Brushes.Black, GetRect(scaledItem, width));
                    else
                        g.FillEllipse(Brushes.Gray, GetRect(scaledItem, width));

                    //g.DrawString("" + sw.Item.TrJunctionNode.SelectedRoute, trainFont, trainBrush, scaledItem);

                    sw.Location2D.X = scaledItem.X;
                    sw.Location2D.Y = scaledItem.Y;
#if false
				 if (sw.main == sw.Item.TrJunctionNode.SelectedRoute)
				 {
					 scaledA.X = ((float)sw.mainEnd.X - minX - ViewWindow.X) * xScale; scaledA.Y = pictureBox1.Height - ((float)sw.mainEnd.Y - minY - ViewWindow.Y) * yScale;
					 g.DrawLine(redPen, scaledA, scaledItem);

				 }
#endif
                    switchItemsDrawn.Add(sw);
                }

                foreach (var s in signals)
                {
                    if (float.IsNaN(s.Location.X) || float.IsNaN(s.Location.Y))
                        continue;
                    scaledItem = new PointF((s.Location.X - subX) * xScale, pbCanvas.Height - (s.Location.Y - subY) * yScale);
                    if (scaledItem.X < 0 || scaledItem.X > IM_Width || scaledItem.Y > IM_Height || scaledItem.Y < 0)
                        continue;

                    s.Location2D.X = scaledItem.X;
                    s.Location2D.Y = scaledItem.Y;
                    if (s.Signal.SignalNormal())//only show nor
                    {
                        var color = Brushes.Green;
                        var pen = greenPen;
                        if (s.IsProceed == 0)
                        {
                        }
                        else if (s.IsProceed == 1)
                        {
                            color = Brushes.Orange;
                            pen = orangePen;
                        }
                        else
                        {
                            color = Brushes.Red;
                            pen = redPen;
                        }
                        g.FillEllipse(color, GetRect(scaledItem, width));
                        if (s.hasDir)
                        {
                            scaledB.X = (s.Dir.X - subX) * xScale;
                            scaledB.Y = pbCanvas.Height - (s.Dir.Y - subY) * yScale;
                            g.DrawLine(pen, scaledItem, scaledB);
                        }
                    }
                }

                if (true/*showPlayerTrain.Checked*/)
                {

                    CleanVerticalCells();//clean the drawing area for text of sidings and platforms
                    foreach (var sw in sidings)
                        scaledItem = DrawSiding(g, scaledItem, sw);
                    foreach (var pw in platforms)
                        scaledItem = DrawPlatform(g, scaledItem, pw);

                    var margin = 30 * xScale;//margins to determine if we want to draw a train
                    var margin2 = 5000 * xScale;

                    //variable for drawing train path
                    var mDist = 5000f;

                    selectedTrainList.Clear();
                    foreach (var t in simulator.Trains)
                        selectedTrainList.Add(t);

                    var redTrain = selectedTrainList.Count;

                    //choosen trains will be drawn later using blue, so it will overlap on the red lines
                    var chosen = AvatarView.SelectedItems;
                    if (chosen.Count > 0)
                    {
                        for (var i = 0; i < chosen.Count; i++)
                        {
                            var name = chosen[i].Text.Split(' ')[0].Trim(); //filter out (H) in the text
                            var train = MultiPlayerManager.OnlineTrains.FindTrain(name);
                            if (train != null)
                            { selectedTrainList.Remove(train); selectedTrainList.Add(train); redTrain--; }
                            //if selected include myself, will show it as blue
                            if (MultiPlayerManager.GetUserName() == name && simulator.PlayerLocomotive != null)
                            {
                                selectedTrainList.Remove(simulator.PlayerLocomotive.Train);
                                selectedTrainList.Add(simulator.PlayerLocomotive.Train);
                                redTrain--;
                            }

                        }
                    }

                    //trains selected in the avatar view list will be drawn in blue, others will be drawn in red
                    pathPen.Color = Color.Red;
                    var drawRed = 0;
                    int ValidTrain = selectedTrainList.Count;
                    //add trains quit into the end, will draw them in gray
                    var quitTrains = MultiPlayerManager.Instance().lostPlayer.Values
                        .Select((OnlinePlayer lost) => lost?.Train)
                        .Where((Train t) => t != null)
                        .Where((Train t) => !selectedTrainList.Contains(t));
                    selectedTrainList.AddRange(quitTrains);
                    foreach (Train t in selectedTrainList)
                    {
                        drawRed++;//how many red has been drawn
                        if (drawRed > redTrain)
                            pathPen.Color = Color.Blue; //more than the red should be drawn, thus draw in blue

                        name = "";
                        TrainCar firstCar = null;
                        if (t.LeadLocomotive != null)
                        {
                            worldPos = t.LeadLocomotive.WorldPosition;
                            name = Train.GetTrainName(t.LeadLocomotive.CarID);
                            firstCar = t.LeadLocomotive;
                        }
                        else if (t.Cars != null && t.Cars.Count > 0)
                        {
                            worldPos = t.Cars[0].WorldPosition;
                            name = Train.GetTrainName(t.Cars[0].CarID);
                            if (t.TrainType == TrainType.Ai)
                                name = t.Number.ToString() + ":" + t.Name;
                            firstCar = t.Cars[0];
                        }
                        else
                            continue;

                        if (xScale < 0.3 || t.FrontTDBTraveller == null || t.RearTDBTraveller == null)
                        {
                            worldPos = firstCar.WorldPosition;
                            scaledItem.X = (worldPos.TileX * 2048 - subX + worldPos.Location.X) * xScale;
                            scaledItem.Y = pbCanvas.Height - (worldPos.TileZ * 2048 - subY + worldPos.Location.Z) * yScale;
                            if (scaledItem.X < -margin2 || scaledItem.X > IM_Width + margin2 || scaledItem.Y > IM_Height + margin2 || scaledItem.Y < -margin2)
                                continue;
                            if (drawRed > ValidTrain)
                                g.FillRectangle(Brushes.Gray, GetRect(scaledItem, 15f));
                            else
                            {
                                if (t == PickedTrain)
                                    g.FillRectangle(Brushes.Red, GetRect(scaledItem, 15f));
                                else
                                    g.FillRectangle(Brushes.DarkGreen, GetRect(scaledItem, 15f));
                                scaledItem.Y -= 25;
                                DrawTrainPath(t, subX, subY, pathPen, g, scaledA, scaledB, mDist);
                            }
                            g.DrawString(name, trainFont, trainBrush, scaledItem);
                            continue;
                        }

                        var loc = t.FrontTDBTraveller.WorldLocation;
                        scaledItem = new PointF((loc.TileX * 2048 + loc.Location.X - subX) * xScale, pbCanvas.Height - (loc.TileZ * 2048 + loc.Location.Z - subY) * yScale);
                        if (scaledItem.X < -margin2 || scaledItem.X > IM_Width + margin2 || scaledItem.Y > IM_Height + margin2 || scaledItem.Y < -margin2)
                            continue;

                        //train quit will not draw path, others will draw it
                        if (drawRed <= ValidTrain)
                            DrawTrainPath(t, subX, subY, pathPen, g, scaledA, scaledB, mDist);

                        trainPen.Color = Color.DarkGreen;
                        foreach (var car in t.Cars)
                        {
                            float x, y;
                            Traveller t1 = new Traveller(t.RearTDBTraveller);
                            worldPos = car.WorldPosition;
                            var dist = t1.DistanceTo(worldPos.WorldLocation);
                            if (dist > 0)
                            {
                                t1.Move(dist - 1 + car.CarLengthM / 2);
                                x = (t1.TileX * 2048 + t1.Location.X - subX) * xScale;
                                y = pbCanvas.Height - (t1.TileZ * 2048 + t1.Location.Z - subY) * yScale;
                                //x = (worldPos.TileX * 2048 + worldPos.Location.X - minX - ViewWindow.X) * xScale; y = pictureBox1.Height - (worldPos.TileZ * 2048 + worldPos.Location.Z - minY - ViewWindow.Y) * yScale;
                                if (x < -margin || x > IM_Width + margin || y > IM_Height + margin || y < -margin)
                                    continue;

                                scaledItem.X = x;
                                scaledItem.Y = y;

                                t1.Move(-car.CarLengthM);
                                x = (t1.TileX * 2048 + t1.Location.X - subX) * xScale;
                                y = pbCanvas.Height - (t1.TileZ * 2048 + t1.Location.Z - subY) * yScale;
                                if (x < -margin || x > IM_Width + margin || y > IM_Height + margin || y < -margin)
                                    continue;

                                scaledA.X = x;
                                scaledA.Y = y;

                                //if the train has quit, will draw in gray, if the train is selected by left click of the mouse, will draw it in red
                                if (drawRed > ValidTrain)
                                    trainPen.Color = Color.Gray;
                                else if (t == PickedTrain)
                                    trainPen.Color = Color.Red;
                                g.DrawLine(trainPen, scaledA, scaledItem);

                                //g.FillEllipse(Brushes.DarkGreen, GetRect(scaledItem, car.Length * xScale));
                            }
                        }
                        worldPos = firstCar.WorldPosition;
                        scaledItem.X = (worldPos.TileX * 2048 - subX + worldPos.Location.X) * xScale;
                        scaledItem.Y = -25 + pbCanvas.Height - (worldPos.TileZ * 2048 - subY + worldPos.Location.Z) * yScale;

                        g.DrawString(name, trainFont, trainBrush, scaledItem);

                    }
                }

            }

            pbCanvas.Invalidate();
        }

        private PointF DrawSiding(System.Drawing.Graphics g, PointF scaledItem, SidingWidget s)
        {
            scaledItem.X = (s.Location.X - subX) * xScale;
            scaledItem.Y = DetermineSidingLocation(scaledItem.X, pbCanvas.Height - (s.Location.Y - subY) * yScale, s.Name);
            if (scaledItem.Y >= 0f) //if we need to draw the siding names
            {

                g.DrawString(s.Name, sidingFont, sidingBrush, scaledItem);
            }
            return scaledItem;
        }
        private PointF DrawPlatform(System.Drawing.Graphics g, PointF scaledItem, PlatformWidget s)
        {
            scaledItem.X = (s.Location.X - subX) * xScale;
            scaledItem.Y = DetermineSidingLocation(scaledItem.X, pbCanvas.Height - (s.Location.Y - subY) * yScale, s.Name);
            if (scaledItem.Y >= 0f) //if we need to draw the siding names
            {

                g.DrawString(s.Name, sidingFont, sidingBrush, scaledItem);
            }
            return scaledItem;
        }

        private Vector2[][] alignedTextY;
        private int[] alignedTextNum;
        private const int spacing = 12;

        private void CleanVerticalCells()
        {
            if (alignedTextY == null || alignedTextY.Length != IM_Height / spacing) //first time to put text, or the text height has changed
            {
                alignedTextY = new Vector2[IM_Height / spacing][];
                alignedTextNum = new int[IM_Height / spacing];
                for (var i = 0; i < IM_Height / spacing; i++)
                    alignedTextY[i] = new Vector2[4]; //each line has at most 4 sidings
            }
            for (var i = 0; i < IM_Height / spacing; i++)
            { alignedTextNum[i] = 0; }

        }
        private float DetermineSidingLocation(float startX, float wantY, string name)
        {
            //out of drawing area
            if (startX < -64 || startX > IM_Width || wantY < -spacing || wantY > IM_Height)
                return -1f;

            int position = (int)(wantY / spacing);//the cell of the text it wants in
            if (position > alignedTextY.Length)
                return wantY;//position is larger than the number of cells
            var endX = startX + name.Length * trainFont.Size;
            int desiredPosition = position;
            while (position < alignedTextY.Length && position >= 0)
            {
                //if the line contains no text yet, put it there
                if (alignedTextNum[position] == 0)
                {
                    alignedTextY[position][alignedTextNum[position]].X = startX;
                    alignedTextY[position][alignedTextNum[position]].Y = endX;//add info for the text (i.e. start and end location)
                    alignedTextNum[position]++;
                    return position * spacing;
                }

                bool conflict = false;
                //check if it is intersect any one in the cell
                foreach (Vector2 v in alignedTextY[position])
                {
                    //check conflict with a text, v.x is the start of the text, v.y is the end of the text
                    if ((startX > v.X && startX < v.Y) || (endX > v.X && endX < v.Y) || (v.X > startX && v.X < endX) || (v.Y > startX && v.Y < endX))
                    {
                        conflict = true;
                        break;
                    }
                }
                if (conflict == false) //no conflict
                {
                    if (alignedTextNum[position] >= alignedTextY[position].Length)
                        return -1f;
                    alignedTextY[position][alignedTextNum[position]].X = startX;
                    alignedTextY[position][alignedTextNum[position]].Y = endX;//add info for the text (i.e. start and end location)
                    alignedTextNum[position]++;
                    return position * spacing;
                }
                position--;
                //cannot move up, then try to move it down
                if (position - desiredPosition < -1)
                {
                    position = desiredPosition + 2;
                }
                //could not find any position up or down, just return negative
                if (position == desiredPosition)
                    return -1f;
            }
            return position * spacing;
        }

        private const float SignalWarningDistance = 500;
        private const float DisplaySegmentLength = 10;
        private const float MaximumSectionDistance = 10000;
        private Dictionary<int, SignallingDebugWindow.TrackSectionCacheEntry> Cache = new Dictionary<int, SignallingDebugWindow.TrackSectionCacheEntry>();

        private SignallingDebugWindow.TrackSectionCacheEntry GetCacheEntry(Traveller position)
        {
            SignallingDebugWindow.TrackSectionCacheEntry rv;
            if (Cache.TryGetValue(position.TrackNode.Index, out rv) && (rv.Direction == position.Direction))
                return rv;
            Cache[position.TrackNode.Index] = rv = new SignallingDebugWindow.TrackSectionCacheEntry()
            {
                Direction = position.Direction,
                Length = 0,
                Objects = new List<SignallingDebugWindow.TrackSectionObject>(),
            };
            var nodeIndex = position.TrackNode.Index;
            var trackNode = new Traveller(position);
            while (true)
            {
                rv.Length += MaximumSectionDistance - trackNode.MoveInSection(MaximumSectionDistance);
                if (!trackNode.NextSection())
                    break;
                if (trackNode.IsEnd)
                    rv.Objects.Add(new SignallingDebugWindow.TrackSectionEndOfLine() { Distance = rv.Length });
                else if (trackNode.IsJunction)
                    rv.Objects.Add(new SignallingDebugWindow.TrackSectionSwitch() { Distance = rv.Length, JunctionNode = trackNode.TrackNode as TrackJunctionNode, NodeIndex = nodeIndex });
                else
                    rv.Objects.Add(new SignallingDebugWindow.TrackSectionObject() { Distance = rv.Length }); // Always have an object at the end.
                if (trackNode.TrackNode.Index != nodeIndex)
                    break;
            }
            trackNode = new Traveller(position);

            rv.Objects = rv.Objects.OrderBy(tso => tso.Distance).ToList();
            return rv;
        }

        //draw the train path if it is within the window
        private void DrawTrainPath(Train train, float subX, float subY, Pen pathPen, System.Drawing.Graphics g, PointF scaledA, PointF scaledB, float MaximumSectionDistance)
        {
            bool ok = false;
            if (train == simulator.PlayerLocomotive.Train)
                ok = true;
            if (MultiPlayerManager.IsMultiPlayer())
            {
                if (MultiPlayerManager.OnlineTrains.FindTrain(train))
                    ok = true;
            }
            if (train.FirstCar != null & train.FirstCar.CarID.Contains("AI"))
                ok = true; //AI train
            if (Math.Abs(train.SpeedMpS) > 0.001)
                ok = true;
            if (ok == false)
                return;

            var DisplayDistance = MaximumSectionDistance;
            var position = train.MUDirection != MidpointDirection.Reverse ? new Traveller(train.FrontTDBTraveller) : new Traveller(train.RearTDBTraveller, true);
            var caches = new List<SignallingDebugWindow.TrackSectionCacheEntry>();
            // Work backwards until we end up on a different track section.
            var cacheNode = new Traveller(position);
            cacheNode.ReverseDirection();
            var initialNodeOffsetCount = 0;
            while (cacheNode.TrackNode.Index == position.TrackNode.Index && cacheNode.NextSection())
                initialNodeOffsetCount++;
            // Now do it again, but don't go the last track section (because it is from a different track node).
            cacheNode = new Traveller(position);
            cacheNode.ReverseDirection();
            for (var i = 1; i < initialNodeOffsetCount; i++)
                cacheNode.NextSection();
            // Push the location right up to the end of the section.
            cacheNode.MoveInSection(MaximumSectionDistance);
            // Now back facing the right way, calculate the distance to the train location.
            cacheNode.ReverseDirection();
            var initialNodeOffset = cacheNode.DistanceTo(position.WorldLocation);
            // Go and collect all the cache entries for the visible range of vector nodes (straights, curves).
            var totalDistance = 0f;
            while (!cacheNode.IsEnd && totalDistance - initialNodeOffset < DisplayDistance)
            {
                if (cacheNode.IsTrack)
                {
                    var cache = GetCacheEntry(cacheNode);
                    cache.Age = 0;
                    caches.Add(cache);
                    totalDistance += cache.Length;
                }
                var nodeIndex = cacheNode.TrackNode.Index;
                while (cacheNode.TrackNode.Index == nodeIndex && cacheNode.NextSection())
                    ;
            }

            var switchErrorDistance = initialNodeOffset + DisplayDistance + SignalWarningDistance;
            var signalErrorDistance = initialNodeOffset + DisplayDistance + SignalWarningDistance;
            var currentDistance = 0f;
            foreach (var cache in caches)
            {
                foreach (var obj in cache.Objects)
                {
                    var objDistance = currentDistance + obj.Distance;
                    if (objDistance < initialNodeOffset)
                        continue;

                    var switchObj = obj as SignallingDebugWindow.TrackSectionSwitch;
                    if (switchObj != null)
                    {
                        for (var pin = switchObj.JunctionNode.InPins; pin < switchObj.JunctionNode.InPins + switchObj.JunctionNode.OutPins; pin++)
                        {
                            if (switchObj.JunctionNode.TrackPins[pin].Link == switchObj.NodeIndex)
                            {
                                if (pin - switchObj.JunctionNode.InPins != switchObj.JunctionNode.SelectedRoute)
                                    switchErrorDistance = objDistance;
                                break;
                            }
                        }
                        if (switchErrorDistance < DisplayDistance)
                            break;
                    }

                }
                if (switchErrorDistance < DisplayDistance || signalErrorDistance < DisplayDistance)
                    break;
                currentDistance += cache.Length;
            }

            var currentPosition = new Traveller(position);
            currentPosition.Move(-initialNodeOffset);
            currentDistance = 0;

            foreach (var cache in caches)
            {
                var lastObjDistance = 0f;
                foreach (var obj in cache.Objects)
                {
                    var objDistance = currentDistance + obj.Distance;

                    for (var step = lastObjDistance; step < obj.Distance; step += DisplaySegmentLength)
                    {
                        var stepDistance = currentDistance + step;
                        var stepLength = DisplaySegmentLength > obj.Distance - step ? obj.Distance - step : DisplaySegmentLength;
                        var previousLocation = currentPosition.WorldLocation;
                        currentPosition.Move(stepLength);
                        if (stepDistance + stepLength >= initialNodeOffset && stepDistance <= initialNodeOffset + DisplayDistance)
                        {
                            var currentLocation = currentPosition.WorldLocation;
                            scaledA.X = (previousLocation.TileX * 2048 + previousLocation.Location.X - subX) * xScale;
                            scaledA.Y = pbCanvas.Height - (previousLocation.TileZ * 2048 + previousLocation.Location.Z - subY) * yScale;
                            scaledB.X = (currentLocation.TileX * 2048 + currentLocation.Location.X - subX) * xScale;
                            scaledB.Y = pbCanvas.Height - (currentPosition.TileZ * 2048 + currentPosition.Location.Z - subY) * yScale;
                            g.DrawLine(pathPen, scaledA, scaledB);
                        }
                    }
                    lastObjDistance = obj.Distance;

                    if (objDistance >= switchErrorDistance)
                        break;
                }
                currentDistance += cache.Length;
                if (currentDistance >= switchErrorDistance)
                    break;

            }

            currentPosition = new Traveller(position);
            currentPosition.Move(-initialNodeOffset);
            currentDistance = 0;
            foreach (var cache in caches)
            {
                var lastObjDistance = 0f;
                foreach (var obj in cache.Objects)
                {
                    currentPosition.Move(obj.Distance - lastObjDistance);
                    lastObjDistance = obj.Distance;

                    var objDistance = currentDistance + obj.Distance;
                    if (objDistance < initialNodeOffset || objDistance > initialNodeOffset + DisplayDistance)
                        continue;

                    var switchObj = obj as SignallingDebugWindow.TrackSectionSwitch;
                    if (switchObj != null)
                    {
                        for (var pin = switchObj.JunctionNode.InPins; pin < switchObj.JunctionNode.InPins + switchObj.JunctionNode.OutPins; pin++)
                        {
                            if (switchObj.JunctionNode.TrackPins[pin].Link == switchObj.NodeIndex && pin - switchObj.JunctionNode.InPins != switchObj.JunctionNode.SelectedRoute)
                            {
                                foreach (var sw in switchItemsDrawn)
                                {
                                    if (sw.Item == switchObj.JunctionNode)
                                    {
                                        var r = 6 * greenPen.Width;
                                        g.DrawLine(pathPen, new PointF(sw.Location2D.X - r, sw.Location2D.Y - r), new PointF(sw.Location2D.X + r, sw.Location2D.Y + r));
                                        g.DrawLine(pathPen, new PointF(sw.Location2D.X - r, sw.Location2D.Y + r), new PointF(sw.Location2D.X + r, sw.Location2D.Y - r));
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (objDistance >= switchErrorDistance)
                        break;
                }
                currentDistance += cache.Length;
                if (currentDistance >= switchErrorDistance)
                    break;
            }
            // Clean up any cache entries who haven't been using for 30 seconds.
            var oldCaches = Cache.Where(kvp => kvp.Value.Age > 30 * 4).ToArray();
            foreach (var oldCache in oldCaches)
                Cache.Remove(oldCache.Key);

        }
        #endregion

        /// <summary>
        /// Generates a rectangle representing a dot being drawn.
        /// </summary>
        /// <param name="p">Center point of the dot, in pixels.</param>
        /// <param name="size">Size of the dot's diameter, in pixels</param>
        /// <returns></returns>
        public static RectangleF GetRect(PointF p, float size)
        {
            return new RectangleF(p.X - size / 2f, p.Y - size / 2f, size, size);
        }

        /// <summary>
        /// Generates line segments from an array of TrVectorSection. Also computes 
        /// the bounds of the entire route being drawn.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="items"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <param name="simulator"></param>
        private void AddSegments(TrackVectorSection[] items, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            double tempX1, tempX2, tempZ1, tempZ2;

            for (int i = 0; i < items.Length - 1; i++)
            {
                DebugVector A = new DebugVector(items[i].Location);
                DebugVector B = new DebugVector(items[i + 1].Location);

                tempX1 = A.Location.TileX * 2048 + A.Location.Location.X;
                tempX2 = B.Location.TileX * 2048 + B.Location.Location.X;
                tempZ1 = A.Location.TileZ * 2048 + A.Location.Location.Z;
                tempZ2 = B.Location.TileZ * 2048 + B.Location.Location.Z;
                CalcBounds(ref maxX, tempX1, true);
                CalcBounds(ref maxY, tempZ1, true);
                CalcBounds(ref maxX, tempX2, true);
                CalcBounds(ref maxY, tempZ2, true);

                CalcBounds(ref minX, tempX1, false);
                CalcBounds(ref minY, tempZ1, false);
                CalcBounds(ref minX, tempX2, false);
                CalcBounds(ref minY, tempZ2, false);

                segments.Add(new LineSegment(A, B, items[i]));
            }
        }

        /// <summary>
        /// Given a value representing a limit, evaluate if the given value exceeds the current limit.
        /// If so, expand the limit.
        /// </summary>
        /// <param name="limit">The current limit.</param>
        /// <param name="value">The value to compare the limit to.</param>
        /// <param name="gt">True when comparison is greater-than. False if less-than.</param>
        private static void CalcBounds(ref float limit, double v, bool gt)
        {
            float value = (float)v;
            if (gt)
            {
                if (value > limit)
                {
                    limit = value;
                }
            }
            else
            {
                if (value < limit)
                {
                    limit = value;
                }
            }
        }


        private float ScrollSpeedX
        {
            get
            {
                return viewWindow.Width * 0.10f;
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            followTrain = false;
            firstShow = true;
            GenerateView();
        }

        private void rmvButton_Click(object sender, EventArgs e)
        {
            if (!MultiPlayerManager.IsServer())
                return;
            AvatarView.SelectedIndices.Remove(0);//remove myself is not possible.
            var chosen = AvatarView.SelectedItems;
            if (chosen.Count > 0)
            {
                for (var i = 0; i < chosen.Count; i++)
                {
                    var tmp = chosen[i];
                    var name = (tmp.Text.Split(' '))[0];//the name may have (H) in it, need to filter that out
                    if (MultiPlayerManager.OnlineTrains.Players.ContainsKey(name))
                    {
                        MultiPlayerManager.OnlineTrains.Players[name].Status = OnlinePlayerStatus.Removed;
                        MultiPlayerManager.BroadCast((new MSGMessage(name, "Error", "Sorry the server has removed you")).ToString());

                    }
                }
            }

        }

        private void windowSizeUpDown_ValueChanged(object sender, EventArgs e)
        {
            // this is the center, before increasing the size
            PointF center = new PointF(viewWindow.X + viewWindow.Width / 2f, viewWindow.Y + viewWindow.Height / 2f);


            float newSizeH = (float)windowSizeUpDown.Value;
            float verticalByHorizontal = viewWindow.Height / viewWindow.Width;
            float newSizeV = newSizeH * verticalByHorizontal;

            viewWindow = new RectangleF(center.X - newSizeH / 2f, center.Y - newSizeV / 2f, newSizeH, newSizeV);


            GenerateView();
        }


        protected override void OnMouseWheel(MouseEventArgs e)
        {
            decimal tempValue = windowSizeUpDown.Value;
            if (e.Delta < 0)
                tempValue /= 0.95m;
            else if (e.Delta > 0)
                tempValue *= 0.95m;
            else
                return;

            if (tempValue < windowSizeUpDown.Minimum)
                tempValue = windowSizeUpDown.Minimum;
            if (tempValue > windowSizeUpDown.Maximum)
                tempValue = windowSizeUpDown.Maximum;
            windowSizeUpDown.Value = tempValue;
        }

        private bool Zooming;
        private bool LeftClick;
        private bool RightClick;

        private void pictureBoxMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                LeftClick = true;
            if (e.Button == MouseButtons.Right)
                RightClick = true;

            if (LeftClick == true && RightClick == false)
            {
                if (dragging == false)
                {
                    dragging = true;
                }
            }
            else if (LeftClick == true && RightClick == true)
            {
                if (Zooming == false)
                    Zooming = true;
            }
            LastCursorPosition.X = e.X;
            LastCursorPosition.Y = e.Y;
            //MSG.Enabled = false;
            lblInstruction1.Visible = true;
            lblInstruction2.Visible = true;
            lblInstruction3.Visible = true;
            lblInstruction4.Visible = true;
        }

        private void pictureBoxMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                LeftClick = false;
            if (e.Button == MouseButtons.Right)
                RightClick = false;

            if (LeftClick == false)
            {
                dragging = false;
                Zooming = false;
            }

            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                PictureMoveAndZoomInOut(e.X, e.Y, 1200);
            }
            else if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                PictureMoveAndZoomInOut(e.X, e.Y, 30000);
            }
            else if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                PictureMoveAndZoomInOut(e.X, e.Y, windowSizeUpDown.Maximum);
            }
            lblInstruction1.Visible = false;
            lblInstruction2.Visible = false;
            lblInstruction3.Visible = false;
            lblInstruction4.Visible = false;

        }

        private ItemWidget findItemFromMouse(int x, int y, int range)
        {
            if (range < 5)
                range = 5;
            double closest = float.NaN;
            ItemWidget closestItem = null;

            //now check for trains (first car only)
            TrainCar firstCar;
            PickedTrain = null;
            float tX, tY;
            closest = 100f;

            foreach (var t in simulator.Trains)
            {
                firstCar = null;
                if (t.LeadLocomotive != null)
                {
                    worldPos = t.LeadLocomotive.WorldPosition;
                    firstCar = t.LeadLocomotive;
                }
                else if (t.Cars != null && t.Cars.Count > 0)
                {
                    worldPos = t.Cars[0].WorldPosition;
                    firstCar = t.Cars[0];

                }
                else
                    continue;

                worldPos = firstCar.WorldPosition;
                tX = (worldPos.TileX * 2048 - subX + worldPos.Location.X) * xScale;
                tY = pbCanvas.Height - (worldPos.TileZ * 2048 - subY + worldPos.Location.Z) * yScale;
                float xSpeedCorr = Math.Abs(t.SpeedMpS) * xScale * 1.5f;
                float ySpeedCorr = Math.Abs(t.SpeedMpS) * yScale * 1.5f;

                if (tX < x - range - xSpeedCorr || tX > x + range + xSpeedCorr || tY < y - range - ySpeedCorr || tY > y + range + ySpeedCorr)
                    continue;
                if (PickedTrain == null)
                    PickedTrain = t;
            }
            //if a train is picked, will clear the avatar list selection
            if (PickedTrain != null)
            {
                AvatarView.SelectedItems.Clear();
            }
            return null;
        }

        private void pictureBoxMouseMove(object sender, MouseEventArgs e)
        {
            if (tWindow.SelectedIndex == 1)
                TimetableDrag(sender, e);
            else
            {
                if (dragging && !Zooming)
                {
                    int diffX = LastCursorPosition.X - e.X;
                    int diffY = LastCursorPosition.Y - e.Y;

                    viewWindow.Offset(diffX * ScrollSpeedX / 10, -diffY * ScrollSpeedX / 10);
                    GenerateView();
                }
                else if (Zooming)
                {
                    decimal tempValue = windowSizeUpDown.Value;
                    if (LastCursorPosition.Y - e.Y < 0)
                        tempValue /= 0.95m;
                    else if (LastCursorPosition.Y - e.Y > 0)
                        tempValue *= 0.95m;

                    if (tempValue < windowSizeUpDown.Minimum)
                        tempValue = windowSizeUpDown.Minimum;
                    if (tempValue > windowSizeUpDown.Maximum)
                        tempValue = windowSizeUpDown.Maximum;
                    windowSizeUpDown.Value = tempValue;
                    GenerateView();

                }
                LastCursorPosition.X = e.X;
                LastCursorPosition.Y = e.Y;
            }
        }

        public bool AddNewMessage(double _, string msg)
        {
            if (messages.Items.Count > 10)
                messages.Items.RemoveAt(0);
            messages.Items.Add(msg);
            messages.SelectedIndex = messages.Items.Count - 1;
            messages.SelectedIndex = -1;
            return true;
        }

        private void chkAllowUserSwitch_CheckedChanged(object sender, EventArgs e)
        {
            MultiPlayerManager.Instance().AllowedManualSwitch = chkAllowUserSwitch.Checked;
            if (chkAllowUserSwitch.Checked == true)
            { MultiPlayerManager.BroadCast((new MSGMessage("All", "SwitchOK", "OK to switch")).ToString()); }
            else
            { MultiPlayerManager.BroadCast((new MSGMessage("All", "SwitchWarning", "Cannot switch")).ToString()); }
        }

        private void chkShowAvatars_CheckedChanged(object sender, EventArgs e)
        {
            simulator.Settings.ShowAvatar = chkShowAvatars.Checked;
            AvatarView.Items.Clear();
            if (avatarList != null)
                avatarList.Clear();
            if (chkShowAvatars.Checked)
                AvatarView.Font = new Font(FontFamily.GenericSansSerif, 12);
            else
                AvatarView.Font = new Font(FontFamily.GenericSansSerif, 16);
            try
            { CheckAvatar(); }
            catch { }
        }

        private const int CP_NOCLOSE_BUTTON = 0x200;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return myCp;
            }
        }

        private string imagestring = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAAACpJREFUOE9jYBjs4D/QgSBMNhg1ABKAFAUi2aFPNY0Ue4FiA6jmlUFsEABfyg/x8/L8/gAAAABJRU5ErkJggg==";

        private void composeMSG_Click(object sender, EventArgs e)
        {
            MSG.Enabled = true;
            MSG.Focus();
            msgAll.Enabled = true;
            if (messages.SelectedItems.Count > 0)
                msgSelected.Enabled = true;
            if (AvatarView.SelectedItems.Count > 0)
                reply2Selected.Enabled = true;
        }

        private void msgAll_Click(object sender, EventArgs e)
        {
            msgDefault();
        }

        private void msgDefault()
        {
            msgAll.Enabled = false;
            msgSelected.Enabled = false;
            reply2Selected.Enabled = false;
            if (!MultiPlayerManager.IsMultiPlayer())
                return;
            string msg = MSG.Text;
            msg = msg.Replace("\r", "", StringComparison.Ordinal);
            msg = msg.Replace("\t", "", StringComparison.Ordinal);
            MSG.Enabled = false;
            if (msg.Length > 0)
            {
                if (MultiPlayerManager.IsServer())
                {
                    var users = MultiPlayerManager.OnlineTrains.Players.Keys
                        .Select((string u) => $"{u}\r");
                    string user = string.Join("", users) + "0END";
                    string msgText = new MSGText(MultiPlayerManager.GetUserName(), user, msg).ToString();
                    try
                    {
                        MultiPlayerManager.Notify(msgText);
                    }
                    catch { }
                    finally
                    {
                        MSG.Text = "";
                    }
                }
                else
                {
                    var user = "0Server\r+0END";
                    MultiPlayerManager.Notify((new MSGText(MultiPlayerManager.GetUserName(), user, msg)).ToString());
                    MSG.Text = "";
                }
            }
        }
        private void replySelected(object sender, EventArgs e)
        {
            msgAll.Enabled = false;
            msgSelected.Enabled = false;
            reply2Selected.Enabled = false;

            if (!MultiPlayerManager.IsMultiPlayer())
                return;
            var msg = MSG.Text;
            msg = msg.Replace("\r", "", StringComparison.Ordinal);
            msg = msg.Replace("\t", "", StringComparison.Ordinal);
            MSG.Text = "";
            MSG.Enabled = false;
            if (msg.Length == 0)
                return;
            var user = "";
            if (messages.SelectedItems.Count > 0)
            {
                var chosen = messages.SelectedItems;
                for (var i = 0; i < chosen.Count; i++)
                {
                    var tmp = (string)(chosen[i]);
                    var index = tmp.IndexOf(':', StringComparison.Ordinal);
                    if (index < 0)
                        continue;
                    tmp = tmp.Substring(0, index) + "\r";
                    if (user.Contains(tmp, StringComparison.OrdinalIgnoreCase))
                        continue;
                    user += tmp;
                }
                user += "0END";
            }
            else
                return;
            MultiPlayerManager.Notify((new MSGText(MultiPlayerManager.GetUserName(), user, msg)).ToString());


        }

        private void checkKeys(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyValue == 13)
            {
                if (e.KeyValue == 13)
                {
                    var msg = MSG.Text;
                    msg = msg.Replace("\r", "", StringComparison.Ordinal);
                    msg = msg.Replace("\t", "", StringComparison.Ordinal);
                    msg = msg.Replace("\n", "", StringComparison.Ordinal);
                    MSG.Enabled = false;
                    MSG.Text = "";
                    if (msg.Length == 0)
                        return;
                    var user = "";

                    if (MultiPlayerManager.IsServer())
                    {
                        var users = MultiPlayerManager.OnlineTrains.Players.Keys
                            .Select((string u) => $"{u}\r");
                        user += string.Join("", users) + "0END";
                        string msgText = new MSGText(MultiPlayerManager.GetUserName(), user, msg).ToString();
                        try
                        {
                            MultiPlayerManager.Notify(msgText);
                        }
                        catch { }
                        finally
                        {
                            MSG.Text = "";
                        }
                    }
                    else
                    {
                        user = "0Server\r+0END";
                        MultiPlayerManager.Notify((new MSGText(MultiPlayerManager.GetUserName(), user, msg)).ToString());
                        MSG.Text = "";
                    }
                }
            }
        }

        private void msgSelected_Click(object sender, EventArgs e)
        {
            msgAll.Enabled = false;
            msgSelected.Enabled = false;
            reply2Selected.Enabled = false;
            MSG.Enabled = false;

            if (!MultiPlayerManager.IsMultiPlayer())
                return;
            var msg = MSG.Text;
            MSG.Text = "";
            msg = msg.Replace("\r", "", StringComparison.Ordinal);
            msg = msg.Replace("\t", "", StringComparison.Ordinal);
            if (msg.Length == 0)
                return;
            var user = "";
            if (AvatarView.SelectedItems.Count > 0)
            {
                var chosen = this.AvatarView.SelectedItems;
                for (var i = 0; i < chosen.Count; i++)
                {
                    var name = chosen[i].Text.Split(' ')[0]; //text may have (H) in it, so need to filter out
                    if (name == MultiPlayerManager.GetUserName())
                        continue;
                    user += name + "\r";
                }
                user += "0END";


            }
            else
                return;

            MultiPlayerManager.Notify((new MSGText(MultiPlayerManager.GetUserName(), user, msg)).ToString());

        }

        private void msgSelectedChanged(object sender, EventArgs e)
        {
            AvatarView.SelectedItems.Clear();
            msgSelected.Enabled = false;
            if (MSG.Enabled == true)
                reply2Selected.Enabled = true;
        }

        private void AvatarView_SelectedIndexChanged(object sender, EventArgs e)
        {
            messages.SelectedItems.Clear();
            reply2Selected.Enabled = false;
            if (MSG.Enabled == true)
                msgSelected.Enabled = true;
            if (AvatarView.SelectedItems.Count <= 0)
                return;
            var name = AvatarView.SelectedItems[0].Text.Split(' ')[0].Trim();
            if (name == MultiPlayerManager.GetUserName())
            {
                if (simulator.PlayerLocomotive != null)
                    PickedTrain = simulator.PlayerLocomotive.Train;
                else if (simulator.Trains.Count > 0)
                    PickedTrain = simulator.Trains[0];
            }
            else
                PickedTrain = MultiPlayerManager.OnlineTrains.FindTrain(name);

        }

        private void chkAllowNewCheck(object sender, EventArgs e)
        {
            MultiPlayerManager.Instance().AllowNewPlayer = chkAllowNew.Checked;
        }

        private void AssistClick(object sender, EventArgs e)
        {
            AvatarView.SelectedIndices.Remove(0);
            if (AvatarView.SelectedIndices.Count > 0)
            {
                var tmp = AvatarView.SelectedItems[0].Text.Split(' ');
                var name = tmp[0].Trim();
                if (MultiPlayerManager.Instance().aiderList.Contains(name))
                    return;
                if (MultiPlayerManager.OnlineTrains.Players.ContainsKey(name))
                {
                    MultiPlayerManager.BroadCast((new MSGAider(name, true)).ToString());
                    MultiPlayerManager.Instance().aiderList.Add(name);
                }
                AvatarView.Items.Clear();
                if (avatarList != null)
                    avatarList.Clear();
            }
        }

        private void btnNormalClick(object sender, EventArgs e)
        {
            if (AvatarView.SelectedIndices.Count > 0)
            {
                var tmp = AvatarView.SelectedItems[0].Text.Split(' ');
                var name = tmp[0].Trim();
                if (MultiPlayerManager.OnlineTrains.Players.ContainsKey(name))
                {
                    MultiPlayerManager.BroadCast((new MSGAider(name, false)).ToString());
                    MultiPlayerManager.Instance().aiderList.Remove(name);
                }
                AvatarView.Items.Clear();
                if (avatarList != null)
                    avatarList.Clear();
            }

        }

        private void btnFollowClick(object sender, EventArgs e)
        {
            followTrain = true;
        }

        private void chkOPenaltyHandle(object sender, EventArgs e)
        {
            MultiPlayerManager.Instance().CheckSpad = chkBoxPenalty.Checked;
            if (this.chkBoxPenalty.Checked == false)
            { MultiPlayerManager.BroadCast((new MSGMessage("All", "OverSpeedOK", "OK to go overspeed and pass stop light")).ToString()); }
            else
            { MultiPlayerManager.BroadCast((new MSGMessage("All", "NoOverSpeed", "Penalty for overspeed and passing stop light")).ToString()); }

        }

        private void chkPreferGreenHandle(object sender, EventArgs e)
        {
            MultiPlayerManager.Instance().PreferGreen = chkBoxPenalty.Checked;

        }

        public bool ClickedTrain;
        private void btnSeeInGameClick(object sender, EventArgs e)
        {
            if (PickedTrain != null)
                ClickedTrain = true;
            else
                ClickedTrain = false;
        }

        private void PictureMoveAndZoomInOut(int x, int y, decimal scale)
        {
            int diffX = x - pbCanvas.Width / 2;
            int diffY = y - pbCanvas.Height / 2;
            viewWindow.Offset(diffX / xScale, -diffY / yScale);
            if (scale < windowSizeUpDown.Minimum)
                scale = windowSizeUpDown.Minimum;
            if (scale > windowSizeUpDown.Maximum)
                scale = windowSizeUpDown.Maximum;
            windowSizeUpDown.Value = scale;
            GenerateView();
        }


        #region Timetable
        public int DaylightOffsetHrs { get; set; }

        private void TimetableDrag(object sender, MouseEventArgs e)
        {
            if (dragging && !Zooming)
            {
                int diffX = e.X - LastCursorPosition.X;
                int diffY = e.Y - LastCursorPosition.Y;

                GenerateView(true);
            }
            else if (Zooming)
            {
                decimal tempValue = windowSizeUpDown.Value;
                if (LastCursorPosition.Y - e.Y < 0)
                    tempValue /= 0.95m;
                else if (LastCursorPosition.Y - e.Y > 0)
                    tempValue *= 0.95m;

                if (tempValue < windowSizeUpDown.Minimum)
                    tempValue = windowSizeUpDown.Minimum;
                if (tempValue > windowSizeUpDown.Maximum)
                    tempValue = windowSizeUpDown.Maximum;
                windowSizeUpDown.Value = tempValue;
                GenerateView(true);
            }
            LastCursorPosition.X = e.X;
            LastCursorPosition.Y = e.Y;
        }

        private void pbCanvas_SizeChanged(object sender, EventArgs e)
        {
            var oldSizePxX = viewWindow.Width * xScale;
            var oldSizePxY = viewWindow.Height * yScale;
            var newSizePxX = pbCanvas.Width;
            var newSizePxY = pbCanvas.Height;
            var sizeIncreaseX = newSizePxX / oldSizePxX;
            var sizeIncreaseY = newSizePxY / oldSizePxY;

            // Could be clever and keep all the previous view still in view and centred at the same point.
            // Instead use the simplest solution:
            viewWindow.Width *= sizeIncreaseX;
            viewWindow.Height *= sizeIncreaseY;
        }

        /// <summary>
        /// Add or subtract hours of daylight to more easily observe activity during the night.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudDaylightOffsetHrs_ValueChanged(object sender, EventArgs e)
        {
            DaylightOffsetHrs = (int)nudDaylightOffsetHrs.Value;
        }

        //      private void bSwitchWindow_Click(object sender, EventArgs e)
        //      {
        //	DispatchWindowWanted = true;
        //	ShowTimetableControls(false);
        //	RestoreDispatchMedia();
        //}

        //private void RestoreDispatchMedia()
        //      {
        //	this.Name = "Dispatch Window";
        //	trainFont = new Font("Arial", 14, FontStyle.Bold);
        //	sidingFont = new Font("Arial", 12, FontStyle.Bold);
        //	trainBrush = new SolidBrush(Color.Red);
        //	sidingBrush = new SolidBrush(Color.Blue);
        //	pbCanvas.BackColor = Color.White;
        //}

        private void tWindow_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetControls();
        }

        /// <summary>
        /// Loads a relevant page from the manual maintained by James Ross's automatic build
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bTrainKey_Click(object sender, EventArgs e)
        {
            // This method is also compatible with .NET Core 3
            var psi = new ProcessStartInfo
            {
                FileName = "https://open-rails.readthedocs.io/en/latest/driving.html#extended-hud-for-dispatcher-information"
                ,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        #endregion
    }

    #region SignalWidget
    /// <summary>
    /// Defines a signal being drawn in a 2D view.
    /// </summary>
    public class SignalWidget : ItemWidget
    {
        public TrackItem Item;
        /// <summary>
        /// The underlying signal object as referenced by the TrItem.
        /// </summary>
        public Signal Signal;

        public PointF Dir;
        public bool hasDir;
        /// <summary>
        /// For now, returns true if any of the signal heads shows any "clear" aspect.
        /// This obviously needs some refinement.
        /// </summary>
        public int IsProceed
        {
            get
            {
                int returnValue = 2;

                foreach (var head in Signal.SignalHeads)
                {
                    if (head.SignalIndicationState == SignalAspectState.Clear_1 ||
                        head.SignalIndicationState == SignalAspectState.Clear_2)
                    {
                        returnValue = 0;
                    }
                    if (head.SignalIndicationState == SignalAspectState.Approach_1 ||
                        head.SignalIndicationState == SignalAspectState.Approach_2 || head.SignalIndicationState == SignalAspectState.Approach_3)
                    {
                        returnValue = 1;
                    }
                }

                return returnValue;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public SignalWidget(SignalItem item, Signal signal)
        {
            Item = item;
            Signal = signal;
            hasDir = false;
            Location = VectorFromLocation(item.Location);
            try
            {
                var node = RuntimeData.Instance.TrackDB.TrackNodes[signal.TrackNode];
                Vector2 v2;
                if (node is TrackVectorNode trackVectorNode)
                {
                    var ts = trackVectorNode.TrackVectorSections[0];
                    v2 = VectorFromLocation(ts.Location);
                }
                else if (node is TrackJunctionNode)
                {
                    var ts = node.UiD;
                    v2 = VectorFromLocation(ts.Location);
                }
                else
                    throw new Exception();
                var v1 = new Vector2(Location.X, Location.Y);
                var v3 = v1 - v2;
                v3.Normalize();
                v2 = v1 - Vector2.Multiply(v3, signal.TrackDirection == TrackDirection.Ahead ? 12f : -12f);
                Dir.X = v2.X;
                Dir.Y = v2.Y;
                v2 = v1 - Vector2.Multiply(v3, signal.TrackDirection == TrackDirection.Ahead ? 1.5f : -1.5f);//shift signal along the dir for 2m, so signals will not be overlapped
                Location.X = v2.X;
                Location.Y = v2.Y;
                hasDir = true;
            }
            catch { }
        }
    }
    #endregion

    #region SwitchWidget
    /// <summary>
    /// Defines a signal being drawn in a 2D view.
    /// </summary>
    public class SwitchWidget : ItemWidget
    {
        public TrackJunctionNode Item;
        public int main;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public SwitchWidget(TrackJunctionNode item)
        {
            Item = item;
            var TS = RuntimeData.Instance.TSectionDat.TrackShapes[item.ShapeIndex];  // TSECTION.DAT tells us which is the main route

            if (TS != null)
            { main = TS.MainRoute; }
            else
                main = 0;
            Location = VectorFromLocation(Item.UiD.Location);
        }
    }

    #endregion

    #region ItemWidget
    public abstract class ItemWidget
    {
        public Vector2 Location;
        public PointF Location2D;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        protected ItemWidget()
        {
            Location = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Location2D = new PointF(float.NegativeInfinity, float.NegativeInfinity);
        }

        protected static PointF PointFromLocation(in WorldLocation location)
        {
            return new PointF((float)(location.TileX * WorldLocation.TileSize + location.Location.X), (float)(location.TileZ * WorldLocation.TileSize + location.Location.Z));
        }

        protected static Vector2 VectorFromLocation(in WorldLocation location)
        {
            return new Vector2((float)(location.TileX * WorldLocation.TileSize + location.Location.X), (float)(location.TileZ * WorldLocation.TileSize + location.Location.Z));
        }
    }
    #endregion

    #region LineSegment
    /// <summary>
    /// Defines a geometric line segment.
    /// </summary>
    public class LineSegment
    {
        public DebugVector A;
        public DebugVector B;
        public DebugVector C;
        //public float radius;
        public bool isCurved;

        public float angle1, angle2;
        //public SectionCurve curve = null;
        //public TrVectorSection MySection;
        public LineSegment(DebugVector A, DebugVector B, TrackVectorSection Section)
        {
            this.A = A;
            this.B = B;

            isCurved = false;
            if (Section == null)
                return;
            //MySection = Section;
            int k = Section.SectionIndex;
            TrackSection ts = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(k);
            if (ts != null)
            {
                if (ts.Curved)
                {
                    float diff = (float)(ts.Radius * (1 - Math.Cos(ts.Angle * 3.14f / 360)));
                    if (diff < 3)
                        return; //not need to worry, curve too small
                                //curve = ts.SectionCurve;
                    Vector3 v = new Vector3(((B.Location.TileX - A.Location.TileX) * 2048 + B.Location.Location.X - A.Location.Location.X), 0, ((B.Location.TileZ - A.Location.TileZ) * 2048 + B.Location.Location.Z - A.Location.Location.Z));
                    isCurved = true;
                    Vector3 v2 = Vector3.Cross(Vector3.Up, v);
                    v2.Normalize();
                    v = v / 2;
                    v.X += A.Location.TileX * 2048 + A.Location.Location.X;
                    v.Z += A.Location.TileZ * 2048 + A.Location.Location.Z;
                    if (ts.Angle > 0)
                    {
                        v = v2 * -diff + v;
                    }
                    else
                        v = v2 * diff + v;
                    C = new DebugVector(0, v.X, 0, v.Z);
                }
            }

        }
    }

    #endregion

    #region SidingWidget

    /// <summary>
    /// Defines a siding name being drawn in a 2D view.
    /// </summary>
    public struct SidingWidget
    {
        public int Id;
        public PointF Location;
        public string Name;
        public int LinkId;

        /// <summary>
        /// The underlying track item.
        /// </summary>
        public SidingItem Item;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public SidingWidget(SidingItem item)
        {
            Id = item.TrackItemId;
            LinkId = item.LinkedSidingId;
            Item = item;
            Name = item.ItemName;
            Location = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);
        }
    }

    public struct PlatformWidget
    {
        public int Id;
        public PointF Location;
        public string Name;
        public PointF Extent1;
        public PointF Extent2;
        public int LinkId;
        public string Station;

        /// <summary>
        /// The underlying track item.
        /// </summary>
		public PlatformItem Item;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
		public PlatformWidget(PlatformItem item)
        {
            Id = item.TrackItemId;
            LinkId = item.LinkedPlatformItemId;
            Item = item;
            Name = item.ItemName;
            Station = item.Station;
            Location = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);
            Extent1 = default(PointF);
            Extent2 = default(PointF);
        }
    }

    #endregion

    public class DebugVector
    {
        private readonly WorldLocation location;
        public ref readonly WorldLocation Location => ref location;

        public DebugVector(int tileX, float x, int tileZ, float z) :
            this(new WorldLocation(tileX, tileZ, x, 0, z))
        { }

        public DebugVector(in WorldLocation location)
        {
            this.location = location;
        }

        public static double DistanceSqr(DebugVector v1, DebugVector v2)
        {
            return Math.Pow((v1.location.TileX - v2.location.TileX) * 2048 + v1.location.Location.X - v2.location.Location.X, 2)
                + Math.Pow((v1.location.TileZ - v2.location.TileZ) * 2048 + v1.location.Location.Z - v2.location.Location.Z, 2);
        }

        public PointF Scale(float xScale, float yScale, float subX, float subY)
        {
            return new PointF()
            {
                X = (location.TileX * 2048 - subX + location.Location.X) * xScale,
                Y = (location.TileZ * 2048 - subY + location.Location.Z) * yScale
            };
        }
    }
}
