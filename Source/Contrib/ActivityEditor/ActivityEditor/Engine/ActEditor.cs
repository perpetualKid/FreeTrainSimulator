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

/// This module ...
/// 
/// Author: Stéfan Paitoni
/// Updates : 
/// 


using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Orts.ActivityEditor.Base.Formats;
using Orts.ActivityEditor.Wizard;
using Orts.Common;

namespace Orts.ActivityEditor.Engine
{
    public enum ToolClicked
    {
        NO_TOOL = 0,
        TAG = 1,
        STATION = 2,
        MOVE = 3,
        ROTATE = 4,
        START = 5,
        STOP = 6,
        WAIT = 7,
        ACTION = 8,
        CHECK = 9,
        AREA = 10,
        AREA_ADD = 11,
        DRAG = 12,
        ZOOM = 13,
        METASEGMENT = 14
    };

    public partial class ActEditor : Form
    {
        private bool saveEnabled = false;
        private bool saveAsEnabled = false;
        private bool loadEnabled = false;
        public List<Viewer2D> viewer2ds;
        public Viewer2D selectedViewer;
        
        //public List<AEActivity> aeActivity;
        //public AEActivity selectedActivity;
        private bool focusOnViewer = false;
        private ToolClicked ToolClicked;
        public Image ToolToUse = null;
        public Cursor CursorToUse = Cursors.Default;
        // private WorldPosition worldPos;
        public ActEditor()
        {
            InitializeComponent();
            SelectTools(TypeEditor.NONE);
            viewer2ds = new List<Viewer2D>();
            selectedViewer = null;
            CheckCurrentStatus();
            if (selectedViewer != null)
            {
                using (MemoryStream ms = new MemoryStream(Properties.Resources.point))
                {
                    selectedViewer.reduit = new Cursor(ms);
                }
            }

            //this.ActivityTool.Visible = false;
        }


        private void StatusEditor_Click(object sender, EventArgs e)
        {
            Form activeChild = this.ActiveMdiChild;
        }

        public void DisplayStatusMessage(string info)
        {
            this.StatusEditor.Text = info;
            this.Refresh();
        }

        private void CheckCurrentStatus()
        {
            if (!Program.AePreference.CheckPrefValidity())
            {
                DisplayStatusMessage("Please, Configure your Path!");
                return;
            }

            DisplayStatusMessage("Create New Activity");
        }
        private void PictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateNewMenuItems();
        }

