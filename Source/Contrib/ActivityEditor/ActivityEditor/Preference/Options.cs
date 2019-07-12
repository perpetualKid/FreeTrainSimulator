using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Orts.ActivityEditor.ActionProperties;
using Orts.Formats.OR;

namespace Orts.ActivityEditor.Preference
{
    public partial class Options : Form
    {
        List<string> routePaths;

        public Options()
        {
            InitializeComponent();
            Program.AePreference.UpdateConfig();
            this.checkBox1.Checked = Program.AePreference.ShowAllSignal;
            this.ShowSnap.Checked = Program.AePreference.ShowSnapCircle;
            this.ShowLabelPlat.Checked = Program.AePreference.ShowPlSiLabel;
            this.MSTSPath.Text = Program.AePreference.MSTSPath;
            this.AEPath.Text = Program.AePreference.AEPath;
            ListRoutePaths.DataSource = Program.AePreference.RoutePaths;
            routePaths = new List<string> ();
            this.showTiles.Checked = Program.AePreference.ShowTiles;
            this.snapTrack.Checked = Program.AePreference.ShowSnapLine;
            this.SnapInfo.Checked = Program.AePreference.ShowSnapInfo;
            this.showRuler.Checked = Program.AePreference.ShowRuler;
            this.snapLine.Checked = Program.AePreference.ShowSnapLine;
            this.trackInfo.Checked = Program.AePreference.ShowTrackInfo;
            this.ListAvailable.DataSource = Program.AePreference.AvailableActions;
            this.ListUsed.DataSource = Program.AePreference.UsedActions;
        }
        
        private void DrawOnTab(object sender, DrawItemEventArgs e)
        {
            Font font;
            Brush back_brush;
            Brush fore_brush;
            Rectangle bounds = e.Bounds;

            this.tabControl1.Controls[e.Index].BackColor = Color.Silver;
            if (e.Index == this.tabControl1.SelectedIndex)
            {
                font = new Font(e.Font, e.Font.Style);
                back_brush = new SolidBrush(Color.DimGray);
                fore_brush = new SolidBrush(Color.White);
                bounds = new Rectangle(bounds.X + (this.tabControl1.Padding.X / 2), 
                    bounds.Y + this.tabControl1.Padding.Y, 
                    bounds.Width - this.tabControl1.Padding.X, 
                    bounds.Height - (this.tabControl1.Padding.Y * 2));
            }
            else
            {
                font = new Font(e.Font, e.Font.Style & ~FontStyle.Bold);
                back_brush = new SolidBrush(this.tabControl1.TabPages[e.Index].BackColor);
                fore_brush = new SolidBrush(this.tabControl1.TabPages[e.Index].ForeColor);
            }
            string tab_name = this.tabControl1.TabPages[e.Index].Text;
            StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.FillRectangle(back_brush, bounds);
            e.Graphics.DrawString(tab_name, font, fore_brush, bounds, sf);
            /*
            Brush background_brush = new SolidBrush(Color.DodgerBlue);
            Rectangle LastTabRect = this.tabControl1.GetTabRect(this.tabControl1.TabPages.Count - 1);
            Rectangle rect = new Rectangle();
            rect.Location = new Point(LastTabRect.Right + this.Left, this.Top);
            rect.Size = new Size(this.Right - rect.Left, LastTabRect.Height);
            e.Graphics.FillRectangle(background_brush, rect);
            background_brush.Dispose();
            sf.Dispose();
            back_brush.Dispose();
            fore_brush.Dispose();
            font.Dispose();
             */
        }

        private void BrowseMSTSPath_Click(object sender, EventArgs e)
        {
            if (MSTSfolderBrowse.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = MSTSfolderBrowse.SelectedPath;
                MSTSPath.Text = path;
                Program.AePreference.MSTSPath = path;
                string completeFileName = Path.Combine(path, "routes");
                if (Directory.Exists(completeFileName))
                {
                    Program.AePreference.RoutePaths.Add(completeFileName);
                    ListRoutePaths.DataSource = null;
                    ListRoutePaths.DataSource = Program.AePreference.RoutePaths;
                    RemoveRoutePaths.Enabled = true;

                }
            }
        }

