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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

using Orts.Common.Position;
using Orts.Simulation;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.Physics;

using Color = System.Drawing.Color;
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

        private string name = "";

        private ImageList imageList1;
        /// <summary>
        /// contains the last position of the mouse
        /// </summary>
        private Pen redPen = new Pen(Color.Red);
        private Pen greenPen = new Pen(Color.Green);
        private Pen orangePen = new Pen(Color.Orange);
        private Pen trainPen = new Pen(Color.DarkGreen);
        private Pen pathPen = new Pen(Color.DeepPink);
        private Pen grayPen = new Pen(Color.Gray);

        //the train selected by leftclicking the mouse
        public Train PickedTrain;

        private Timer UITimer;
        private Viewer Viewer;

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
        /// Creates a new DebugViewerForm.
        /// </summary>
        /// <param name="simulator"></param>
        /// /// <param name="viewer"></param>
        public DispatchViewer(Viewer viewer)
        {
            InitializeComponent();
            Visible = false;
            simulator = Simulator.Instance;
            this.Viewer = viewer;

            // initialise the timer used to handle user input
            UITimer = new Timer();
            UITimer.Interval = 100;
            UITimer.Tick += new System.EventHandler(UITimer_Tick);
            UITimer.Start();

            chkAllowUserSwitch.Checked = false;
            if (MultiPlayerManager.IsMultiPlayer())
            { MultiPlayerManager.Instance().AllowedManualSwitch = false; }

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
        private SolidBrush trainBrush;

        private void UITimer_Tick(object sender, EventArgs e)
        {
            if (!Viewer.DebugViewerEnabled)
            { 
                this.Visible = false; 
                firstShow = true; 
                return; 
            }
            else
                this.Visible = true;

            GenerateView();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Viewer.DebugViewerEnabled = false;
            base.OnClosing(e);
        }

        public void ToggleVisibility()
        {
            if (InvokeRequired)
                Invoke(new Action(() => ToggleVisibility()));
            Visible = !Visible;
        }
        #region initData

        /// <summary>
        /// Initialises the picturebox and the image it contains. 
        /// </summary>
        public void InitImage()
        {
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

        /// <summary>
        /// Regenerates the 2D view. At the moment, examines the track network
        /// each time the view is drawn. Later, the traversal and drawing can be separated.
        /// </summary>
        public void GenerateView(bool dragging = false)
        {
            if (tWindow.SelectedIndex == 1)
            {
                GenerateTimetableView(dragging);
                return;
            }

            InitImage();

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

            try
            {
                CheckAvatar();
            }
            catch { } //errors for avatar, just ignore
        }
        #endregion

        private void refreshButton_Click(object sender, EventArgs e)
        {
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
                    tmp = string.Concat(tmp.AsSpan(0, index), "\r");
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
            GenerateView();
        }


        #region Timetable
        public int DaylightOffsetHrs { get; set; }

        /// <summary>
        /// Add or subtract hours of daylight to more easily observe activity during the night.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nudDaylightOffsetHrs_ValueChanged(object sender, EventArgs e)
        {
            DaylightOffsetHrs = (int)nudDaylightOffsetHrs.Value;
        }

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
}