        private void QuitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Really Quit?", "Exit", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                Application.Exit();
            }
        }

        private void LoadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenActivity.InitialDirectory = Program.AePreference.AEPath;
            if (OpenActivity.ShowDialog() == DialogResult.OK)
            {
                ActivityInfo activityInfo = ActivityInfo.LoadActivity(OpenActivity.FileName);
                if (activityInfo == null)
                    return;
                this.Cursor = Cursors.AppStarting;
                this.Refresh();

                Viewer2D newViewer = new Viewer2D(this, activityInfo);
                viewer2ds.Add(newViewer);
                focusOnViewer = true;
                SetFocus(newViewer);
                this.Cursor = Cursors.Default;
                this.Refresh();

                DisplayStatusMessage("Load Succeed");
            }
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.selectedViewer.Save();
            //this.selectedActivity.activityInfo.saveXml();
            DisplayStatusMessage("Save Activity Info Status");
        }

        private void ActivityToolStripMenuItem_Click(object sender, EventArgs e)
        {   //  Try to create a new activity Object with wizard
            ActivityInfo activityInfo = new ActivityInfo();
            activityInfo.Config(Program.AePreference.RoutePaths);
            WizardForm wiz = new WizardForm(activityInfo);
            if (wiz.ShowDialog() == DialogResult.OK)
            {
                Viewer2D newViewer = new Viewer2D(this, activityInfo);
                viewer2ds.Add(newViewer);
                focusOnViewer = true;
                SetFocus(newViewer);
                this.Cursor = Cursors.AppStarting;
                this.Refresh();

                this.Cursor = Cursors.Default;
                this.Refresh();
                DisplayStatusMessage("Load Succeed");
            }
        }

        private void TrafficToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //  For Generic Traffic definition
        }

        private void AboutActivityEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutActEdit aae = new AboutActEdit();
            aae.ShowDialog();
        }

        private void PreferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preference.Options optionWindow = new Preference.Options();
            optionWindow.ShowDialog();
        }
        private void UpdateNewMenuItems()
        {
            if (Program.AePreference.Loaded == true)
            {
                this.activityToolStripMenuItem.Enabled = true;
                this.trafficToolStripMenuItem.Enabled = true;
                this.loadMetada.Enabled = true;

            }
            else
            {
                this.activityToolStripMenuItem.Enabled = false;
                this.trafficToolStripMenuItem.Enabled = false;
                this.loadMetada.Enabled = false;
            }

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (selectedViewer == null)
            {
                DisplayStatusMessage("No Activity Selected");
                return;
            }
            DisplayStatusMessage("Type In Activity Descr");
            SimpleTextEd simpleTextEd = new SimpleTextEd();
            simpleTextEd.aEditer.Text = selectedViewer.Descr;
            simpleTextEd.ShowDialog();
            if (simpleTextEd.aEditer.TextLength > 0)
            {
                selectedViewer.Descr = simpleTextEd.aEditer.Text;
                DisplayStatusMessage("Act Description Updated");
            }
        }

        private void ActivityAECB_TextChanged(object sender, EventArgs e)
        {
#if WITH_DEBUG
            File.AppendAllText(@"C:\temp\AE.txt",
                "TagName_TextChanged: " + sender.ToString() + "\n");
#endif
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            if (selectedViewer == null)
                return;
            /* if (selectedActivity.GetTagVisibility())
            {
                //selectedActivity.TagPanel.Visible = false;
                //selectedActivity.SetTagVisibility(false);
            }
            else
            {
                //selectedActivity.TagPanel.Visible = true;
                //selectedActivity.SetTagVisibility(true);
            } */
        }

        private void AddTag_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Tag");
            selectedViewer.SetToolClicked(ToolClicked.TAG);
        }

        private void AddStation_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Station");
            selectedViewer.SetToolClicked(ToolClicked.STATION);
        }

        #region Activity    
        //  Not used actuially
        private void AddActivityStart_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Start Activity");
            ToolClicked = ToolClicked.START;
            ToolToUse = global::Orts.ActivityEditor.Properties.Resources.Activity_start;

        }

        private void AddActivityStop_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Stop Activity");
            ToolClicked = ToolClicked.STOP;
            ToolToUse = global::Orts.ActivityEditor.Properties.Resources.Activity_stop;

        }

        private void AddActivityWait_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Wait Point Activity");
            ToolClicked = ToolClicked.WAIT;
            ToolToUse = global::Orts.ActivityEditor.Properties.Resources.Activity_wait;

        }

        private void AddActivityAction_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Action Point Activity");
            ToolClicked = ToolClicked.ACTION;
            ToolToUse = global::Orts.ActivityEditor.Properties.Resources.Action;

        }

        private void AddActivityEval_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Evaluation Point Activity");
            ToolClicked = ToolClicked.CHECK;
            ToolToUse = global::Orts.ActivityEditor.Properties.Resources.Activity_check;

        }
        #endregion
        
        private void MoveSelected_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Please, Place Move Tool");
            selectedViewer.SetToolClicked(ToolClicked.MOVE);
        }

        private void RotateSelected_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Place Rotate Tool");
            selectedViewer.SetToolClicked(ToolClicked.ROTATE);
        }

        public void UnsetToolClick()
        {
            DisplayStatusMessage("Wait Action Form");
            ToolClicked = ToolClicked.NO_TOOL;
            ToolToUse = null;
            this.Cursor = Cursors.Default;
            CursorToUse = Cursors.Default;
        }

        private void AddArea_Click(object sender, EventArgs e)
        {
            DisplayStatusMessage("Add Station Area");
            selectedViewer.SetToolClicked(ToolClicked.AREA);
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.selectedViewer.Save();
        }

        public void SetFocus(Viewer2D viewer)
        {
            if (viewer == null || !focusOnViewer)
                return;
            if (selectedViewer != viewer)
            {
                this.SuspendLayout();
                if (selectedViewer != null)
                {
                    selectedViewer.UnsetFocus();
                }
                viewer.SetFocus();
                selectedViewer = viewer;
                if (viewer.ViewerMode == TypeEditor.ACTIVITY)
                {
                    activityOverview.Visible = true;
                    ActivityAECB.Text = viewer.aeConfig.aeActivity.activityInfo.ActivityName;
                }
                this.loadToolStripMenuItem.Enabled = this.loadEnabled;
                this.saveAsToolStripMenuItem.Enabled = this.saveAsEnabled;
                this.saveToolStripMenuItem.Enabled = this.saveEnabled;
                viewer.Show();
                viewer.Focus();
                viewer.Select();
                this.ResumeLayout(true);
                this.PerformLayout();
            }
        }

        private void RouteData_Enter(object sender, EventArgs e)
        {
            focusOnViewer = false;
            selectedViewer = null;
            this.Focus();
        }

        public bool AskFocus()
        {
            focusOnViewer = true;
            return focusOnViewer;
        }
        private void UpdateRouteCfg(object sender, EventArgs e)
        {
            RouteInfo routeInfo = new RouteInfo();
            routeInfo.Config(Program.AePreference.RoutePaths);
            WizardForm wiz = new WizardForm(routeInfo);
            if (wiz.ShowDialog() == DialogResult.OK)
            {
                this.Cursor = Cursors.AppStarting;
                this.Refresh();

                Viewer2D newViewer = new Viewer2D(this, routeInfo.route);
                viewer2ds.Add(newViewer);
                focusOnViewer = true;
                SetFocus(newViewer);

                this.Cursor = Cursors.Default;
                this.Refresh();
                DisplayStatusMessage("Load Succeed");
            }
        }

        static int cntAct = 0;
        static int cntRoute = 0;
        public void SelectTools(TypeEditor viewerMode)
        {
            if (viewerMode == TypeEditor.ACTIVITY)
            {
                cntAct++;
            }
            else if (viewerMode == TypeEditor.ROUTECONFIG)
            {
                cntRoute++;
            }
            else if (viewerMode == TypeEditor.TRAFFIC)
            {
            }
            else
            {
            }
            if (cntAct > 0)
            {
                this.activityCFG.Visible = true;
            }
            else
            {
                this.activityCFG.Visible = false;
            }
            if (cntRoute > 0)
            {
                this.routeCFG.Visible = true;
            }
            else
            {
                this.routeCFG.Visible = false;
            }
            if (cntRoute == 0 && cntAct == 0)
            {
                this.toolStrip3.Visible = false;
            }
            else
            {
                this.toolStrip3.Visible = true;
            }
        }

        public void UnselectTools(TypeEditor viewerMode)
        {
            if (viewerMode == TypeEditor.ACTIVITY)
            {
                cntAct--;
            }
            else if (viewerMode == TypeEditor.ROUTECONFIG)
            {
                cntRoute--;
            }
            else if (viewerMode == TypeEditor.TRAFFIC)
            {
            }
            else
            {
            }
            if (cntAct > 0)
                this.activityCFG.Visible = true;
            else
            {
                cntAct = 0;
                this.activityCFG.Visible = false;
            }
            if (cntRoute > 0)
                this.routeCFG.Visible = true;
            else
            {
                cntRoute = 0;
                this.routeCFG.Visible = false;
            }
            if (cntRoute == 0 && cntAct == 0)
            {
                this.toolStrip3.Visible = false;
            }
            else
            {
                this.toolStrip3.Visible = true;
            }
        }

        private void ActEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (selectedViewer != null)
            {
                selectedViewer.Viewer2D_KeyDown(sender, e);
            }
        }

        private void EditMetaSegment(object sender, EventArgs e)
        {
            DisplayStatusMessage("Edit Metadata for Segment");
            selectedViewer.SetToolClicked(ToolClicked.METASEGMENT);
        }

        private void RouteData_Leave(object sender, EventArgs e)
        {
            DisplayStatusMessage("lose");
        }

        private void SaveRouteCfg(object sender, EventArgs e)
        {

        }
