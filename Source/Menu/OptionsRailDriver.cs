using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ORTS.Common;
using ORTS.Common.Input;
using ORTS.Settings;

namespace ORTS
{
    public partial class OptionsForm : Form
    {
        private RailDriverBase instance;
        private Form railDriverLegend;
        private RailDriverCalibrationSetting currentCalibrationStep;

        private static int[,] startingPoints = { 
            { 170, 110 }, { 170, 150 }, { 170, 60 }, //Reverser
            { 230, 120 },  { 230, 150 }, { 230, 90 }, { 230, 60 }, //Throttle
            { 340, 150}, { 340, 90}, { 340, 60}, // Auto Brake
            { 440, 150}, { 470, 150}, { 440, 60}, { 470, 60}, { 440, 150}, { 470, 150}, // Independent Brake
            { 520, 150}, { 535, 150}, { 550, 150}, // Rotary Switch 1
            { 520, 80}, { 540, 80}, { 560, 80}, // Rotary Switch 2
        };
        
        private Form GetRailDriverLegend()
        {
            const int WM_NCLBUTTONDOWN = 0xA1;
            const int HT_CAPTION = 0x2;

            if (null == railDriverLegend)
            {
                Size clientSize = new Size(Properties.Resources.RailDriverLegend.Width, Properties.Resources.RailDriverLegend.Height);
                PictureBox legend = new PictureBox() { Image = Properties.Resources.RailDriverLegend, Size = clientSize };
                legend.MouseDown += (object sender, MouseEventArgs e) =>
                {
                    (sender as Control).Capture = false;
                    Message msg = Message.Create(railDriverLegend.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    base.WndProc(ref msg);
                };

                railDriverLegend = new Form()
                {
                    ShowIcon = false,
                    ShowInTaskbar = false,
                    ControlBox = false,
                    Text = string.Empty,
                    FormBorderStyle = FormBorderStyle.FixedSingle,
                    ClientSize = clientSize
                };
                railDriverLegend.Controls.Add(legend);
                railDriverLegend.FormClosed += (object sender, FormClosedEventArgs e) =>
                {
                    railDriverLegend = null;
                };
                railDriverLegend.KeyDown += (object sender, KeyEventArgs e) =>
                {
                    if (e.KeyValue == 0x1b)
                        railDriverLegend.Close();
                };
                legend.Paint += (object sender, PaintEventArgs e) =>
                {
                    Pen penLine = new Pen(Color.Red, 4f);
                    Pen penArrow = new Pen(Color.Red, 4f) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };

                    if ((int)currentCalibrationStep < (startingPoints.Length / 2))
                    {
                        e.Graphics.DrawRectangle(penLine, startingPoints[(int)currentCalibrationStep, 0], startingPoints[(int)currentCalibrationStep, 1], 60, 40);
                        e.Graphics.DrawLine(penArrow, 10, 10, startingPoints[(int)currentCalibrationStep, 0] - 5, startingPoints[(int)currentCalibrationStep, 1] + 20);
                        e.Graphics.DrawString(GetStringAttribute.GetPrettyName(currentCalibrationStep), new Font("Arial", 14), new SolidBrush(Color.Red), 80, 225);
                    }
                };
            }
            else
            {
                railDriverLegend.Hide();
            }
            return railDriverLegend;
        }

        private Task<Panel> InitializeRailDriverInputControls()
        {
            TaskCompletionSource<Panel> tcs = new TaskCompletionSource<Panel>();
            Panel panel = new Panel();

            var columnWidth = (panelRDButtons.ClientSize.Width - 20) / 2;

            Label tempLabel = new Label();
            RDButtonInputControl tempControl = new RDButtonInputControl(Settings.RailDriver.UserCommands[(int)UserCommand.GameQuit], RailDriverSettings.GetDefaultValue(UserCommand.GameQuit), instance);
            int rowTop = Math.Max(tempLabel.Margin.Top, tempControl.Margin.Top);
            int rowHeight = tempControl.Height;
            int rowSpacing = rowHeight + tempControl.Margin.Vertical;

            string previousCategory = "";
            var i = 0;
            foreach (UserCommand command in Enum.GetValues(typeof(UserCommand)))
            {
                var name = InputSettings.GetPrettyLocalizedName(command);
                var category = ParseCategoryFrom(name);
                var descriptor = ParseDescriptorFrom(name);

                if (category != previousCategory)
                {
                    var catlabel = new Label
                    {
                        Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                        Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                        Text = category,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    catlabel.Font = new Font(catlabel.Font, FontStyle.Bold);
                    panel.Controls.Add(catlabel);

                    previousCategory = category;
                    ++i;
                }

                var label = new Label
                {
                    Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                    Text = descriptor,
                    TextAlign = ContentAlignment.MiddleRight
                };
                panel.Controls.Add(label);

                var keyInputControl = new RDButtonInputControl(Settings.RailDriver.UserCommands[(int)command], RailDriverSettings.GetDefaultValue(command), instance)
                {
                    Location = new Point(columnWidth + tempControl.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempControl.Margin.Horizontal, rowHeight),
                    Tag = command
                };
                panel.Controls.Add(keyInputControl);
                toolTip1.SetToolTip(keyInputControl, catalog.GetString("Click to change this button"));

                ++i;
            }
            tcs.SetResult(panel);
            return tcs.Task;
        }

        private async Task InitializeRailDriverSettingsAsync()
        {
            instance = RailDriverBase.GetInstance();
#if !DEBUG
            if (!instance.Enabled)
            {
                tabOptions.TabPages.Remove(tabPageRailDriver);
                await Task.CompletedTask;
            }
#endif
            panelRDButtons.SuspendLayout();
            panelRDButtons.Controls.Clear();

            Panel controls = await Task.Run(InitializeRailDriverInputControls);
            panelRDButtons.Controls.Add(controls);
            controls.Dock = DockStyle.Fill;
            panelRDButtons.ResumeLayout(true);

        }

        private void RunCalibration()
        {
            RailDriverCalibrationSetting nextStep = RailDriverCalibrationSetting.ReverserNeutral;
            DialogResult result = DialogResult.OK;
            while (result == DialogResult.OK && nextStep < RailDriverCalibrationSetting.ReverseReverser)
            {
                currentCalibrationStep = nextStep;
                railDriverLegend.Invalidate(true);  //enforce redraw legend to show guidance
                result = MessageBox.Show(railDriverLegend, $"Now calibrating \"{GetStringAttribute.GetPrettyName(currentCalibrationStep)}\". \r\n\r\nClick OK to continue, or Cancel to abort the process any time", "RailDriver Calibration", MessageBoxButtons.OKCancel);
                // Read Setting
                if (result == DialogResult.OK)
                {
                    //TODO
                }
                nextStep++;
            }
            railDriverLegend.Invalidate(true);
            if (nextStep == RailDriverCalibrationSetting.ReverseReverser)
            {
                if (MessageBox.Show(railDriverLegend, "Calibration Completed. Do you want to store the results?", "Calibration Done", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    // store settings
                }
            }
            currentCalibrationStep = RailDriverCalibrationSetting.PercentageCutOffDelta;
        }
    }
}