        private void AddRoutePaths_Click(object sender, EventArgs e)
        {
            if (MSTSfolderBrowse.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = MSTSfolderBrowse.SelectedPath;
                Program.AePreference.RoutePaths.Add(path);
                ListRoutePaths.DataSource = null;
                ListRoutePaths.DataSource = Program.AePreference.RoutePaths;
                RemoveRoutePaths.Enabled = true;
            }
        }

        private void RemoveRoutePaths_Click(object sender, EventArgs e)
        {
            int selected = -1;
            //  Trouver le sélectionné
            selected = ListRoutePaths.SelectedIndex;
            if (selected >= 0)
            {
                Program.AePreference.RoutePaths.RemoveAt(selected);
                ListRoutePaths.DataSource = null;
                ListRoutePaths.DataSource = Program.AePreference.RoutePaths;
            }
            if (routePaths.Count < 1)
            {
                RemoveRoutePaths.Enabled = false;
                return;
            }
        }

        private void ConfigureRoutePath ()
        {
            if (Program.AePreference.RoutePaths.Count <= 0)
            {
                this.ListRoutePaths.DataSource = null;
                RemoveRoutePaths.Enabled = false;
            }
            else
            {
                this.ListRoutePaths.DataSource = Program.AePreference.RoutePaths;
                RemoveRoutePaths.Enabled = true;
            }
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            Program.AePreference.ShowAllSignal = checkBox1.Checked;
        }

        private void CheckedChanged(object sender, EventArgs e)
        {
            this.snapCircle.Enabled = ShowSnap.Checked;
            this.snapCircleLabel.Enabled = ShowSnap.Checked;
            this.snapCircle.Value = Program.AePreference.SnapCircle>0?Program.AePreference.SnapCircle:2;
            Program.AePreference.ShowSnapCircle = ShowSnap.Checked;
        }

        private void PlSiShow(object sender, EventArgs e)
        {
            this.PlSiZoomLevel.Enabled = ShowLabelPlat.Checked;
            this.PlSiLabel.Enabled = ShowLabelPlat.Checked;
            this.PlSiZoomLevel.Value = (decimal)(Program.AePreference.PlSiZoom > 0 ? Program.AePreference.PlSiZoom : 1);
            Program.AePreference.ShowPlSiLabel = ShowLabelPlat.Checked;
            Program.AePreference.PlSiZoom = (float)this.PlSiZoomLevel.Value;
        }

        private void SnapCircle_ValueChanged(object sender, EventArgs e)
        {
            Program.AePreference.SnapCircle = ((int)((NumericUpDown)sender).Value);
        }

        private void PlSiValue(object sender, EventArgs e)
        {
            Program.AePreference.PlSiZoom = ((float)((NumericUpDown)sender).Value);
            this.PlSiZoomLevel.Value = (decimal)(Program.AePreference.PlSiZoom > 0 ? Program.AePreference.PlSiZoom : 1);
        }

        private void ShowTiles_CheckedChanged(object sender, EventArgs e)
        {
            Program.AePreference.ShowTiles = showTiles.Checked;
        }

        private void SnapTrack_CheckedChanged(object sender, EventArgs e)
        {
            Program.AePreference.ShowSnapLine = snapTrack.Checked;
        }

        private void SnapInfo_CheckedChanged(object sender, EventArgs e)
        {
            Program.AePreference.ShowSnapInfo = SnapInfo.Checked;
        }

        private void ShowRuler_CheckedChanged(object sender, EventArgs e)
        {
            Program.AePreference.ShowRuler = showRuler.Checked;
        }
        private void OptionOK_click(object sender, EventArgs e)
        {
            Close();
            Program.AePreference.ShowAllSignal = this.checkBox1.Checked ;
            Program.AePreference.ShowSnapCircle = this.ShowSnap.Checked;
            Program.AePreference.ShowPlSiLabel = this.ShowLabelPlat.Checked;
            Program.AePreference.MSTSPath = this.MSTSPath.Text;
            //Program.aePreference.AEPath = this.AEPath.Text;
            Program.AePreference.ShowTiles = this.showTiles.Checked;
            Program.AePreference.ShowSnapLine = this.snapTrack.Checked;
            Program.AePreference.ShowSnapInfo = this.SnapInfo.Checked;
            Program.AePreference.ShowRuler = this.showRuler.Checked;
            Program.AePreference.ShowSnapLine = snapLine.Checked;
            Program.AePreference.ShowTrackInfo = this.trackInfo.Checked;
            Program.AePreference.SaveXml();
        }

