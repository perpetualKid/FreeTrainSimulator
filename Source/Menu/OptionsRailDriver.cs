using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ORTS.Common;
using ORTS.Settings;

namespace ORTS
{
    public partial class OptionsForm : Form
    {
        private Form railDriverLegend;
        private void ShowRailDriverLegend()
        {
            const int WM_NCLBUTTONDOWN = 0xA1;
            const int HT_CAPTION = 0x2;

            if (null == railDriverLegend)
            {
                void FormClosed(object sender, FormClosedEventArgs e)
                {
                    railDriverLegend.FormClosed -= FormClosed;
                    railDriverLegend = null;
                }
                void Legend_MouseDown(object sender, MouseEventArgs e)
                {
                    (sender as Control).Capture = false;
                    Message msg = Message.Create(railDriverLegend.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    base.WndProc(ref msg);
                }
                void KeyEvent(object sender, KeyEventArgs e)
                {
                    if (e.KeyValue == 0x1b)
                        railDriverLegend.Close();
                }

                Size clientSize = new Size(Properties.Resources.RailDriverLegend.Width, Properties.Resources.RailDriverLegend.Height);
                PictureBox legend = new PictureBox() { Image = Properties.Resources.RailDriverLegend, Size = clientSize };
                legend.MouseDown += Legend_MouseDown;

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
                railDriverLegend.FormClosed += FormClosed;
                railDriverLegend.KeyDown += KeyEvent; ;

                railDriverLegend.Show(this);
            }
        }

        private void InitializeRailDriverSettings()
        {
#if !DEBUG
            if (!Common.Input.RailDriverBase.GetInstance().Enabled)
                tabOptions.TabPages.Remove(tabPageRailDriver);
#endif

            panelRDButtons.Controls.Clear();

            var columnWidth = (panelRDButtons.ClientSize.Width - 20) / 2;

            var tempLabel = new Label();
            var tempKIC = new KeyInputControl(Settings.Input.Commands[(int)UserCommand.GameQuit], InputSettings.DefaultCommands[(int)UserCommand.GameQuit]);
            var rowTop = Math.Max(tempLabel.Margin.Top, tempKIC.Margin.Top);
            var rowHeight = tempKIC.Height;
            var rowSpacing = rowHeight + tempKIC.Margin.Vertical;

            var lastCategory = "";
            var i = 0;
            foreach (UserCommand command in Enum.GetValues(typeof(UserCommand)))
            {
                var name = InputSettings.GetPrettyLocalizedName(command);
                var category = ParseCategoryFrom(name);
                var descriptor = ParseDescriptorFrom(name);

                if (category != lastCategory)
                {
                    var catlabel = new Label
                    {
                        Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                        Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                        Text = category,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    catlabel.Font = new Font(catlabel.Font, FontStyle.Bold);
                    panelRDButtons.Controls.Add(catlabel);

                    lastCategory = category;
                    ++i;
                }

                var label = new Label
                {
                    Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight),
                    Text = descriptor,
                    TextAlign = ContentAlignment.MiddleRight
                };
                panelRDButtons.Controls.Add(label);

                var keyInputControl = new KeyInputControl(Settings.Input.Commands[(int)command], InputSettings.DefaultCommands[(int)command])
                {
                    Location = new Point(columnWidth + tempKIC.Margin.Left, rowTop + rowSpacing * i),
                    Size = new Size(columnWidth - tempKIC.Margin.Horizontal, rowHeight),
                    ReadOnly = true,
                    Tag = command
                };
                panelRDButtons.Controls.Add(keyInputControl);
                toolTip1.SetToolTip(keyInputControl, catalog.GetString("Click to change this key"));

                ++i;
            }

        }
    }
}
