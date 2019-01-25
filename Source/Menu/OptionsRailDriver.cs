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
        private void ShowRailDriverLegend()
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
                }; ;

                railDriverLegend.Show(this);
            }
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
    }
}