        private void SnapLine_CheckedChanged(object sender, EventArgs e)
        {
            Program.AePreference.ShowSnapLine = snapLine.Checked;
        }

        private void TrackInfo_changed(object sender, EventArgs e)
        {
            Program.AePreference.ShowTrackInfo = trackInfo.Checked;
        }

        private void AddToUsed(object sender, EventArgs e)
        {
            int selected = -1;
            //  Trouver le sélectionné
            selected = ListAvailable.SelectedIndex;
            if (selected >= 0)
            {
                Program.AePreference.AddGenAction(Program.AePreference.AvailableActions[selected]);
                ListUsed.DataSource = null;
                ListUsed.DataSource = Program.AePreference.UsedActions;

            }
            if (routePaths.Count < 1)
            {
                return;
            }
        }

        private void RemoveFromUsed(object sender, EventArgs e)
        {
            int selected = -1;
            //  Trouver le sélectionné
            selected = ListUsed.SelectedIndex;
            if (selected >= 0)
            {
                Program.AePreference.RemoveGenAction(selected);
                ListUsed.DataSource = null;
                ListUsed.DataSource = Program.AePreference.UsedActions;

            }
            if (routePaths.Count < 1)
            {
                return;
            }
        }

        public void EditProperties(object sender, EventArgs e)
        {
            int selected = -1;
            //  Trouver le sélectionné
            selected = ListUsed.SelectedIndex;
            if (selected >= 0)
            {
                AuxActionRef action = Program.AePreference.GetAction(selected);
                if (action != null)
                {
                    if (action.GetType() == typeof(AuxActionHorn))
                        EditHornProperties(action);
                    else if (action.GetType() == typeof(AuxControlStart))
                        EditControlStartProperties(action);
                }
            }
        }

        public void EditHornProperties(AuxActionRef action)
        {
            HornProperties hornProperties = new HornProperties(action);
            hornProperties.ShowDialog();
            ((AuxActionHorn)action).SaveProperties(hornProperties.Action);

        }

        public void EditControlStartProperties(AuxActionRef action)
        {
            ControlStartProperties controlStartProperties = new ControlStartProperties(action);
            controlStartProperties.ShowDialog();
            ((AuxControlStart)action).SaveProperties(controlStartProperties.Action);
        }

        public void ShowCommentUsed(object sender, EventArgs e)
        {
            int selected = -1;
            //  Trouver le sélectionné
            selected = ListUsed.SelectedIndex;
            if (selected >= 0)
            {
                CommentAction.Text = Program.AePreference.GetComment(Program.AePreference.AvailableActions[selected]);
            }
        }

        public void ShowCommentAvailable(object sender, EventArgs e)
        {
            int selected = -1;
            //  Trouver le sélectionné
            selected = ListAvailable.SelectedIndex;
            if (selected >= 0)
            {
                CommentAction.Text = Program.AePreference.GetComment(Program.AePreference.AvailableActions[selected]);
            }
        }

        private void MouseDownUsed(object sender, MouseEventArgs e)
        {
            ListUsed.SelectedIndex = ListUsed.IndexFromPoint(e.X, e.Y);
            int index = ListUsed.SelectedIndex;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                CommentAction.Text = Program.AePreference.GetComment(Program.AePreference.AvailableActions[index]);
                AuxActionRef action = Program.AePreference.GetAction(index);
                if (action != null)
                {
                    if (action.GetType() == typeof(AuxActionHorn))
                        EditHornProperties(action);
                    else if (action.GetType() == typeof(AuxControlStart))
                        EditControlStartProperties(action);
                }
            }
        }

    }
}