#if zorro
        public void CloseActivity(AEActivity activity)
        {
            this.SuspendLayout();
            AEActivity toClose;
            //unsetFocus(activity);
            int item = aeActivity.FindIndex(0, place => place.activityInfo.ActivityName == activity.activityInfo.ActivityName);
            toClose = aeActivity[item];
            aeActivity.RemoveAt(item);
            if (aeActivity.Count > 0)
            {
                selectedActivity = aeActivity[0];
                setFocus(aeActivity[0]);
                //selectedActivity.TagPanel.Visible = true;
                //selectedActivity.StationPanel.Visible = true;
                selectedActivity.ActivityPanel.Visible = true;
                selectedActivity.SetTagVisibility(true);
            }
            else
            {
                activity.ActivityPanel.Visible = false;
                activity.ActivityPanel.Refresh();
                this.ActivityAECB.ResetText();
                selectedActivity = null;
                DisplayStatusMessage(Program.intlMngr.GetString("NoMoreAct"));
            }
            this.ResumeLayout(true);
        }

        public void unsetFocus(AEActivity activity)
        {
            this.loadToolStripMenuItem.Enabled = false;
            this.saveAsToolStripMenuItem.Enabled = false;
            this.saveToolStripMenuItem.Enabled = false;
        }

#endif
    }
}
