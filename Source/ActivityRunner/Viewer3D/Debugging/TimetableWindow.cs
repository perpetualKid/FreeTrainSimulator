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
using System.Drawing;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.MultiPlayer;

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
                refreshButton.Text = "View Self";
            }

            btnSeeInGame.Visible = dispatchView;
            label1.Visible = dispatchView;
            resLabel.Visible = dispatchView;
            refreshButton.Visible = dispatchView;
        }

        private void SetDispatchMedia()
        {
            trainFont = new Font("Arial", 14, FontStyle.Bold);
            trainBrush = new SolidBrush(Color.Red);
        }

        private void ShowTimetableControls(bool timetableView)
        {
            lblSimulationTimeText.Visible = timetableView;
            lblSimulationTime.Visible = timetableView;
            lblShow.Visible = timetableView;
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
            trainBrush = new SolidBrush(Color.Red);
        }

        private void GenerateTimetableView(bool dragging)
        {
            ShowSimulationTime();

            InitImage();
        }

        private void ShowSimulationTime()
        {
            var ct = TimeSpan.FromSeconds(Simulator.Instance.ClockTime);
            lblSimulationTime.Text = $"{ct:hh}:{ct:mm}:{ct:ss}";
        }
    }
}
